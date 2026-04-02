using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NursingBackend.Services.Billing;

public sealed class BillingTelemetry
{
	private static readonly Meter Meter = new("NursingBackend.Services.Billing", "1.0.0");
	private static readonly ActivitySource ActivitySource = new("NursingBackend.Services.Billing");
	private readonly Counter<long> invoiceIssuedCounter = Meter.CreateCounter<long>("nursing_billing_invoices_issued_total");
	private readonly Counter<long> compensationCreatedCounter = Meter.CreateCounter<long>("nursing_billing_compensations_created_total");
	private readonly Counter<long> compensationResolvedCounter = Meter.CreateCounter<long>("nursing_billing_compensations_resolved_total");

	public Activity? StartActivity(string name, params KeyValuePair<string, object?>[] tags)
	{
		var activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
		if (activity is null)
		{
			return null;
		}

		foreach (var tag in tags)
		{
			activity.SetTag(tag.Key, tag.Value);
		}

		return activity;
	}

	public void RecordInvoiceIssued(string tenantId, string packageName)
	{
		invoiceIssuedCounter.Add(1,
			new KeyValuePair<string, object?>("tenant.id", tenantId),
			new KeyValuePair<string, object?>("billing.package", packageName));
	}

	public void RecordCompensationCreated(string tenantId, string failureCode)
	{
		compensationCreatedCounter.Add(1,
			new KeyValuePair<string, object?>("tenant.id", tenantId),
			new KeyValuePair<string, object?>("failure.code", failureCode));
	}

	public void RecordCompensationResolved(string tenantId, string restoredStatus)
	{
		compensationResolvedCounter.Add(1,
			new KeyValuePair<string, object?>("tenant.id", tenantId),
			new KeyValuePair<string, object?>("invoice.status", restoredStatus));
	}
}