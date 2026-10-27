using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Services.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-18 – Organizer tạo, xem, sửa và xóa giải của BTC mình.</summary>
[ApiController]
[Route("api/events")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class EventsController : ControllerBase
{
    private readonly IEventService _events;
    public EventsController(IEventService events) => _events = events;

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<EventSummaryDto>>> GetMine(CancellationToken ct) =>
        Ok(await _events.QueryMine().OrderByDescending(e => e.EventDate).ThenBy(e => e.Name).ToListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EventDetailDto>> Get(int id, CancellationToken ct) =>
        Ok(await _events.GetDetailAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<EventDetailDto>> Create(UpsertEventRequest request, CancellationToken ct)
    {
        var created = await _events.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EventDetailDto>> Update(int id, UpsertEventRequest request, CancellationToken ct) =>
        Ok(await _events.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _events.DeleteAsync(id, ct);
        return NoContent();
    }
}
