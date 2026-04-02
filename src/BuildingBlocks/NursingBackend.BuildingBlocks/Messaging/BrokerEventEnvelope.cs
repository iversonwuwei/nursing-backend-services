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
	public const string CarePlanRoutingKey = "care.CarePlanGenerated";
	public const string VisitRequestedRoutingKey = "visit.VisitRequested";
	public const string InvoiceIssuedRoutingKey = "billing.InvoiceIssued";
}