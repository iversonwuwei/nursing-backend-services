namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record VisitAppointmentCreateRequest(
    string ElderId,
    string VisitorName,
    string Relation,
    string Phone,
    DateTimeOffset PlannedAtUtc,
    string VisitType,
    string Notes);

public sealed record VisitAppointmentResponse(
    string VisitId,
    string ElderId,
    string TenantId,
    string VisitorName,
    string Relation,
    string Status,
    DateTimeOffset PlannedAtUtc);

public sealed record AdminVisitAppointmentResponse(
    string VisitId,
    string ElderId,
    string TenantId,
    string VisitorName,
    string Relation,
    string? Phone,
    DateTimeOffset PlannedAtUtc,
    string VisitType,
    string Status,
    string? Notes);