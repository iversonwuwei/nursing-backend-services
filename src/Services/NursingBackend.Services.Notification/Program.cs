using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Networking;
using NursingBackend.Services.Notification;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<NotificationDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing"));
builder.Services.AddSingleton<NotificationTelemetry>();
builder.Services.Configure<NotificationProviderCallbackOptions>(builder.Configuration.GetSection("ProviderCallbacks"));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "notification-service",
	ServiceType: "domain-service",
	BoundedContext: "communication",
	Consumers: ["admin-bff", "family-bff", "nani-bff", "all-domain-services"],
	Capabilities: ["in-app-messages", "sms-push-email", "template-management", "delivery-audit"]));

app.MapPost("/api/notifications/dispatch", async (HttpContext context, NotificationDispatchRequest request, NotificationDbContext dbContext, NotificationTelemetry telemetry) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	using var activity = telemetry.StartActivity("notification.dispatch",
		new KeyValuePair<string, object?>("tenant.id", requestContext.TenantId),
		new KeyValuePair<string, object?>("notification.source_service", request.SourceService),
		new KeyValuePair<string, object?>("notification.category", request.Category));

	var existing = await dbContext.Notifications.FirstOrDefaultAsync(item =>
		item.TenantId == requestContext.TenantId
		&& item.Audience == request.Audience
		&& item.AudienceKey == request.AudienceKey
		&& item.SourceService == request.SourceService
		&& item.SourceEntityId == request.SourceEntityId
		&& item.Category == request.Category);
	if (existing is not null)
	{
		return Results.Ok(ToResponse(existing));
	}

	var entity = new NotificationMessageEntity
	{
		NotificationId = $"NTF-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		Audience = request.Audience,
		AudienceKey = request.AudienceKey,
		Category = request.Category,
		Title = request.Title,
		Body = request.Body,
		SourceService = request.SourceService,
		SourceEntityId = request.SourceEntityId,
		CorrelationId = request.CorrelationId,
		Status = "Queued",
		CreatedAtUtc = DateTimeOffset.UtcNow,
	};

	dbContext.Notifications.Add(entity);
	await dbContext.SaveChangesAsync();
	telemetry.RecordDispatch(entity.SourceService, entity.Category);

	return Results.Ok(ToResponse(entity));
}).RequireAuthorization();

app.MapGet("/api/notifications", async (HttpContext context, string audience, string audienceKey, NotificationDbContext dbContext, CancellationToken cancellationToken) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var items = await dbContext.Notifications
		.Where(item => item.TenantId == tenantId && item.Audience == audience && item.AudienceKey == audienceKey)
		.OrderByDescending(item => item.CreatedAtUtc)
		.ToListAsync(cancellationToken);

	return Results.Ok(items.Select(ToResponse));
}).RequireAuthorization();

app.MapPost("/api/notifications/{notificationId}/delivery-result", async (string notificationId, HttpContext context, NotificationDeliveryResultRequest request, NotificationDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, NotificationTelemetry telemetry, CancellationToken cancellationToken) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var entity = await dbContext.Notifications.FirstOrDefaultAsync(item => item.NotificationId == notificationId && item.TenantId == tenantId, cancellationToken);
	if (entity is null)
	{
		return Results.Problem(title: $"通知 {notificationId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	var result = await ProcessDeliveryResultAsync(context, request, entity, dbContext, httpClientFactory, configuration, telemetry, cancellationToken, provider: null);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/provider-callbacks/notifications", async (HttpContext context, NotificationDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, IOptions<NotificationProviderCallbackOptions> callbackOptions, NotificationTelemetry telemetry, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
	context.Request.EnableBuffering();
	using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
	var rawBody = await reader.ReadToEndAsync(cancellationToken);
	context.Request.Body.Position = 0;

	var request = JsonSerializer.Deserialize<NotificationProviderCallbackRequest>(rawBody);
	if (request is null)
	{
		return Results.Problem(title: "provider callback body 无法解析。", statusCode: StatusCodes.Status400BadRequest);
	}

	var options = callbackOptions.Value;
	var profile = NotificationProviderWebhookPolicy.ResolveProfile(options, request.Provider);
	var providedSignature = context.Request.Headers[profile.SignatureHeaderName].FirstOrDefault()
		?? context.Request.Headers[PlatformHeaderNames.ProviderSignature].FirstOrDefault();
	var providedTimestamp = context.Request.Headers[profile.TimestampHeaderName].FirstOrDefault()
		?? context.Request.Headers[PlatformHeaderNames.ProviderTimestamp].FirstOrDefault();
	var providedKey = context.Request.Headers[options.SharedKeyHeaderName].FirstOrDefault()
		?? context.Request.Headers[PlatformHeaderNames.ProviderWebhookKey].FirstOrDefault();
	var signatureValid = NotificationProviderWebhookPolicy.IsSignatureValid(profile, providedSignature, providedTimestamp, rawBody, timeProvider);
	var sharedKeyValid = options.AllowSharedKeyFallback && NotificationProviderWebhookPolicy.IsAuthorized(options.SharedKey, providedKey);
	if (!signatureValid && !sharedKeyValid)
	{
		telemetry.RecordProviderSignatureFailure(request.Provider);
		return Results.Unauthorized();
	}

	var dedupeKey = NotificationProviderWebhookPolicy.BuildDedupeKey(request);
	var existingReceipt = await dbContext.ProviderCallbackReceipts.FirstOrDefaultAsync(item => item.DedupeKey == dedupeKey, cancellationToken);
	if (existingReceipt is not null)
	{
		telemetry.RecordProviderDuplicate(request.Provider);
		return Results.Ok(new
		{
			duplicate = true,
			receiptId = existingReceipt.ReceiptId,
			status = existingReceipt.Status,
			processedAtUtc = existingReceipt.ProcessedAtUtc,
		});
	}

	var receipt = new NotificationProviderCallbackReceiptEntity
	{
		ReceiptId = $"PCB-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
		Provider = request.Provider,
		DedupeKey = dedupeKey,
		ProviderMessageId = request.ProviderMessageId,
		NotificationId = request.NotificationId,
		CorrelationId = request.CorrelationId,
		Status = request.Status,
		SignatureStatus = signatureValid ? "SignatureValid" : "SharedKeyFallback",
		ReceivedAtUtc = DateTimeOffset.UtcNow,
	};
	await dbContext.ProviderCallbackReceipts.AddAsync(receipt, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);

	var entity = await ResolveNotificationAsync(request, dbContext, cancellationToken);
	if (entity is null)
	{
		receipt.Status = "NotificationNotFound";
		receipt.ProcessedAtUtc = DateTimeOffset.UtcNow;
		await dbContext.SaveChangesAsync(cancellationToken);
		return Results.Problem(title: "无法根据 provider callback 解析通知。", statusCode: StatusCodes.Status404NotFound);
	}

	telemetry.RecordProviderCallback(request.Provider, request.Status);
	var deliveryRequest = NotificationProviderWebhookPolicy.ToDeliveryResultRequest(request, profile);
	var result = await ProcessDeliveryResultAsync(context, deliveryRequest, entity, dbContext, httpClientFactory, configuration, telemetry, cancellationToken, provider: request.Provider);
	receipt.NotificationId = entity.NotificationId;
	receipt.CorrelationId = entity.CorrelationId;
	receipt.Status = "Processed";
	receipt.ProcessedAtUtc = DateTimeOffset.UtcNow;
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(result);
});

app.MapGet("/api/notifications/{notificationId}/attempts", async (string notificationId, HttpContext context, NotificationDbContext dbContext, CancellationToken cancellationToken) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var attempts = await dbContext.DeliveryAttempts
		.Where(item => item.TenantId == tenantId && item.NotificationId == notificationId)
		.OrderByDescending(item => item.AttemptedAtUtc)
		.ToListAsync(cancellationToken);

	return Results.Ok(attempts.Select(ToAttemptResponse));
}).RequireAuthorization();

app.MapGet("/api/notifications/observability", async (HttpContext context, NotificationDbContext dbContext, CancellationToken cancellationToken) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var response = new NotificationObservabilityResponse(
		Queued: await dbContext.Notifications.CountAsync(item => item.TenantId == tenantId && item.Status == "Queued", cancellationToken),
		Delivered: await dbContext.Notifications.CountAsync(item => item.TenantId == tenantId && item.Status == "Delivered", cancellationToken),
		Failed: await dbContext.Notifications.CountAsync(item => item.TenantId == tenantId && item.Status == "Failed", cancellationToken),
		CompensationRequested: await dbContext.DeliveryAttempts.CountAsync(item => item.TenantId == tenantId && item.CompensationStatus == "Requested", cancellationToken),
		CompensationFailed: await dbContext.DeliveryAttempts.CountAsync(item => item.TenantId == tenantId && item.CompensationStatus == "RequestFailed", cancellationToken),
		GeneratedAtUtc: DateTimeOffset.UtcNow);

	return Results.Ok(response);
}).RequireAuthorization();

app.Run();

static NotificationMessageResponse ToResponse(NotificationMessageEntity entity)
{
	return new NotificationMessageResponse(
		NotificationId: entity.NotificationId,
		TenantId: entity.TenantId,
		Audience: entity.Audience,
		AudienceKey: entity.AudienceKey,
		Category: entity.Category,
		Title: entity.Title,
		Body: entity.Body,
		SourceService: entity.SourceService,
		SourceEntityId: entity.SourceEntityId,
		CreatedAtUtc: entity.CreatedAtUtc,
		Status: entity.Status);
}

static NotificationDeliveryAttemptResponse ToAttemptResponse(NotificationDeliveryAttemptEntity entity)
{
	return new NotificationDeliveryAttemptResponse(
		DeliveryAttemptId: entity.DeliveryAttemptId,
		NotificationId: entity.NotificationId,
		Channel: entity.Channel,
		Status: entity.Status,
		FailureCode: entity.FailureCode,
		FailureReason: entity.FailureReason,
		CompensationStatus: entity.CompensationStatus,
		CompensationReferenceId: entity.CompensationReferenceId,
		AttemptedAtUtc: entity.AttemptedAtUtc);
}

static string? GetTenantId(HttpContext context)
{
	var requestContext = context.GetPlatformRequestContext();
	return string.IsNullOrWhiteSpace(requestContext?.TenantId) ? null : requestContext.TenantId;
}

static async Task<NotificationMessageEntity?> ResolveNotificationAsync(NotificationProviderCallbackRequest request, NotificationDbContext dbContext, CancellationToken cancellationToken)
{
	if (!string.IsNullOrWhiteSpace(request.NotificationId))
	{
		return await dbContext.Notifications.FirstOrDefaultAsync(item => item.NotificationId == request.NotificationId, cancellationToken);
	}

	if (!string.IsNullOrWhiteSpace(request.CorrelationId) && !string.IsNullOrWhiteSpace(request.SourceService) && !string.IsNullOrWhiteSpace(request.SourceEntityId))
	{
		return await dbContext.Notifications.FirstOrDefaultAsync(
			item => item.CorrelationId == request.CorrelationId && item.SourceService == request.SourceService && item.SourceEntityId == request.SourceEntityId,
			cancellationToken);
	}

	return null;
}

static async Task<object> ProcessDeliveryResultAsync(HttpContext context, NotificationDeliveryResultRequest request, NotificationMessageEntity entity, NotificationDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, NotificationTelemetry telemetry, CancellationToken cancellationToken, string? provider)
{
	var normalizedStatus = string.IsNullOrWhiteSpace(request.Status) ? "Delivered" : request.Status.Trim();
	var normalizedChannel = string.IsNullOrWhiteSpace(request.Channel) ? "in-app" : request.Channel.Trim();

	using var activity = telemetry.StartActivity("notification.delivery_result",
		new KeyValuePair<string, object?>("tenant.id", entity.TenantId),
		new KeyValuePair<string, object?>("notification.id", entity.NotificationId),
		new KeyValuePair<string, object?>("notification.status", normalizedStatus),
		new KeyValuePair<string, object?>("notification.channel", normalizedChannel),
		new KeyValuePair<string, object?>("provider.name", provider));

	entity.Status = normalizedStatus;

	var attempt = new NotificationDeliveryAttemptEntity
	{
		DeliveryAttemptId = $"ATT-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
		NotificationId = entity.NotificationId,
		TenantId = entity.TenantId,
		SourceService = entity.SourceService,
		SourceEntityId = entity.SourceEntityId,
		CorrelationId = entity.CorrelationId,
		Channel = normalizedChannel,
		Status = normalizedStatus,
		FailureCode = request.FailureCode,
		FailureReason = request.FailureReason,
		CompensationStatus = "NotRequired",
		AttemptedAtUtc = DateTimeOffset.UtcNow,
	};

	dbContext.DeliveryAttempts.Add(attempt);
	await dbContext.SaveChangesAsync(cancellationToken);
	telemetry.RecordDelivery(normalizedStatus, normalizedChannel, entity.SourceService);

	if (BillingNotificationCompensationPolicy.ShouldRequest(entity, normalizedStatus))
	{
		var compensation = await RequestBillingCompensationAsync(httpClientFactory.CreateClient(), configuration, entity, request, cancellationToken);
		attempt.CompensationStatus = compensation.Succeeded ? "Requested" : "RequestFailed";
		attempt.CompensationReferenceId = compensation.CompensationId;
		await dbContext.SaveChangesAsync(cancellationToken);
		telemetry.RecordCompensationRequest(compensation.Succeeded, entity.SourceService);
	}

	return new
	{
		notification = ToResponse(entity),
		attempt = ToAttemptResponse(attempt),
	};
}

static async Task<(bool Succeeded, string? CompensationId)> RequestBillingCompensationAsync(HttpClient client, IConfiguration configuration, NotificationMessageEntity entity, NotificationDeliveryResultRequest request, CancellationToken cancellationToken)
{
	var compensationRequest = BillingNotificationCompensationPolicy.BuildRequest(entity, request);
	var billingUrl = configuration["ServiceEndpoints:Billing"] ?? "http://localhost:5253";
	using var downstream = new HttpRequestMessage(HttpMethod.Post, $"{billingUrl}/api/billing/invoices/{entity.SourceEntityId}/notifications/compensate")
	{
		Content = JsonContent.Create(compensationRequest),
	};
	downstream.Headers.TryAddWithoutValidation(PlatformHeaderNames.TenantId, entity.TenantId);
	downstream.Headers.TryAddWithoutValidation(PlatformHeaderNames.CorrelationId, entity.CorrelationId);
	var internalServiceKey = configuration["InternalServiceAuth:ApiKey"];
	if (!string.IsNullOrWhiteSpace(internalServiceKey))
	{
		downstream.Headers.TryAddWithoutValidation(PlatformHeaderNames.InternalServiceKey, internalServiceKey);
	}
	using var response = await client.SendAsync(downstream, cancellationToken);
	if (!response.IsSuccessStatusCode)
	{
		return (false, null);
	}

	var payload = await response.ReadJsonAsync<BillingCompensationResponse>(cancellationToken);
	return (true, payload?.CompensationId);
}
