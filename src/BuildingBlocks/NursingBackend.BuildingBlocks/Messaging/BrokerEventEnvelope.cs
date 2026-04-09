namespace NursingBackend.BuildingBlocks.Messaging;

public sealed record BrokerEventEnvelope(
	string SourceService,
	string TenantId,
	string AggregateType,
	string AggregateId,
	string EventType,
	string PayloadJson,
	string CorrelationId,
	DateTimeOffset OccurredAtUtc);

public static class BrokerTopology
{
	public const string DomainEventsExchange = "nursing.domain.events";
	public const string DomainEventsQueue = "nursing.notification.events";
	public const string AiEventsQueue = "nursing.ai.events";
	public const string AiEventsRetryQueue = "nursing.ai.events.retry";
	public const string AiEventsDeadLetterQueue = "nursing.ai.events.dead";
	public const string CarePlanRoutingKey = "care.CarePlanGenerated";
	public const string VisitRequestedRoutingKey = "visit.VisitRequested";
	public const string InvoiceIssuedRoutingKey = "billing.InvoiceIssued";
	public const string VitalRecordedRoutingKey = "health.VitalRecorded";
	public const string HealthRiskDetectedRoutingKey = "health.HealthRiskDetected";
	public const string ElderAdmittedRoutingKey = "elder.ElderAdmitted";
	public const string AlertRaisedRoutingKey = "operations.AlertRaised";
	public const string ShiftAssignedRoutingKey = "staffing.ShiftAssigned";
}