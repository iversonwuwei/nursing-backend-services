using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Networking;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "admin-bff",
	ServiceType: "bff",
	BoundedContext: "admin-edge",
	Consumers: ["admin-web"],
	Capabilities: ["dashboard-aggregation", "approval-views", "analytics-dto", "ai-operations-view"]));

app.MapPost("/api/admin/admissions/onboard", async (AdminAdmissionOnboardRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var client = httpClientFactory.CreateClient();

		var identity = await GetJsonAsync<IdentityContextResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Identity", "http://localhost:5265")}/api/identity/me",
			cancellationToken);

		var tenant = await GetJsonAsync<TenantDescriptorResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Tenant", "http://localhost:5186")}/api/tenants/{requestContext.TenantId}",
			cancellationToken);

		var admission = await PostJsonAsync<AdmissionRecordResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/admissions",
			new AdmissionCreateRequest(
				AdmissionReference: request.AdmissionReference,
				ElderName: request.ElderName,
				Age: request.Age,
				Gender: request.Gender,
				CareLevel: request.CareLevel,
				RoomNumber: request.RoomNumber,
				FamilyContactName: request.FamilyContactName,
				FamilyContactPhone: request.FamilyContactPhone,
				MedicalAlerts: request.MedicalAlerts),
			cancellationToken);

		var healthArchive = await PostJsonAsync<HealthArchiveSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Health", "http://localhost:5197")}/api/health/archives/from-admission",
			new HealthArchiveCreateFromAdmissionRequest(
				AdmissionId: admission.AdmissionId,
				ElderId: admission.ElderId,
				ElderName: admission.ElderName,
				CareLevel: admission.CareLevel,
				BloodPressure: request.BloodPressure,
				HeartRate: request.HeartRate,
				Temperature: request.Temperature,
				BloodSugar: request.BloodSugar,
				Oxygen: request.Oxygen,
				AlertSummary: request.AlertSummary),
			cancellationToken);

		var carePlan = await PostJsonAsync<CarePlanResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/plans/from-admission",
			new CarePlanCreateFromAdmissionRequest(
				AdmissionId: admission.AdmissionId,
				ElderId: admission.ElderId,
				ElderName: admission.ElderName,
				CareLevel: admission.CareLevel,
				RoomNumber: admission.RoomNumber),
			cancellationToken);

		var familyNotification = await PostJsonAsync<NotificationMessageResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications/dispatch",
			new NotificationDispatchRequest(
				Audience: "family",
				AudienceKey: admission.ElderId,
				Category: "admission",
				Title: $"{admission.ElderName} 已完成入住建档",
				Body: $"已生成健康档案与护理计划，房间 {admission.RoomNumber}，护理等级 {admission.CareLevel}。",
				SourceService: "admin-bff",
				SourceEntityId: admission.AdmissionId,
				CorrelationId: requestContext.CorrelationId),
			cancellationToken);

		var naniNotifications = await GetJsonAsync<IReadOnlyList<NotificationMessageResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications?audience=nani&audienceKey={admission.ElderId}",
			cancellationToken) ?? [];

		var notifications = new List<NotificationMessageResponse> { familyNotification };
		notifications.AddRange(naniNotifications);

		return Results.Ok(new AdminAdmissionOnboardResponse(
			Admission: admission,
			HealthArchive: healthArchive,
			CarePlan: carePlan,
			Notifications: notifications,
			Tenant: tenant,
			Operator: identity,
			CorrelationId: requestContext.CorrelationId));
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "入住纵切编排失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapGet("/api/admin/nursing/workflow-board", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<NursingWorkflowBoardResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/workflow-board",
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理工作流看板查询失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapGet("/api/admin/nursing/observability", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<CareWorkflowObservabilityResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/observability",
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理工作流观测查询失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapGet("/api/admin/nursing/audits", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, int? take, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var suffix = take is > 0 ? $"?take={take.Value}" : string.Empty;
		var response = await GetJsonAsync<IReadOnlyList<CareWorkflowAuditResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/audits{suffix}",
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理工作流审计查询失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/packages", async (CreateServicePackageRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePackageResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/packages",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "创建护理套餐失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/packages/{packageId}/actions/{action}", async (string packageId, string action, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePackageActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/packages/{packageId}/actions/{action}",
			new { },
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理套餐动作提交失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/packages/{packageId}/plans", async (string packageId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/packages/{packageId}/plans",
			new { },
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "从护理套餐生成计划失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/plans", async (CreateServicePlanRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/plans",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "创建护理计划失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/plans/{planId}/actions/{action}", async (string planId, string action, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/plans/{planId}/actions/{action}",
			new { },
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理计划动作提交失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/tasks/{taskId}/start", async (string taskId, ServicePlanTaskActionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/tasks/{taskId}/start",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理任务开始执行失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/tasks/{taskId}/complete", async (string taskId, ServicePlanTaskActionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/tasks/{taskId}/complete",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理任务完成失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPut("/api/admin/nursing/tasks/{taskId}/note", async (string taskId, SaveServicePlanTaskNoteRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<ServicePlanActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/tasks/{taskId}/note",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理任务备注保存失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.Run();

static string ResolveServiceUrl(IConfiguration configuration, string serviceName, string fallback)
{
	return configuration[$"ServiceEndpoints:{serviceName}"] ?? fallback;
}

static async Task<T> GetJsonAsync<T>(HttpClient client, HttpContext context, string url, CancellationToken cancellationToken)
{
	using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Get, url, context);
	using var response = await client.SendAsync(request, cancellationToken);
	response.EnsureSuccessStatusCode();
	return (await response.ReadJsonAsync<T>(cancellationToken))!;
}

static async Task<T> PostJsonAsync<T>(HttpClient client, HttpContext context, string url, object payload, CancellationToken cancellationToken)
{
	using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Post, url, context, payload);
	using var response = await client.SendAsync(request, cancellationToken);
	response.EnsureSuccessStatusCode();
	return (await response.ReadJsonAsync<T>(cancellationToken))!;
}

static async Task<T> PutJsonAsync<T>(HttpClient client, HttpContext context, string url, object payload, CancellationToken cancellationToken)
{
	using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Put, url, context, payload);
	using var response = await client.SendAsync(request, cancellationToken);
	response.EnsureSuccessStatusCode();
	return (await response.ReadJsonAsync<T>(cancellationToken))!;
}
