using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Services.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-18/19 – quản lý thông tin, chính sách và văn bản miễn trừ của giải.</summary>
[ApiController]
[Route("api/events")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class EventsController : ControllerBase
{
    private readonly IEventService _events;
    private readonly IEventPolicyService _policies;
    public EventsController(IEventService events, IEventPolicyService policies)
    {
        _events = events;
        _policies = policies;
    }

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

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, ChangeEventStatusRequest request, CancellationToken ct)
    {
        await _events.ChangeStatusAsync(id, request, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/policies")]
    [AllowAnonymous]
    public async Task<ActionResult<EventPolicyDto>> GetPolicy(int id, CancellationToken ct) =>
        Ok(await _policies.GetAsync(id, ct));

    [HttpPut("{id:int}/policies")]
    public async Task<ActionResult<EventPolicyDto>> UpdatePolicy(int id, UpdateEventPolicyRequest request, CancellationToken ct) =>
        Ok(await _policies.UpdateAsync(id, request, ct));

    [HttpGet("{id:int}/waiver")]
    [AllowAnonymous]
    public async Task<ActionResult<EventWaiverDto>> GetWaiver(int id, CancellationToken ct) =>
        Ok(await _policies.GetWaiverAsync(id, ct));

    [HttpPut("{id:int}/waiver")]
    public async Task<ActionResult<EventWaiverDto>> UpdateWaiver(int id, UpdateEventWaiverRequest request, CancellationToken ct) =>
        Ok(await _policies.UpdateWaiverAsync(id, request, ct));
}
