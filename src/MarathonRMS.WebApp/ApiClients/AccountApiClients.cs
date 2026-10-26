using MarathonRMS.DTOs.Accounts;
using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Legal;
using MarathonRMS.DTOs.Organizations;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>Hồ sơ người chạy + đồng ý dữ liệu (UC-03, UC-04). B dùng GetProfileAsync để điền sẵn form đăng ký.</summary>
public class ProfileApiClient : ApiClientBase
{
    public ProfileApiClient(HttpClient http) : base(http) { }

    public Task<ParticipantProfileDto> GetProfileAsync(CancellationToken ct = default) => GetAsync<ParticipantProfileDto>("api/participants/me", ct);
    public Task<ParticipantProfileDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default) =>
        PutAsync<ParticipantProfileDto>("api/participants/me", request, ct);

    public Task<List<ConsentStatusDto>> GetConsentsAsync(CancellationToken ct = default) => GetAsync<List<ConsentStatusDto>>("api/consents/me", ct);
    public Task<List<ConsentStatusDto>> SetConsentAsync(SetConsentRequest request, CancellationToken ct = default) =>
        PutAsync<List<ConsentStatusDto>>("api/consents", request, ct);
}

/// <summary>Hồ sơ BTC của Member (UC-08) và Staff của BTC (UC-13).</summary>
public class OrganizationApiClient : ApiClientBase
{
    public OrganizationApiClient(HttpClient http) : base(http) { }

    public Task<OrganizationApplicationDto> SubmitApplicationAsync(CreateOrganizationApplicationRequest request, CancellationToken ct = default) =>
        PostAsync<OrganizationApplicationDto>("api/organization-applications", request, ct);
    public Task<List<OrganizationApplicationDto>> GetMyApplicationsAsync(CancellationToken ct = default) =>
        GetAsync<List<OrganizationApplicationDto>>("api/organization-applications/me", ct);

    /// <summary>UC-16 – BTC của Organizer đang đăng nhập.</summary>
    public Task<OrganizationDto> GetMineAsync(CancellationToken ct = default) => GetAsync<OrganizationDto>("api/organizations/me", ct);
    public Task<OrganizationDto> UpdateMineAsync(UpdateMyOrganizationRequest request, CancellationToken ct = default) =>
        PutAsync<OrganizationDto>("api/organizations/me", request, ct);

    public Task<List<StaffMemberDto>> GetStaffAsync(CancellationToken ct = default) => GetAsync<List<StaffMemberDto>>("api/organizations/me/staff", ct);
    public Task<StaffMemberDto> InviteStaffAsync(InviteStaffRequest request, CancellationToken ct = default) =>
        PostAsync<StaffMemberDto>("api/organizations/me/staff", request, ct);
    public Task RemoveStaffAsync(string userId, CancellationToken ct = default) =>
        DeleteAsync($"api/organizations/me/staff/{Uri.EscapeDataString(userId)}", ct);
}

/// <summary>Địa điểm (UC-12). B dùng GetAllAsync cho dropdown chọn địa điểm khi tạo giải.</summary>
public class VenueApiClient : ApiClientBase
{
    public VenueApiClient(HttpClient http) : base(http) { }

    public Task<List<VenueDto>> GetAllAsync(string? city = null, CancellationToken ct = default) =>
        GetAsync<List<VenueDto>>(Query("api/venues", ("city", city)), ct);
    public Task<VenueDto> CreateAsync(UpsertVenueRequest request, CancellationToken ct = default) => PostAsync<VenueDto>("api/venues", request, ct);
    public Task<VenueDto> UpdateAsync(int id, UpsertVenueRequest request, CancellationToken ct = default) => PutAsync<VenueDto>($"api/venues/{id}", request, ct);
    public Task DeleteVenueAsync(int id, CancellationToken ct = default) => DeleteAsync($"api/venues/{id}", ct);
}

/// <summary>Quản trị nền tảng (UC-09, UC-10, UC-11).</summary>
public class AdminApiClient : ApiClientBase
{
    public AdminApiClient(HttpClient http) : base(http) { }

    public Task<PagedResult<OrganizationApplicationDto>> SearchApplicationsAsync(string? status, int page, CancellationToken ct = default) =>
        GetAsync<PagedResult<OrganizationApplicationDto>>(Query("api/admin/organization-applications", ("status", status), ("page", page)), ct);
    public Task<OrganizationApplicationDto> ApproveApplicationAsync(int id, CancellationToken ct = default) =>
        PostAsync<OrganizationApplicationDto>($"api/admin/organization-applications/{id}/approve", ct);
    public Task<OrganizationApplicationDto> RejectApplicationAsync(int id, RejectApplicationRequest request, CancellationToken ct = default) =>
        PostAsync<OrganizationApplicationDto>($"api/admin/organization-applications/{id}/reject", request, ct);

    public Task<PagedResult<OrganizationDto>> SearchOrganizationsAsync(string? keyword, int page, CancellationToken ct = default) =>
        GetAsync<PagedResult<OrganizationDto>>(Query("api/admin/organizations", ("keyword", keyword), ("page", page)), ct);
    public Task<OrganizationDto> GetOrganizationAsync(int id, CancellationToken ct = default) => GetAsync<OrganizationDto>($"api/admin/organizations/{id}", ct);
    public Task<OrganizationDto> UpdateOrganizationAsync(int id, UpdateOrganizationRequest request, CancellationToken ct = default) =>
        PutAsync<OrganizationDto>($"api/admin/organizations/{id}", request, ct);
    public Task<OrganizationDto> SetOrganizationActiveAsync(int id, SetActiveRequest request, CancellationToken ct = default) =>
        PatchAsync<OrganizationDto>($"api/admin/organizations/{id}/active", request, ct);

    public Task<PagedResult<UserListItemDto>> SearchUsersAsync(string? keyword, string? role, int page, CancellationToken ct = default) =>
        GetAsync<PagedResult<UserListItemDto>>(Query("api/admin/users", ("keyword", keyword), ("role", role), ("page", page)), ct);
    public Task<UserListItemDto> SetUserActiveAsync(string id, SetActiveRequest request, CancellationToken ct = default) =>
        PatchAsync<UserListItemDto>($"api/admin/users/{Uri.EscapeDataString(id)}/active", request, ct);

    // UC-85 – danh mục lý do phạt / DSQ
    public Task<List<PenaltyReasonAdminDto>> GetPenaltyReasonsAsync(CancellationToken ct = default) =>
        GetAsync<List<PenaltyReasonAdminDto>>("api/admin/penalty-reasons", ct);
    public Task<PenaltyReasonAdminDto> CreatePenaltyReasonAsync(UpsertPenaltyReasonRequest request, CancellationToken ct = default) =>
        PostAsync<PenaltyReasonAdminDto>("api/admin/penalty-reasons", request, ct);
    public Task<PenaltyReasonAdminDto> UpdatePenaltyReasonAsync(string code, UpsertPenaltyReasonRequest request, CancellationToken ct = default) =>
        PutAsync<PenaltyReasonAdminDto>($"api/admin/penalty-reasons/{Uri.EscapeDataString(code)}", request, ct);
    public Task DeletePenaltyReasonAsync(string code, CancellationToken ct = default) =>
        DeleteAsync($"api/admin/penalty-reasons/{Uri.EscapeDataString(code)}", ct);
}

/// <summary>Chức năng SuperAdmin (UC-14, UC-15, UC-84).</summary>
public class SuperAdminApiClient : ApiClientBase
{
    public SuperAdminApiClient(HttpClient http) : base(http) { }

    /// <summary>UC-17 – nhật ký hệ thống qua OData.</summary>
    public Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(string? filter, int page, int pageSize = 20, CancellationToken ct = default) =>
        GetODataPageAsync<AuditLogDto>("AuditLogs", filter, "CreatedAt desc,Id desc", page, pageSize, ct);
    public Task<AuditLogFacetsDto> GetAuditLogFacetsAsync(CancellationToken ct = default) =>
        GetAsync<AuditLogFacetsDto>("api/superadmin/audit-logs/facets", ct);

    public Task<PagedResult<UserListItemDto>> GetAdminsAsync(CancellationToken ct = default) => GetAsync<PagedResult<UserListItemDto>>("api/superadmin/admins", ct);
    public Task<UserListItemDto> CreateAdminAsync(CreateAdminRequest request, CancellationToken ct = default) =>
        PostAsync<UserListItemDto>("api/superadmin/admins", request, ct);
    public Task<UserListItemDto> SetAdminActiveAsync(string id, SetActiveRequest request, CancellationToken ct = default) =>
        PatchAsync<UserListItemDto>($"api/superadmin/admins/{Uri.EscapeDataString(id)}/active", request, ct);
    public Task<UserListItemDto> ChangeAdminRoleAsync(string id, ChangeAdminRoleRequest request, CancellationToken ct = default) =>
        PutAsync<UserListItemDto>($"api/superadmin/admins/{Uri.EscapeDataString(id)}/role", request, ct);

    public Task<List<SystemSettingDto>> GetSettingsAsync(CancellationToken ct = default) => GetAsync<List<SystemSettingDto>>("api/superadmin/settings", ct);
    public Task<SystemSettingDto> UpdateSettingAsync(string key, UpdateSettingRequest request, CancellationToken ct = default) =>
        PutAsync<SystemSettingDto>($"api/superadmin/settings/{Uri.EscapeDataString(key)}", request, ct);

    public Task<List<LegalDocumentDto>> GetLegalVersionsAsync(string docType, CancellationToken ct = default) =>
        GetAsync<List<LegalDocumentDto>>($"api/superadmin/legal/{docType}/versions", ct);
    public Task<LegalDocumentDto> PublishLegalAsync(PublishLegalDocumentRequest request, CancellationToken ct = default) =>
        PostAsync<LegalDocumentDto>("api/superadmin/legal", request, ct);
}
