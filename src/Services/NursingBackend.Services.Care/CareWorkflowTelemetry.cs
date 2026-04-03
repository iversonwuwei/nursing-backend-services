using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NursingBackend.Services.Care;

public sealed class CareWorkflowTelemetry
{
    private static readonly Meter Meter = new("NursingBackend.Services.Care", "1.0.0");
    private static readonly ActivitySource ActivitySource = new("NursingBackend.Services.Care");

    private readonly Counter<long> taskCompletionCounter = Meter.CreateCounter<long>("nursing_care_workflow_task_completed_total");
    private readonly Counter<long> planArchiveCounter = Meter.CreateCounter<long>("nursing_care_workflow_plan_archived_total");
    private long unassignedBacklog;

    public CareWorkflowTelemetry()
    {
        Meter.CreateObservableGauge<long>(
            "nursing_care_workflow_unassigned_backlog",
            () => new Measurement<long>(Interlocked.Read(ref unassignedBacklog)));
    }

    public long TaskCompletionTotal { get; private set; }

    public long PlanArchiveTotal { get; private set; }

    public long UnassignedBacklogGauge => Interlocked.Read(ref unassignedBacklog);

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

    public void RecordTaskCompleted(string tenantId, string planId)
    {
        taskCompletionCounter.Add(1,
            new KeyValuePair<string, object?>("tenant.id", tenantId),
            new KeyValuePair<string, object?>("plan.id", planId));
        TaskCompletionTotal += 1;
    }

    public void RecordPlanArchived(string tenantId, string planId)
    {
        planArchiveCounter.Add(1,
            new KeyValuePair<string, object?>("tenant.id", tenantId),
            new KeyValuePair<string, object?>("plan.id", planId));
        PlanArchiveTotal += 1;
    }

    public void UpdateUnassignedBacklog(long count)
    {
        Interlocked.Exchange(ref unassignedBacklog, count);
    }
}