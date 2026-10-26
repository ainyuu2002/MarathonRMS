using MarathonRMS.DTOs.Accounts;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Organizations;
using MarathonRMS.Services.Accounts;
using MarathonRMS.Services.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-03 – hồ sơ người chạy chính chủ. B dùng GET để điền sẵn form đăng ký giải.</summary>
[ApiController]
[Route("api/participants")]
[Authorize]
public class ParticipantsController : ControllerBase
{
    private readonly IProfileService _profiles;
    public ParticipantsController(IProfileService profiles) => _profiles = profiles;

    [HttpGet("me")]
    public async Task<ActionResult<ParticipantProfileDto>> GetMine(CancellationToken ct) => Ok(await _profiles.GetMineAsync(ct));

    [HttpPut("me")]
    public async Task<ActionResult<ParticipantProfileDto>> UpdateMine(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await _profiles.UpdateMineAsync(request, ct));
}

/// <summary>UC-04 – các đồng ý xử lý dữ liệu (BR-09).</summary>
[ApiController]
[Route("api/consents")]
[Authorize]
public class ConsentsController : ControllerBase
{
    private readonly IConsentService _consents;
    public ConsentsController(IConsentService consents) => _consents = consents;

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<ConsentStatusDto>>> GetMine(CancellationToken ct) => Ok(await _consents.GetMineAsync(ct));

    /// <summary>Bật/tắt đồng ý tùy chọn (HealthData, PhotoMarketing, PublicListing).</summary>
    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<ConsentStatusDto>>> Set(SetConsentRequest request, CancellationToken ct) =>
        Ok(await _consents.SetAsync(request, ct));
}

/// <summary>UC-08 – Member nộp hồ sơ đăng ký làm BTC.</summary>
[ApiController]
[Route("api/organization-applications")]
[Authorize(Policy = AppPolicies.MemberOnly)]
public class OrganizationApplicationsController : ControllerBase
{
    private readonly IOrganizationApplicationService _applications;
    public OrganizationApplicationsController(IOrganizationApplicationService applications) => _applications = applications;

    [HttpPost]
    [ProducesResponseType(typeof(OrganizationApplicationDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrganizationApplicationDto>> Submit(CreateOrganizationApplicationRequest request, CancellationToken ct)
    {
        var created = await _applications.SubmitAsync(request, ct);
        return CreatedAtAction(nameof(GetMine), null, created);
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<OrganizationApplicationDto>>> GetMine(CancellationToken ct) =>
        Ok(await _applications.GetMineAsync(ct));
}

/// <summary>UC-13 – Organizer quản lý Staff của BTC mình (BR-34).</summary>
[ApiController]
[Route("api/organizations/me/staff")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class OrganizationStaffController : ControllerBase
{
    private readonly IOrganizationService _organizations;
    public OrganizationStaffController(IOrganizationService organizations) => _organizations = organizations;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StaffMemberDto>>> GetAll(CancellationToken ct) =>
        Ok(await _organizations.GetMyStaffAsync(ct));

    [HttpPost]
    public async Task<ActionResult<StaffMemberDto>> Invite(InviteStaffRequest request, CancellationToken ct) =>
        Ok(await _organizations.InviteStaffAsync(request, ct));

    [HttpDelete("{userId}")]
    public async Task<IActionResult> Remove(string userId, CancellationToken ct)
    {
        await _organizations.RemoveStaffAsync(userId, ct);
        return NoContent();
    }
}
