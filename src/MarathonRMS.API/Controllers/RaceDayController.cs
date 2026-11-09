using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>Vận hành ngày thi cho Staff/Organizer của BTC: chọn giải, quầy race kit (UC-52, UC-53).</summary>
[ApiController]
[Route("api/race-day")]
[Authorize(Policy = AppPolicies.StaffOrOrganizer)]
public class RaceDayController : ControllerBase
{
    private readonly IRaceKitService _raceKits;

    public RaceDayController(IRaceKitService raceKits) => _raceKits = raceKits;

    /// <summary>Giải của BTC người gọi đã công bố (không gồm bản nháp, đã hủy).</summary>
    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<EventSummaryDto>>> GetEvents(CancellationToken ct) =>
        Ok(await _raceKits.GetOperationalEventsAsync(ct));

    /// <summary>Tra cứu bằng POST: số giấy tờ / SĐT nằm trong body, không lên URL và log truy cập.</summary>
    [HttpPost("events/{eventId:int}/race-kit/search")]
    public async Task<ActionResult<IReadOnlyList<RaceKitLookupItemDto>>> LookupRaceKit(int eventId, RaceKitLookupRequest request,
        CancellationToken ct) => Ok(await _raceKits.LookupAsync(eventId, request.Keyword, ct));

    [HttpPost("race-kit/{registrationId:int}/pickup")]
    public async Task<ActionResult<RaceKitPickupDto>> PickupRaceKit(int registrationId, RaceKitPickupRequest request,
        CancellationToken ct) => Ok(await _raceKits.PickupAsync(registrationId, request, ct));
}
