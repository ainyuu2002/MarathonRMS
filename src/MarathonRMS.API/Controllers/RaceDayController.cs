using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.Services.Events;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.API.Controllers;

/// <summary>Vận hành ngày thi cho Staff/Organizer của BTC: chọn giải, quầy race kit (UC-52).</summary>
[ApiController]
[Route("api/race-day")]
[Authorize(Policy = AppPolicies.StaffOrOrganizer)]
public class RaceDayController : ControllerBase
{
    private readonly IEventService _events;
    private readonly IRaceKitService _raceKits;

    public RaceDayController(IEventService events, IRaceKitService raceKits)
    {
        _events = events;
        _raceKits = raceKits;
    }

    /// <summary>Giải của BTC người gọi đã công bố (không gồm bản nháp, đã hủy).</summary>
    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<EventSummaryDto>>> GetEvents(CancellationToken ct)
    {
        // Status trong DTO là chuỗi chiếu từ enum – lọc sau khi tải (mỗi BTC có ít giải)
        var events = await _events.QueryMine().OrderByDescending(e => e.EventDate).ThenBy(e => e.Name).ToListAsync(ct);
        return Ok(events.Where(e => e.Status is not ("Draft" or "Cancelled")).ToList());
    }

    [HttpGet("events/{eventId:int}/race-kit")]
    public async Task<ActionResult<IReadOnlyList<RaceKitLookupItemDto>>> LookupRaceKit(int eventId, [FromQuery] string? keyword,
        CancellationToken ct) => Ok(await _raceKits.LookupAsync(eventId, keyword ?? "", ct));
}
