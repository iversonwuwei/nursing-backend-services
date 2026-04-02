namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record TenantDescriptorResponse(
    string TenantId,
    string TenantName,
    string Plan,
    string DataIsolationMode,
    IReadOnlyList<string> EnabledModules,
    IReadOnlyList<string> EnabledFeatures,
    IReadOnlyList<string> Branches);