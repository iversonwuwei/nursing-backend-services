using Microsoft.Extensions.Options;
using NursingBackend.BuildingBlocks.Auth;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "identity-service",
	ServiceType: "domain-service",
	BoundedContext: "identity-and-access",
	Consumers: ["api-gateway", "admin-bff", "family-bff", "nani-bff"],
	Capabilities: ["login", "token-issuance", "role-resolution", "device-session"]));

app.MapPost("/api/identity/dev-login", (DevLoginRequest request, IOptions<PlatformJwtOptions> jwtOptions) =>
{
	if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.UserName))
	{
		return Results.Problem(title: "tenantId、userId 和 userName 为必填项。", statusCode: StatusCodes.Status400BadRequest);
	}

	var token = new PlatformAccessToken(
		TenantId: request.TenantId,
		UserId: request.UserId,
		UserName: request.UserName,
		Roles: request.Roles,
		Scopes: request.Scopes,
		ExpiresAtUtc: DateTimeOffset.UtcNow.AddHours(8));

	return Results.Ok(new DevLoginResponse(
		AccessToken: PlatformJwtExtensions.CreateAccessToken(token, jwtOptions.Value),
		TokenType: "Bearer",
		ExpiresAtUtc: token.ExpiresAtUtc,
		TenantId: token.TenantId,
		UserId: token.UserId,
		UserName: token.UserName,
		Roles: token.Roles,
		Scopes: token.Scopes));
}).AllowAnonymous();

app.MapGet("/api/identity/me", (HttpContext context) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Unauthorized();
	}

	return Results.Ok(new IdentityContextResponse(
		TenantId: requestContext.TenantId,
		UserId: requestContext.UserId,
		UserName: requestContext.UserName,
		Roles: requestContext.Roles,
		Scopes: requestContext.Scopes,
		CorrelationId: requestContext.CorrelationId));
}).RequireAuthorization();

app.MapGet("/api/identity/roles", () => Results.Ok(PlatformRoleCatalog.DefaultRoles));

app.Run();

internal static class PlatformRoleCatalog
{
	public static readonly IReadOnlyList<AdminRoleDescriptorResponse> DefaultRoles = new List<AdminRoleDescriptorResponse>
	{
		new(
			Id: "system-admin",
			Name: "系统管理员",
			Description: "负责平台级配置、角色授权与系统规则维护。",
			Scope: "全院区",
			Abilities: new[] { "配置管理", "角色授权", "系统规则", "审计日志" },
			IsHighRisk: true),
		new(
			Id: "care-supervisor",
			Name: "护理主管",
			Description: "管理所属院区的排班、护理计划复核与异常处理。",
			Scope: "所属院区 / 所属楼层",
			Abilities: new[] { "排班管理", "计划复核", "异常处理", "质量巡检" },
			IsHighRisk: true),
		new(
			Id: "caregiver",
			Name: "护理员",
			Description: "执行护理任务、服务打卡与交接班。",
			Scope: "所属班组",
			Abilities: new[] { "任务执行", "服务打卡", "交接班", "家属反馈" },
			IsHighRisk: false),
		new(
			Id: "nurse",
			Name: "护士",
			Description: "负责生命体征监测、用药执行与临床观察。",
			Scope: "所属院区",
			Abilities: new[] { "体征监测", "用药执行", "临床观察", "告警处置" },
			IsHighRisk: false),
		new(
			Id: "doctor",
			Name: "医生",
			Description: "诊疗、医嘱下达与健康评估。",
			Scope: "所属院区",
			Abilities: new[] { "医嘱下达", "健康评估", "诊疗复诊", "病历书写" },
			IsHighRisk: true),
		new(
			Id: "reception",
			Name: "前台 / 入住接待",
			Description: "接待入住咨询、预约与家属来访登记。",
			Scope: "所属院区",
			Abilities: new[] { "入住咨询", "预约登记", "家属接待", "合约资料" },
			IsHighRisk: false),
	};
}
