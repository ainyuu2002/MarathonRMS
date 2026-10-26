using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Legal;
using MarathonRMS.DTOs.Organizations;
using MarathonRMS.Services.Admin;
using MarathonRMS.Services.Legal;
using MarathonRMS.Services.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-09 – Admin duyệt / từ chối hồ sơ BTC (BR-33).</summary>
[ApiController]
[Route("api/admin/organization-applications")]
[Authorize(Policy = AppPolicies.AdminOrAbove)]
public class AdminOrganizationApplicationsController : ControllerBase
{
    private readonly IOrganizationApplicationService _applications;
    public AdminOrganizationApplicationsController(IOrganizationApplicationService applications) => _applications = applications;

    /// <summary>status = Pending | Approved | Rejected (bỏ trống = tất cả; hồ sơ chờ duyệt lên đầu).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrganizationApplicationDto>>> Search(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _applications.SearchAsync(status, page, pageSize, ct));

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<OrganizationApplicationDto>> Approve(int id, CancellationToken ct) =>
        Ok(await _applications.ApproveAsync(id, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<OrganizationApplicationDto>> Reject(int id, RejectApplicationRequest request, CancellationToken ct) =>
        Ok(await _applications.RejectAsync(id, request, ct));
}

/// <summary>UC-10 – Admin xem, sửa, tạm khóa BTC.</summary>
[ApiController]
[Route("api/admin/organizations")]
[Authorize(Policy = AppPolicies.AdminOrAbove)]
public class AdminOrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizations;
    public AdminOrganizationsController(IOrganizationService organizations) => _organizations = organizations;

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrganizationDto>>> Search(
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _organizations.SearchAsync(keyword, page, pageSize, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrganizationDto>> Get(int id, CancellationToken ct) => Ok(await _organizations.GetAsync(id, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OrganizationDto>> Update(int id, UpdateOrganizationRequest request, CancellationToken ct) =>
        Ok(await _organizations.UpdateAsync(id, request, ct));

    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<OrganizationDto>> SetActive(int id, SetActiveRequest request, CancellationToken ct) =>
        Ok(await _organizations.SetActiveAsync(id, request.IsActive, request.Reason, ct));
}

/// <summary>UC-11 – Admin khóa/mở tài khoản Member/Organizer/Staff (BR-35: không áp dụng cho tài khoản quản trị).</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AppPolicies.AdminOrAbove)]
public class AdminUsersController : ControllerBase
{
    private readonly IUserAdminService _users;
    public AdminUsersController(IUserAdminService users) => _users = users;

    /// <summary>role = Member | Organizer | Staff | Admin | SuperAdmin | Admins (bỏ trống = tất cả).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> Search(
        [FromQuery] string? keyword, [FromQuery] string? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await _users.SearchAsync(keyword, role, page, pageSize, ct));

    [HttpPatch("{id}/active")]
    public async Task<ActionResult<UserListItemDto>> SetActive(string id, SetActiveRequest request, CancellationToken ct) =>
        Ok(await _users.SetUserActiveAsync(id, request, ct));
}

/// <summary>UC-85 – danh mục lý do phạt / DSQ (BR-28).</summary>
[ApiController]
[Route("api/admin/penalty-reasons")]
[Authorize(Policy = AppPolicies.AdminOrAbove)]
public class AdminPenaltyReasonsController : ControllerBase
{
    private readonly IPenaltyReasonService _reasons;
    public AdminPenaltyReasonsController(IPenaltyReasonService reasons) => _reasons = reasons;

    /// <summary>UC-85 – toàn bộ danh mục, kể cả lý do đã ngừng dùng, kèm số lần đã dùng.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PenaltyReasonAdminDto>>> GetAll(CancellationToken ct) => Ok(await _reasons.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<PenaltyReasonAdminDto>> Create(UpsertPenaltyReasonRequest request, CancellationToken ct) =>
        Ok(await _reasons.CreateAsync(request, ct));

    [HttpPut("{code}")]
    public async Task<ActionResult<PenaltyReasonAdminDto>> Update(string code, UpsertPenaltyReasonRequest request, CancellationToken ct) =>
        Ok(await _reasons.UpdateAsync(code, request, ct));

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        await _reasons.DeleteAsync(code, ct);
        return NoContent();
    }
}

/// <summary>UC-12 – địa điểm. GET công khai (B dùng cho dropdown tạo giải); ghi: Admin, SuperAdmin, Organizer.</summary>
[ApiController]
[Route("api/venues")]
public class VenuesController : ControllerBase
{
    private const string Writers = AppRoles.Admin + "," + AppRoles.SuperAdmin + "," + AppRoles.Organizer;
    private readonly IVenueService _venues;
    public VenuesController(IVenueService venues) => _venues = venues;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<VenueDto>>> GetAll([FromQuery] string? city, CancellationToken ct) =>
        Ok(await _venues.GetAllAsync(city, ct));

    [HttpPost]
    [Authorize(Roles = Writers)]
    public async Task<ActionResult<VenueDto>> Create(UpsertVenueRequest request, CancellationToken ct)
    {
        var venue = await _venues.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), null, venue);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Writers)]
    public async Task<ActionResult<VenueDto>> Update(int id, UpsertVenueRequest request, CancellationToken ct) =>
        Ok(await _venues.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _venues.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>UC-14 (tài khoản Admin), UC-15 (cấu hình hệ thống), UC-84 (văn bản pháp lý) – chỉ SuperAdmin.</summary>
[ApiController]
[Route("api/superadmin")]
[Authorize(Policy = AppPolicies.SuperAdminOnly)]
public class SuperAdminController : ControllerBase
{
    private readonly IUserAdminService _users;
    private readonly ISystemSettingService _settings;
    private readonly ILegalService _legal;

    public SuperAdminController(IUserAdminService users, ISystemSettingService settings, ILegalService legal)
    {
        _users = users;
        _settings = settings;
        _legal = legal;
    }

    [HttpGet("admins")]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> GetAdmins(
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await _users.SearchAsync(keyword, UserAdminService.AdminsFilter, page, pageSize, ct));

    [HttpPost("admins")]
    public async Task<ActionResult<UserListItemDto>> CreateAdmin(CreateAdminRequest request, CancellationToken ct) =>
        Ok(await _users.CreateAdminAsync(request, ct));

    [HttpPatch("admins/{id}/active")]
    public async Task<ActionResult<UserListItemDto>> SetAdminActive(string id, SetActiveRequest request, CancellationToken ct) =>
        Ok(await _users.SetAdminActiveAsync(id, request, ct));

    [HttpPut("admins/{id}/role")]
    public async Task<ActionResult<UserListItemDto>> ChangeAdminRole(string id, ChangeAdminRoleRequest request, CancellationToken ct) =>
        Ok(await _users.ChangeAdminRoleAsync(id, request, ct));

    [HttpGet("settings")]
    public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> GetSettings(CancellationToken ct) =>
        Ok(await _settings.GetAllAsync(ct));

    [HttpPut("settings/{key}")]
    public async Task<ActionResult<SystemSettingDto>> UpdateSetting(string key, UpdateSettingRequest request, CancellationToken ct) =>
        Ok(await _settings.UpdateAsync(key, request, ct));

    [HttpGet("legal/{docType}/versions")]
    public async Task<ActionResult<IReadOnlyList<LegalDocumentDto>>> GetLegalVersions(string docType, CancellationToken ct) =>
        Ok(await _legal.GetVersionsAsync(docType, ct));

    [HttpPost("legal")]
    public async Task<ActionResult<LegalDocumentDto>> PublishLegal(PublishLegalDocumentRequest request, CancellationToken ct) =>
        Ok(await _legal.PublishAsync(request, ct));
}
