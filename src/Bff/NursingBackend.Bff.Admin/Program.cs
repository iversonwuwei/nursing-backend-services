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

// ── Content Management Proxy (Config Service) ──────────────────────────────

app.MapGet("/api/admin/static-texts", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? ns, string? locale, string? keyword, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(ns)) qs += $"&ns={Uri.EscapeDataString(ns)}";
		if (!string.IsNullOrWhiteSpace(locale)) qs += $"&locale={Uri.EscapeDataString(locale)}";
		if (!string.IsNullOrWhiteSpace(keyword)) qs += $"&keyword={Uri.EscapeDataString(keyword)}";
		var response = await GetJsonAsync<StaticTextListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/static-texts/{id}", async (string id, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<StaticTextResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts/{id}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/static-texts", async (StaticTextCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<StaticTextResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPut("/api/admin/static-texts/{id}", async (string id, StaticTextUpdateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<StaticTextResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts/{id}", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本更新失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapDelete("/api/admin/static-texts/{id}", async (string id, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Delete, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts/{id}", context);
		using var resp = await client.SendAsync(req, cancellationToken);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(new { success = true });
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本删除失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/static-texts/namespaces", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<string>>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts/namespaces", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "命名空间查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/option-groups", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? status, string? keyword) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = "";
		if (!string.IsNullOrWhiteSpace(status)) qs += $"?status={Uri.EscapeDataString(status)}";
		if (!string.IsNullOrWhiteSpace(keyword)) qs += $"{(qs.Length > 0 ? "&" : "?")}keyword={Uri.EscapeDataString(keyword)}";
		var response = await GetJsonAsync<OptionGroupListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项分组查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/option-groups", async (OptionGroupCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<OptionGroupResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项分组创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPut("/api/admin/option-groups/{id}", async (string id, OptionGroupUpdateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<OptionGroupResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{id}", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项分组更新失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapDelete("/api/admin/option-groups/{id}", async (string id, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Delete, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{id}", context);
		using var resp = await client.SendAsync(req, cancellationToken);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(new { success = true });
	}
	catch (Exception ex) { return Results.Problem(title: "选项分组删除失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/option-groups/{groupId}/items", async (string groupId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<OptionItemResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/option-groups/{groupId}/items", async (string groupId, OptionItemCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<OptionItemResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/audit-logs", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? resourceType, string? operatorId, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(resourceType)) qs += $"&resourceType={Uri.EscapeDataString(resourceType)}";
		if (!string.IsNullOrWhiteSpace(operatorId)) qs += $"&operatorId={Uri.EscapeDataString(operatorId)}";
		var response = await GetJsonAsync<ContentAuditLogListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/audit-logs{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "操作日志查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Option Item CRUD Proxy (Config Service) ────────────────────────────────

app.MapPut("/api/admin/option-groups/{groupId}/items/{itemId}", async (string groupId, string itemId, OptionItemUpdateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<OptionItemResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items/{itemId}", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项更新失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapDelete("/api/admin/option-groups/{groupId}/items/{itemId}", async (string groupId, string itemId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Delete, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items/{itemId}", context);
		using var resp = await client.SendAsync(req, cancellationToken);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(new { success = true });
	}
	catch (Exception ex) { return Results.Problem(title: "选项删除失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPatch("/api/admin/option-groups/{groupId}/items/{itemId}/toggle", async (string groupId, string itemId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Patch, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items/{itemId}/toggle", context);
		using var resp = await client.SendAsync(req, cancellationToken);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(await resp.ReadJsonAsync<OptionItemResponse>(cancellationToken));
	}
	catch (Exception ex) { return Results.Problem(title: "选项状态切换失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPut("/api/admin/option-groups/{groupId}/items/reorder", async (string groupId, OptionItemReorderRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<List<OptionItemResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items/reorder", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项排序失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/audit-logs/{resourceType}/{resourceId}", async (string resourceType, string resourceId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<ContentAuditLogListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/audit-logs/{Uri.EscapeDataString(resourceType)}/{Uri.EscapeDataString(resourceId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "资源审计日志查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Elder Service Proxy ────────────────────────────────────────────────────

app.MapGet("/api/admin/elders", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? name, string? status, string? careLevel, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(name)) qs += $"&name={Uri.EscapeDataString(name)}";
		if (!string.IsNullOrWhiteSpace(status)) qs += $"&status={Uri.EscapeDataString(status)}";
		if (!string.IsNullOrWhiteSpace(careLevel)) qs += $"&careLevel={Uri.EscapeDataString(careLevel)}";
		var response = await GetJsonAsync<ElderListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "老人列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/elders/{elderId}", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<ElderProfileSummaryResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{Uri.EscapeDataString(elderId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "老人详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Health Service Proxy ───────────────────────────────────────────────────

app.MapGet("/api/admin/elders/{elderId}/health-summary", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<HealthArchiveSummaryResponse>(client, context, $"{ResolveServiceUrl(configuration, "Health", "http://localhost:5197")}/api/health/elders/{Uri.EscapeDataString(elderId)}/summary", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "健康摘要查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Billing Service Proxy ──────────────────────────────────────────────────

app.MapGet("/api/admin/elders/{elderId}/invoices", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<BillingInvoiceResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/elders/{Uri.EscapeDataString(elderId)}/invoices", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "账单列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/invoices/{invoiceId}", async (string invoiceId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<BillingInvoiceResponse>(client, context, $"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/invoices/{Uri.EscapeDataString(invoiceId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "账单详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/billing/observability", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<BillingObservabilityResponse>(client, context, $"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/observability", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "账单可观测数据查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Visit Service Proxy ────────────────────────────────────────────────────

app.MapGet("/api/admin/elders/{elderId}/appointments", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<VisitAppointmentResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Visit", "http://localhost:5050")}/api/visits/elders/{Uri.EscapeDataString(elderId)}/appointments", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "探访预约查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Notification Service Proxy ─────────────────────────────────────────────

app.MapGet("/api/admin/notifications", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? audience, string? audienceKey) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = "";
		if (!string.IsNullOrWhiteSpace(audience)) qs += $"?audience={Uri.EscapeDataString(audience)}";
		if (!string.IsNullOrWhiteSpace(audienceKey)) qs += $"{(qs.Length > 0 ? "&" : "?")}audienceKey={Uri.EscapeDataString(audienceKey)}";
		var response = await GetJsonAsync<List<NotificationMessageResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "通知列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/notifications/observability", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<NotificationObservabilityResponse>(client, context, $"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications/observability", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "通知可观测数据查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Tenant Service Proxy ───────────────────────────────────────────────────

app.MapGet("/api/admin/tenants", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<TenantDescriptorResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Tenant", "http://localhost:5186")}/api/tenants", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "租户列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/tenants/{tenantId}", async (string tenantId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<TenantDescriptorResponse>(client, context, $"{ResolveServiceUrl(configuration, "Tenant", "http://localhost:5186")}/api/tenants/{Uri.EscapeDataString(tenantId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "租户详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── AI Orchestration Proxy ─────────────────────────────────────────────────

app.MapPost("/api/admin/ai/dashboard-insights", async (AiDashboardInsightsRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/dashboard-insights", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI Dashboard 分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/health-risk", async (AiHealthRiskRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/health-risk", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 健康风险分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/alert-suggestion", async (AiAlertSuggestionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/alert-suggestion", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 报警建议失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/task-priority", async (AiTaskPriorityRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/task-priority", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 任务优先级排序失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/admission-assessment", async (AiAdmissionAssessmentRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/admission-assessment", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 入住评估失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/ops-report", async (AiOpsReportRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/ops-report", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 运营报告生成失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/financial-insights", async (AiResourceInsightsRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/financial-insights", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 财务分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/device-insights", async (AiAlertSuggestionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/device-insights", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 设备分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/incident-analysis", async (AiAlertSuggestionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/incident-analysis", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 事故分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/resource-insights", async (AiResourceInsightsRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/resource-insights", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 资源分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/chat", async (AiChatRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/chat", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 问答失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/elder-detail-action", async (AiAlertSuggestionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/elder-detail-action", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 老人详情分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/ai/rules", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/rules", ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 规则查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPatch("/api/admin/ai/rules/{ruleId}/toggle", async (string ruleId, AiRuleToggleRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Patch, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/rules/{ruleId}/toggle", context, request);
		using var resp = await client.SendAsync(req, ct);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(await resp.ReadJsonAsync<object>(ct));
	}
	catch (Exception ex) { return Results.Problem(title: "AI 规则切换失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/ai/models/status", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/models/status", ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 模型状态查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/ai/audit-logs", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct, string? capability, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(capability)) qs += $"&capability={Uri.EscapeDataString(capability)}";
		var response = await GetJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/audit-logs{qs}", ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 审计日志查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/ai/audit-logs/{auditId}", async (string auditId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/audit-logs/{Uri.EscapeDataString(auditId)}", ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 审计详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
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
