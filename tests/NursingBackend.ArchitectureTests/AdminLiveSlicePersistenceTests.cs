using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.Services.Elder;
using NursingBackend.Services.Organization;
using NursingBackend.Services.Rooms;
using NursingBackend.Services.Staffing;

namespace NursingBackend.ArchitectureTests;

public class AdminLiveSlicePersistenceTests
{
	[Fact]
	public void Organization_model_keeps_unique_tenant_name_index()
	{
		using var dbContext = CreateOrganizationDbContext();
		var entityType = dbContext.Model.FindEntityType(typeof(OrganizationEntity));
		Assert.NotNull(entityType);
		var index = Assert.Single(entityType.GetIndexes(), index =>
			index.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "Name"]));

		Assert.True(index.IsUnique);
	}

	[Fact]
	public void Rooms_model_keeps_filtering_index_shape()
	{
		using var dbContext = CreateRoomsDbContext();
		var entityType = dbContext.Model.FindEntityType(typeof(RoomEntity));
		Assert.NotNull(entityType);
		var index = Assert.Single(entityType.GetIndexes(), index =>
			index.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "Status", "LifecycleStatus", "OrganizationName", "CreatedAtUtc"]));

		Assert.False(index.IsUnique);
	}

	[Fact]
	public void Staffing_model_keeps_organization_and_status_index_shape()
	{
		using var dbContext = CreateStaffingDbContext();
		var entityType = dbContext.Model.FindEntityType(typeof(StaffMemberEntity));
		Assert.NotNull(entityType);
		var index = Assert.Single(entityType.GetIndexes(), index =>
			index.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "OrganizationId", "Department", "Status", "LifecycleStatus", "CreatedAtUtc"]));

		Assert.False(index.IsUnique);
		Assert.Equal(128, entityType.FindProperty(nameof(StaffMemberEntity.OrganizationId))?.GetMaxLength());
		Assert.Equal(256, entityType.FindProperty(nameof(StaffMemberEntity.OrganizationName))?.GetMaxLength());
	}

	[Fact]
	public void Elder_model_keeps_assessment_and_profile_json_converters()
	{
		using var dbContext = CreateElderDbContext();
		var admissionEntity = dbContext.Model.FindEntityType(typeof(AdmissionRecordEntity));
		var elderEntity = dbContext.Model.FindEntityType(typeof(ElderProfileEntity));
		Assert.NotNull(admissionEntity);
		Assert.NotNull(elderEntity);

		Assert.NotNull(admissionEntity.FindProperty(nameof(AdmissionRecordEntity.SourceDocumentNames))?.GetValueConverter());
		Assert.NotNull(admissionEntity.FindProperty(nameof(AdmissionRecordEntity.AiReasons))?.GetValueConverter());
		Assert.NotNull(admissionEntity.FindProperty(nameof(AdmissionRecordEntity.AiFocusTags))?.GetValueConverter());
		Assert.NotNull(admissionEntity.FindProperty(nameof(AdmissionRecordEntity.SourceDocumentNames))?.GetValueComparer());
		Assert.NotNull(admissionEntity.FindProperty(nameof(AdmissionRecordEntity.AiReasons))?.GetValueComparer());
		Assert.NotNull(admissionEntity.FindProperty(nameof(AdmissionRecordEntity.AiFocusTags))?.GetValueComparer());
		Assert.NotNull(elderEntity.FindProperty(nameof(ElderProfileEntity.MedicalAlerts))?.GetValueConverter());
		Assert.NotNull(elderEntity.FindProperty(nameof(ElderProfileEntity.ServiceItems))?.GetValueConverter());
		Assert.NotNull(elderEntity.FindProperty(nameof(ElderProfileEntity.MedicalAlerts))?.GetValueComparer());
		Assert.NotNull(elderEntity.FindProperty(nameof(ElderProfileEntity.ServiceItems))?.GetValueComparer());
	}

	[Fact]
	public void Elder_model_string_list_converters_tolerate_empty_or_malformed_provider_values()
	{
		using var dbContext = CreateElderDbContext();
		var admissionEntity = dbContext.Model.FindEntityType(typeof(AdmissionRecordEntity));
		var elderEntity = dbContext.Model.FindEntityType(typeof(ElderProfileEntity));
		Assert.NotNull(admissionEntity);
		Assert.NotNull(elderEntity);

		var admissionConverter = admissionEntity.FindProperty(nameof(AdmissionRecordEntity.SourceDocumentNames))?.GetValueConverter();
		var alertsConverter = elderEntity.FindProperty(nameof(ElderProfileEntity.MedicalAlerts))?.GetValueConverter();
		Assert.NotNull(admissionConverter);
		Assert.NotNull(alertsConverter);

		Assert.Equal([], Assert.IsType<List<string>>(admissionConverter.ConvertFromProvider("")));
		Assert.Equal([], Assert.IsType<List<string>>(alertsConverter.ConvertFromProvider("not-json")));
	}

	private static OrganizationDbContext CreateOrganizationDbContext()
	{
		var options = new DbContextOptionsBuilder<OrganizationDbContext>()
			.UseNpgsql("Host=localhost;Database=nursing_test_org;Username=test;Password=test")
			.Options;
		return new OrganizationDbContext(options);
	}

	private static RoomsDbContext CreateRoomsDbContext()
	{
		var options = new DbContextOptionsBuilder<RoomsDbContext>()
			.UseNpgsql("Host=localhost;Database=nursing_test_rooms;Username=test;Password=test")
			.Options;
		return new RoomsDbContext(options);
	}

	private static StaffingDbContext CreateStaffingDbContext()
	{
		var options = new DbContextOptionsBuilder<StaffingDbContext>()
			.UseNpgsql("Host=localhost;Database=nursing_test_staffing;Username=test;Password=test")
			.Options;
		return new StaffingDbContext(options);
	}

	private static ElderDbContext CreateElderDbContext()
	{
		var options = new DbContextOptionsBuilder<ElderDbContext>()
			.UseNpgsql("Host=localhost;Database=nursing_test_elder;Username=test;Password=test")
			.Options;
		return new ElderDbContext(options);
	}
}