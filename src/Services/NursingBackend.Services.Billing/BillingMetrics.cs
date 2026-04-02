using System.Diagnostics.Metrics;

namespace NursingBackend.Services.Billing;

public sealed class BillingMetrics
{
	private readonly Meter meter = new("NursingBackend.Services.Billing", "1.0.0");
	private readonly Counter<long> invoicesIssuedCounter;
	private readonly Counter<long> compensationsCreatedCounter;
	private readonly Counter<long> compensationsResolvedCounter;

	public BillingMetrics()
	{
		invoicesIssuedCounter = meter.CreateCounter<long>("nursing_billing_invoices_issued");
		compensationsCreatedCounter = meter.CreateCounter<long>("nursing_billing_compensations_created");
		compensationsResolvedCounter = meter.CreateCounter<long>("nursing_billing_compensations_resolved");
	}

	public void RecordInvoiceIssued() => invoicesIssuedCounter.Add(1);
	public void RecordCompensationCreated() => compensationsCreatedCounter.Add(1);
	public void RecordCompensationResolved() => compensationsResolvedCounter.Add(1);
}