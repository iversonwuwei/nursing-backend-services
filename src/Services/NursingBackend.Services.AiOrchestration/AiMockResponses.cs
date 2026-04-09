using NursingBackend.BuildingBlocks.Contracts;

namespace NursingBackend.Services.AiOrchestration;

internal static class AiMockResponses
{
	public static T Create<T>(string endpoint, object input)
	{
		object response = endpoint switch
		{
			"/api/ai/dashboard-insights" when input is AiDashboardInsightsRequest request => new AiDashboardInsightsResponse(
				Summary: $"当前共有 {request.TotalElders} 位老人，活跃护理计划 {request.ActiveCarePlans} 个，建议优先处理 {request.OpenAlerts} 条报警与 {request.PendingTasks} 个待办。",
				KeyInsights: [
					$"入住率 {request.OccupancyPercent}% ，床位利用已进入重点观察区间。",
					$"待处理报警 {request.OpenAlerts} 条，需优先核查高风险对象。"
				],
				ActionItems: [
					"先处理高风险报警，再复核跨班次未完成任务。",
					"补齐今日运营备注，便于交接班与审计复盘。"
				]),
			"/api/ai/health-risk" when input is AiHealthRiskRequest request => new AiHealthRiskResponse(
				RiskLevel: request.Oxygen < 92 || request.Temperature >= 38 ? "高风险" : "中风险",
				Explanation: $"{request.ElderName} 当前生命体征存在波动，需结合既往病史与当前用药持续观察。",
				Recommendations: ["30 分钟内复测生命体征", "通知责任护士复核当前护理动作"],
				MonitoringPoints: ["血氧趋势", "体温变化", "主诉与精神状态"]),
			"/api/ai/alert-suggestion" when input is AiAlertSuggestionRequest request => new AiAlertSuggestionResponse(
				SuggestedAction: "先完成现场复核，再决定是否升级通知护士长。",
				Rationale: $"报警类型为 {request.AlertType}，当前严重度 {request.Severity}，需要把对象状态与近 2 小时记录结合判断。",
				Priority: request.Severity,
				Steps: ["确认对象当前状态", "核对最近处置记录", "必要时升级上报"]),
			"/api/ai/task-priority" when input is AiTaskPriorityRequest request => new AiTaskPriorityResponse(
				RankedTasks: request.Tasks
					.Select((task, index) => new AiTaskPriorityRankedItem(
						TaskId: task.TaskId,
						Rank: index + 1,
						Priority: index == 0 ? "高" : index == 1 ? "中" : "常规",
						Reason: index == 0 ? "临近执行时点且对象护理等级较高。" : "保持当前顺序即可。"))
					.ToList(),
				Rationale: "优先级按护理等级、到期时间与当前状态综合排序。"),
			"/api/ai/admission-assessment" when input is AiAdmissionAssessmentRequest request => new AiAdmissionAssessmentResponse(
				RecommendedCareLevel: request.RequestedCareLevel,
				AssessmentSummary: $"{request.ElderName} 当前申请等级与现有风险提示基本匹配，建议进入人工复核。",
				RiskFactors: request.MedicalAlerts.Count > 0 ? request.MedicalAlerts : ["需人工复核既往病史"],
				Recommendations: ["补齐家属备注", "确认认定结论后再生成护理计划"]),
			"/api/ai/ops-report" when input is AiOpsReportRequest request => new AiOpsReportResponse(
				ReportTitle: $"{request.Period} {request.ReportType} 报告",
				Summary: "本周期整体运营平稳，但报警响应与排班覆盖仍需持续观察。",
				Highlights: ["重点对象跟进较及时", "护理计划闭环率维持稳定"],
				Concerns: ["跨班次待办仍有积压"],
				Recommendations: ["优化晚班交接", "补强高风险对象晨间巡查"]),
			"/api/ai/financial-insights" when input is AiResourceInsightsRequest request => new AiResourceInsightsResponse(
				Summary: $"{request.ResourceType} 当前可生成管理摘要。",
				Insights: ["账单与费用结构可按周期输出摘要。"],
				Suggestions: ["优先检查逾期与异常波动项。"]),
			"/api/ai/device-insights" when input is AiAlertSuggestionRequest request => new AiAlertSuggestionResponse(
				SuggestedAction: "建议先做设备连通性与电量复核。",
				Rationale: request.AlertDescription,
				Priority: request.Severity,
				Steps: ["检查在线状态", "确认告警持续时间", "必要时安排人工巡检"]),
			"/api/ai/incident-analysis" when input is AiAlertSuggestionRequest request => new AiAlertSuggestionResponse(
				SuggestedAction: "先恢复现场事实，再整理复盘结论。",
				Rationale: request.AlertDescription,
				Priority: request.Severity,
				Steps: ["确认事件时间线", "识别处置缺口", "补齐复盘纪要"]),
			"/api/ai/resource-insights" when input is AiResourceInsightsRequest request => new AiResourceInsightsResponse(
				Summary: $"{request.ResourceType} 资源当前处于可监控状态。",
				Insights: ["现有数据足以形成运营摘要。"],
				Suggestions: ["把异常对象与短缺项前置展示。"]),
			"/api/ai/elder-detail-action" when input is AiAlertSuggestionRequest request => new AiAlertSuggestionResponse(
				SuggestedAction: "建议先回看老人近 24 小时记录，再决定是否升级跟进。",
				Rationale: request.AlertDescription,
				Priority: request.Severity,
				Steps: ["查看健康摘要", "查看任务完成情况", "记录人工确认结果"]),
			"/api/ai/chat" when input is AiChatRequest request => new AiChatResponse(
				Reply: $"已收到问题：{request.Message}。当前为本地联调模式，返回确定性摘要结果用于打通前后端链路。",
				ConversationId: request.ConversationId ?? $"mock-admin-{Guid.NewGuid():N}"),
			"/api/ai/family-chat" when input is AiChatRequest request => new AiChatResponse(
				Reply: $"已收到家属问题：{request.Message}。当前返回本地联调说明性回答。",
				ConversationId: request.ConversationId ?? $"mock-family-{Guid.NewGuid():N}"),
			"/api/ai/shift-summary" when input is AiShiftSummaryRequest request => new AiShiftSummaryResponse(
				Summary: $"{request.Shift} 已完成 {request.CompletedTasks} 项任务，剩余 {request.PendingTasks} 项待办，当前有 {request.Alerts} 条报警需关注。",
				KeyPoints: ["优先核查高风险对象", "交接班前确认未闭环任务"],
				HandoverItems: ["补齐未完成任务说明", "同步报警处置进展"]),
			"/api/ai/care-copilot" when input is AiAlertSuggestionRequest request => new AiAlertSuggestionResponse(
				SuggestedAction: "建议按照任务单先核对对象状态，再执行护理动作。",
				Rationale: request.AlertDescription,
				Priority: request.Severity,
				Steps: ["确认对象身份", "检查执行前条件", "完成后补记录"]),
			"/api/ai/handover-draft" when input is AiHandoverDraftRequest request => new AiHandoverDraftResponse(
				Draft: $"{request.FromShift} 向 {request.ToShift} 交接：已完成 {request.CompletedItems.Count} 项，待跟进 {request.PendingItems.Count} 项，重点关注 {request.Alerts.Count} 个异常对象。",
				CriticalItems: request.PendingItems.Take(3).ToList()),
			"/api/ai/escalation-draft" when input is AiEscalationDraftRequest request => new AiEscalationDraftResponse(
				Draft: $"请关注 {request.ElderName} 的 {request.AlertType} 事项，当前状态为 {request.CurrentStatus}，建议立即复核。",
				SuggestedRecipient: "护士长",
				Priority: "高"),
			"/api/ai/today-summary" when input is AiFamilyTodaySummaryRequest request => new AiFamilyTodaySummaryResponse(
				Summary: $"{request.ElderName} 今日整体状态稳定，已完成 {request.CompletedTasks} 项护理，仍有 {request.PendingTasks} 项后续观察。",
				FrequentQuestions: [
					new AiFamilyQaItem("今天状态怎么样？", "整体平稳，护理动作已按计划推进。"),
					new AiFamilyQaItem("还需要特别注意什么？", "建议继续关注近期健康摘要中的重点项。")
				]),
			"/api/ai/health-explain" when input is AiHealthExplainRequest request => new AiHealthExplainResponse(
				Explanation: $"{request.MetricName} 当前值为 {request.MetricValue}，与正常范围 {request.NormalRange} 对比需要继续观察。",
				Recommendation: "建议家属结合护理团队说明，重点关注趋势而非单点数值。"),
			"/api/ai/visit-assistant" when input is AiVisitAssistantRequest request => new AiVisitAssistantResponse(
				SuggestedTimeSlots: request.PreferredTimeSlots?.Take(2).ToList() ?? ["19:30", "周末上午"],
				VisitTips: ["探视前先确认老人休息状态", "如近期指标波动，优先选择安静时段"],
				Recommendation: "建议选择护理动作较少的时段，以提升探视体验。"),
			"/api/ai/visit-risk" when input is AiVisitRiskRequest request => new AiVisitRiskResponse(
				RiskLevel: "中",
				Analysis: $"{request.TimeSlot} 可安排探视，但需结合 {request.CurrentHealthStatus ?? "当前护理安排"} 做最终确认。",
				Precautions: ["探视前确认身体状态", "避免与治疗时段冲突"]),
			_ => throw new InvalidOperationException($"No mock AI response is configured for endpoint '{endpoint}'.")
		};

		return (T)response;
	}
}