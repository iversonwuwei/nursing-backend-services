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
			$"{ResolveServiceUrl(configuration, "Identity", "http://localhost:5301")}/api/identity/me",
			cancellationToken);

		var tenant = await GetJsonAsync<TenantDescriptorResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Tenant", "http://localhost:5302")}/api/tenants/{requestContext.TenantId}",
			cancellationToken);

		var admission = await PostJsonAsync<AdmissionRecordResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5310")}/api/elders/admissions",
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
			$"{ResolveServiceUrl(configuration, "Health", "http://localhost:5312")}/api/health/archives/from-admission",
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
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5311")}/api/care/plans/from-admission",
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
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5317")}/api/notifications/dispatch",
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
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5317")}/api/notifications?audience=nani&audienceKey={admission.ElderId}",
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
