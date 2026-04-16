namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record AdminDashboardKpiResponse(
	int ElderCount,
	int TenantCount,
	int PendingAlerts,
	int WorkflowPendingCount);

public sealed record AdminDashboardMetricItemResponse(
	string Label,
	int Value);

public sealed record AdminDashboardAlertModuleBreakdownResponse(
	string Label,
	int Pending,
	int Processing,
	int Resolved,
	int Critical,
	int TotalOpen);

public sealed record AdminDashboardStaffLeaderboardItemResponse(
	string Name,
	string Role,
	int Tasks,
	int Completed,
	int CompletionRate,
	string Trend);

public sealed record AdminDashboardOverviewResponse(
	DateTimeOffset GeneratedAtUtc,
	AdminDashboardKpiResponse Kpis,
	IReadOnlyList<AdminDashboardAlertModuleBreakdownResponse> AlertModules,
	IReadOnlyList<AdminDashboardMetricItemResponse> NotificationBreakdown,
	IReadOnlyList<AdminDashboardMetricItemResponse> FinanceBreakdown,
	IReadOnlyList<AdminDashboardMetricItemResponse> WorkflowBreakdown,
	IReadOnlyList<AdminDashboardStaffLeaderboardItemResponse> StaffLeaderboard);