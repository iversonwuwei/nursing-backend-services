using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Billing;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<BillingDbContext>(options =>
	options.UseNpgsql(PostgresConnectionStrings.Resolve(builder.Configuration, "BillingPostgres", "nursing_billing")));
builder.Services.AddSingleton<BillingTelemetry>();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "billing-service",
	ServiceType: "domain-service",
	BoundedContext: "billing-and-entitlement",
	Consumers: ["admin-bff", "family-bff", "notification-service"],
	Capabilities: ["bill-issuance", "payment-status", "overdue-management", "tenant-package-billing"]));

app.MapPost("/api/billing/invoices", async (HttpContext context, BillingInvoiceCreateRequest request, BillingDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, BillingTelemetry telemetry, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	using var activity = telemetry.StartActivity("billing.invoice.issue",
		new KeyValuePair<string, object?>("tenant.id", requestContext.TenantId),
		new KeyValuePair<string, object?>("billing.elder_id", request.ElderId),
		new KeyValuePair<string, object?>("billing.package", request.PackageName));

	var createdAtUtc = DateTimeOffset.UtcNow;
	var invoice = new BillingInvoiceEntity
	{
		InvoiceId = $"INV-{createdAtUtc.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		ElderId = request.ElderId,
		ElderName = request.ElderName,
		PackageName = request.PackageName,
		Amount = request.Amount,
		DueAtUtc = request.DueAtUtc,
		Status = "Issued",
		NotificationStatus = "Pending",
		CreatedAtUtc = createdAtUtc,
	};

	dbContext.Invoices.Add(invoice);
	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-BILL-{invoice.InvoiceId}",
		TenantId = requestContext.TenantId,
		AggregateType = "BillingInvoice",
		AggregateId = invoice.InvoiceId,
		EventType = "InvoiceIssued",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			invoice.InvoiceId,
			invoice.ElderId,
			invoice.ElderName,
			invoice.PackageName,
			invoice.Amount,
			invoice.DueAtUtc,
		}),
		CreatedAtUtc = createdAtUtc,
	});
	await dbContext.SaveChangesAsync(cancellationToken);
	telemetry.RecordInvoiceIssued(invoice.TenantId, invoice.PackageName);

	if (configuration.GetValue("Outbox:DispatchInlineOnWrite", false))
	{
		await BillingOutboxNotificationDispatcher.DispatchPendingAsync(
			dbContext,
			httpClientFactory.CreateClient(),
			context,
			configuration,
			cancellationToken,
			maxMessages: 1);
	}

	return Results.Ok(ToInvoiceResponse(invoice));
}).RequireAuthorization();

app.MapGet("/api/billing/invoices/{invoiceId}", async (string invoiceId, BillingDbContext dbContext, CancellationToken cancellationToken) =>
{
	var invoice = await dbContext.Invoices.FirstOrDefaultAsync(item => item.InvoiceId == invoiceId, cancellationToken);
	return invoice is null
		? Results.Problem(title: $"账单 {invoiceId} 不存在。", statusCode: StatusCodes.Status404NotFound)
		: Results.Ok(ToInvoiceResponse(invoice));
}).RequireAuthorization();

app.MapGet("/api/billing/elders/{elderId}/invoices", async (string elderId, BillingDbContext dbContext, CancellationToken cancellationToken) =>
{
	var invoices = await dbContext.Invoices.Where(item => item.ElderId == elderId).OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
	return Results.Ok(invoices.Select(ToInvoiceResponse));
}).RequireAuthorization();

app.MapGet("/api/billing/invoices", async (HttpContext context, BillingDbContext dbContext, CancellationToken cancellationToken, string? status, string? notificationStatus) =>
{
	var tenantId = context.GetPlatformRequestContext()?.TenantId;
	if (string.IsNullOrWhiteSpace(tenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var query = dbContext.Invoices.Where(item => item.TenantId == tenantId);
	if (!string.IsNullOrWhiteSpace(status))
	{
		query = query.Where(item => item.Status == status);
	}
	if (!string.IsNullOrWhiteSpace(notificationStatus))
	{
		query = query.Where(item => item.NotificationStatus == notificationStatus);
	}

	var invoices = await query.OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
	return Results.Ok(invoices.Select(ToInvoiceResponse));
}).RequireAuthorization();

app.MapPost("/api/billing/invoices/{invoiceId}/notifications/compensate", async (string invoiceId, HttpContext context, BillingNotificationCompensationRequest request, BillingDbContext dbContext, IConfiguration configuration, BillingTelemetry telemetry, CancellationToken cancellationToken) =>
{
	if (!IsCompensationCallerAuthorized(context, configuration))
	{
		return Results.Unauthorized();
	}

	var invoice = await dbContext.Invoices.FirstOrDefaultAsync(item => item.InvoiceId == invoiceId, cancellationToken);
	if (invoice is null)
	{
		return Results.Problem(title: $"账单 {invoiceId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	var existing = await dbContext.CompensationRecords.FirstOrDefaultAsync(item => item.NotificationId == request.NotificationId, cancellationToken);
	if (existing is not null)
	{
		return Results.Ok(ToCompensationResponse(existing));
	}

	invoice.Status = "ActionRequired";
	invoice.NotificationStatus = "Failed";
	invoice.LastNotificationFailureCode = request.FailureCode;
	invoice.LastNotificationFailureReason = request.FailureReason;
	invoice.UpdatedAtUtc = DateTimeOffset.UtcNow;

	var record = new BillingCompensationRecordEntity
	{
		CompensationId = $"CMP-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
		TenantId = invoice.TenantId,
		InvoiceId = invoice.InvoiceId,
		NotificationId = request.NotificationId,
		CorrelationId = request.CorrelationId,
		FailureCode = request.FailureCode,
		FailureReason = request.FailureReason,
		Status = "Open",
		CreatedAtUtc = DateTimeOffset.UtcNow,
	};

	dbContext.CompensationRecords.Add(record);
	await dbContext.SaveChangesAsync(cancellationToken);
	telemetry.RecordCompensationCreated(invoice.TenantId, record.FailureCode);

	return Results.Ok(ToCompensationResponse(record));
});

app.MapPost("/api/billing/invoices/{invoiceId}/compensations/{compensationId}/resolve", async (string invoiceId, string compensationId, BillingCompensationResolveRequest request, BillingDbContext dbContext, BillingTelemetry telemetry, CancellationToken cancellationToken) =>
{
	var invoice = await dbContext.Invoices.FirstOrDefaultAsync(item => item.InvoiceId == invoiceId, cancellationToken);
	var compensation = await dbContext.CompensationRecords.FirstOrDefaultAsync(item => item.CompensationId == compensationId && item.InvoiceId == invoiceId, cancellationToken);
	if (invoice is null || compensation is null)
	{
		return Results.Problem(title: "补偿记录不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	compensation.Status = "Resolved";
	compensation.ResolutionNote = request.ResolutionNote;
	compensation.ResolvedAtUtc = DateTimeOffset.UtcNow;
	invoice.Status = string.IsNullOrWhiteSpace(request.RestoredInvoiceStatus) ? "Issued" : request.RestoredInvoiceStatus;
	invoice.NotificationStatus = "Recovered";
	invoice.UpdatedAtUtc = DateTimeOffset.UtcNow;
	await dbContext.SaveChangesAsync(cancellationToken);
	telemetry.RecordCompensationResolved(invoice.TenantId, invoice.Status);

	return Results.Ok(ToCompensationResponse(compensation));
}).RequireAuthorization();

app.MapGet("/api/billing/observability", async (BillingDbContext dbContext, CancellationToken cancellationToken) =>
{
	var utcNow = DateTimeOffset.UtcNow;
	var response = new BillingObservabilityResponse(
		PendingOutbox: await dbContext.OutboxMessages.CountAsync(item => item.DispatchedAtUtc == null && item.EventType == "InvoiceIssued", cancellationToken),
		ActionRequiredInvoices: await dbContext.Invoices.CountAsync(item => item.Status == "ActionRequired", cancellationToken),
		OpenCompensations: await dbContext.CompensationRecords.CountAsync(item => item.Status == "Open", cancellationToken),
		OverdueInvoices: await dbContext.Invoices.CountAsync(item => item.DueAtUtc < utcNow && item.Status != "Paid", cancellationToken),
		FailedNotificationInvoices: await dbContext.Invoices.CountAsync(item => item.NotificationStatus == "Failed", cancellationToken),
		GeneratedAtUtc: utcNow);

	return Results.Ok(response);
}).RequireAuthorization();

app.MapGet("/api/billing/summary", async (HttpContext context, BillingDbContext dbContext, CancellationToken cancellationToken) =>
{
	var tenantId = context.GetPlatformRequestContext()?.TenantId;
	if (string.IsNullOrWhiteSpace(tenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var utcNow = DateTimeOffset.UtcNow;
	var invoices = await dbContext.Invoices.Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken);
	var response = new AdminFinanceSummaryResponse(
		PendingReview: invoices.Count(item => item.Status is "Issued" or "Recovered"),
		Issued: invoices.Count(item => item.Status == "Issued"),
		Overdue: invoices.Count(item => item.DueAtUtc < utcNow && item.Status != "Paid"),
		PendingArchive: invoices.Count(item => item.NotificationStatus is "Delivered" or "Recovered"),
		ActionRequired: invoices.Count(item => item.Status == "ActionRequired"),
		FailedNotifications: invoices.Count(item => item.NotificationStatus == "Failed"),
		GeneratedAtUtc: utcNow);

	return Results.Ok(response);
}).RequireAuthorization();

app.MapPost("/api/billing/outbox/dispatch", async (HttpContext context, BillingDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var dispatched = await BillingOutboxNotificationDispatcher.DispatchPendingAsync(
		dbContext,
		httpClientFactory.CreateClient(),
		context,
		configuration,
		cancellationToken);
	var pending = await dbContext.OutboxMessages.CountAsync(item => item.DispatchedAtUtc == null && item.EventType == "InvoiceIssued", cancellationToken);

	return Results.Ok(new
	{
		dispatched,
		pending,
		service = "billing-service",
		utc = DateTimeOffset.UtcNow,
	});
}).RequireAuthorization();

app.Run();

static BillingInvoiceResponse ToInvoiceResponse(BillingInvoiceEntity invoice)
{
	return new BillingInvoiceResponse(
		InvoiceId: invoice.InvoiceId,
		TenantId: invoice.TenantId,
		ElderId: invoice.ElderId,
		ElderName: invoice.ElderName,
		PackageName: invoice.PackageName,
		Amount: invoice.Amount,
		DueAtUtc: invoice.DueAtUtc,
		Status: invoice.Status,
		NotificationStatus: invoice.NotificationStatus,
		CreatedAtUtc: invoice.CreatedAtUtc,
		UpdatedAtUtc: invoice.UpdatedAtUtc);
}

static BillingCompensationResponse ToCompensationResponse(BillingCompensationRecordEntity record)
{
	return new BillingCompensationResponse(
		CompensationId: record.CompensationId,
		InvoiceId: record.InvoiceId,
		NotificationId: record.NotificationId,
		Status: record.Status,
		FailureCode: record.FailureCode,
		FailureReason: record.FailureReason,
		CreatedAtUtc: record.CreatedAtUtc,
		ResolvedAtUtc: record.ResolvedAtUtc);
}

static bool IsCompensationCallerAuthorized(HttpContext context, IConfiguration configuration)
{
	if (context.User.Identity?.IsAuthenticated == true)
	{
		return true;
	}

	var configured = configuration["InternalServiceAuth:ApiKey"];
	var provided = context.Request.Headers[PlatformHeaderNames.InternalServiceKey].FirstOrDefault();
	return !string.IsNullOrWhiteSpace(configured)
		&& !string.IsNullOrWhiteSpace(provided)
		&& string.Equals(configured, provided, StringComparison.Ordinal);
}
