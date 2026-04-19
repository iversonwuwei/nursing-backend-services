using NursingBackend.BuildingBlocks.Contracts;

namespace NursingBackend.Bff.Admin;

internal static class AdminBffAggregationPolicy
{
	public static AdminRoomRecordResponse MergeRoomRecord(AdminRoomRecordResponse room, IReadOnlyList<ElderListItemResponse> elders)
	{
		var occupants = elders
			.Where(item => string.Equals(item.RoomNumber, room.RoomId, StringComparison.OrdinalIgnoreCase))
			.OrderBy(item => item.AdmissionCreatedAtUtc ?? DateTimeOffset.MaxValue)
			.ThenBy(item => item.ElderId, StringComparer.Ordinal)
			.Take(room.Capacity)
			.ToArray();

		var occupied = occupants.Length;
		var effectiveStatus = room.LifecycleStatus == "待启用"
			? "待启用"
			: room.Status == "维护中"
				? "维护中"
				: occupied >= room.Capacity
					? "已满"
					: "可入住";

		var beds = Enumerable.Range(1, room.Capacity)
			.Select(index =>
			{
				var occupant = occupants.ElementAtOrDefault(index - 1);
				if (occupant is null)
				{
					return new AdminRoomBedInfoResponse(index, "available", null);
				}

				return new AdminRoomBedInfoResponse(
					index,
					"occupied",
					new AdminRoomBedOccupantResponse(
						occupant.ElderId,
						occupant.ElderName,
						occupant.CareLevel,
						occupant.AdmissionCreatedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "待补录"));
			})
			.ToArray();

		return room with
		{
			Occupied = occupied,
			Status = effectiveStatus,
			BedsInfo = beds,
		};
	}

	public static bool MatchesOrganization(AdminRoomRecordResponse room, OrganizationRecordResponse organization)
	{
		if (!string.IsNullOrWhiteSpace(room.OrganizationId)
			&& string.Equals(room.OrganizationId, organization.OrganizationId, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return string.Equals(room.OrganizationName, organization.Name, StringComparison.OrdinalIgnoreCase);
	}

	public static bool MatchesStaffOrganization(AdminStaffRecordResponse staff, OrganizationRecordResponse organization)
	{
		if (!string.IsNullOrWhiteSpace(staff.OrganizationId)
			&& string.Equals(staff.OrganizationId, organization.OrganizationId, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return !string.IsNullOrWhiteSpace(staff.OrganizationName)
			&& string.Equals(staff.OrganizationName, organization.Name, StringComparison.OrdinalIgnoreCase);
	}

	public static AdminOrganizationSummaryResponse MergeOrganizationSummary(OrganizationRecordResponse organization, IReadOnlyList<AdminRoomRecordResponse> rooms, IReadOnlyList<AdminStaffRecordResponse> staff)
	{
		var matchedRooms = rooms.Where(room => MatchesOrganization(room, organization)).ToArray();
		var matchedStaff = staff.Where(item => MatchesStaffOrganization(item, organization)).ToArray();
		var totalBeds = matchedRooms.Sum(room => room.Capacity);
		var occupiedBeds = matchedRooms.Sum(room => room.Occupied);
		var availableBeds = Math.Max(0, totalBeds - occupiedBeds);

		return new AdminOrganizationSummaryResponse(
			OrganizationId: organization.OrganizationId,
			TenantId: organization.TenantId,
			Name: organization.Name,
			Address: organization.Address,
			Phone: organization.Phone,
			Status: organization.Status,
			EstablishedDate: organization.EstablishedDate,
			Manager: organization.Manager,
			ManagerPhone: organization.ManagerPhone,
			Description: organization.Description,
			LifecycleStatus: organization.LifecycleStatus,
			CreatedAt: organization.CreatedAt,
			ActivatedAt: organization.ActivatedAt,
			ActivationNote: organization.ActivationNote,
			TotalBeds: totalBeds,
			OccupiedBeds: occupiedBeds,
			AvailableBeds: availableBeds,
			ElderlyCount: occupiedBeds,
			StaffCount: matchedStaff.Length,
			RoomCount: matchedRooms.Length,
			StaffIntegrationStatus: "live");
	}

	public static AdminOrganizationRoomSummaryResponse MapOrganizationRoom(AdminRoomRecordResponse room) => new(
		RoomId: room.RoomId,
		Name: room.Name,
		FloorName: room.FloorName,
		Type: room.Type,
		Capacity: room.Capacity,
		Occupied: room.Occupied,
		Status: room.Status,
		CleanStatus: room.CleanStatus);

	public static IReadOnlyList<string> BuildAssessmentMedicalAlerts(AdminAssessmentCaseCreateRequest request)
	{
		return new[]
		{
			request.ChronicConditions,
			request.AllergySummary,
			request.RiskNotes,
		}
			.Select(value => value?.Trim())
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.Cast<string>()
			.Distinct(StringComparer.Ordinal)
			.ToArray();
	}

	public static AssessmentAiRecommendationResponse BuildAssessmentAiRecommendation(AdminAssessmentCaseCreateRequest request, AiAdmissionAssessmentResponse response)
	{
		var reasons = response.RiskFactors.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
		var focusTags = response.RiskFactors
			.Concat(response.Recommendations)
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.Take(3)
			.ToArray();
		var confidence = Math.Clamp(72 + reasons.Length * 6, 72, 95);
		var assessmentScore = Math.Clamp(request.AdlScore + reasons.Length * 4, 20, 100);
		var levelCode = response.RecommendedCareLevel
			.Replace("特级护理", "L4", StringComparison.Ordinal)
			.Replace("一级护理", "L3", StringComparison.Ordinal)
			.Replace("二级护理", "L2", StringComparison.Ordinal)
			.Replace("三级护理", "L1", StringComparison.Ordinal)
			.Replace(" ", string.Empty, StringComparison.Ordinal);

		return new AssessmentAiRecommendationResponse(
			RecommendedLevel: response.RecommendedCareLevel,
			Confidence: confidence,
			AssessmentScore: assessmentScore,
			ReasonSummary: response.AssessmentSummary,
			Reasons: reasons.Length > 0 ? reasons : [response.AssessmentSummary],
			FocusTags: focusTags.Length > 0 ? focusTags : [request.RequestedCareLevel],
			PlanTemplateCode: $"ASSESS-{levelCode}-{(reasons.Length > 1 ? "HIGH" : "STD")}");
	}
}