using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-57 – bấm giờ tại checkpoint.</summary>
[ApiController]
[Route("api/race-day")]
[Authorize(Policy = AppPolicies.StaffOrOrganizer)]
public class TimingController : ControllerBase
{
    private readonly ITimingService _timing;
    public TimingController(ITimingService timing) => _timing = timing;

    [HttpPost("timing")]
    public async Task<ActionResult<TimingRecordDto>> Record(RecordTimingRequest request, CancellationToken ct) =>
        Ok(await _timing.RecordAsync(request, ct));

    /// <summary>?bib=… ⇒ mọi thời gian của BIB; không có bib ⇒ bản ghi mới nhất (lọc checkpointId).</summary>
    [HttpGet("events/{eventId:int}/timing")]
    public async Task<ActionResult<IReadOnlyList<TimingRecordDto>>> Get(int eventId, [FromQuery] int? bib,
        [FromQuery] int? checkpointId, [FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(bib is int b
            ? await _timing.GetByBibAsync(eventId, b, ct)
            : await _timing.GetRecentAsync(eventId, checkpointId, take, ct));

    [HttpGet("registrations/{registrationId:int}/timing")]
    public async Task<ActionResult<IReadOnlyList<TimingRecordDto>>> GetByRegistration(int registrationId, CancellationToken ct) =>
        Ok(await _timing.GetByRegistrationAsync(registrationId, ct));

    [HttpDelete("timing/{id:int}")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _timing.DeleteAsync(id, ct);
        return NoContent();
    }
}
