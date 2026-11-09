using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-66 – khiếu nại kết quả: VĐV gửi / xem của mình; Organizer xem theo giải và xử lý.</summary>
[ApiController]
[Route("api")]
[Authorize]
public class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _complaints;
    public ComplaintsController(IComplaintService complaints) => _complaints = complaints;

    /// <summary>Chỉ chính VĐV / người đăng ký hộ, khi kết quả tạm thời và còn hạn (BR-27).</summary>
    [HttpPost("results/{resultId:int}/complaints")]
    public async Task<ActionResult<ComplaintDto>> Create(int resultId, CreateComplaintRequest request, CancellationToken ct) =>
        Ok(await _complaints.CreateAsync(resultId, request, ct));

    [HttpGet("complaints/me")]
    public async Task<ActionResult<IReadOnlyList<ComplaintDto>>> GetMine(CancellationToken ct) =>
        Ok(await _complaints.GetMineAsync(ct));

    [HttpGet("events/{eventId:int}/complaints")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<EventComplaintsDto>> GetForEvent(int eventId, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await _complaints.GetForEventAsync(eventId, status, ct));

    [HttpPost("complaints/{complaintId:int}/resolve")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<ComplaintDto>> Resolve(int complaintId, ResolveComplaintRequest request, CancellationToken ct) =>
        Ok(await _complaints.ResolveAsync(complaintId, request, ct));
}
