using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Services.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-22/23/24 – mức giá, nhóm tuổi và checkpoint.</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class EventSetupController : ControllerBase
{
    private readonly IPriceTierService _prices;
    private readonly IAgeGroupService _ageGroups;
    private readonly ICheckpointService _checkpoints;

    public EventSetupController(IPriceTierService prices, IAgeGroupService ageGroups, ICheckpointService checkpoints)
    {
        _prices = prices;
        _ageGroups = ageGroups;
        _checkpoints = checkpoints;
    }

    [HttpGet("categories/{categoryId:int}/price-tiers")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PriceTierDto>>> GetPriceTiers(int categoryId, CancellationToken ct) =>
        Ok(await _prices.GetByCategoryAsync(categoryId, ct));

    [HttpPost("categories/{categoryId:int}/price-tiers")]
    public async Task<ActionResult<PriceTierDto>> CreatePriceTier(int categoryId, UpsertPriceTierRequest request,
        CancellationToken ct)
    {
        var created = await _prices.CreateAsync(categoryId, request, ct);
        return CreatedAtAction(nameof(GetPriceTiers), new { categoryId }, created);
    }

    [HttpPut("price-tiers/{id:int}")]
    public async Task<ActionResult<PriceTierDto>> UpdatePriceTier(int id, UpsertPriceTierRequest request,
        CancellationToken ct) => Ok(await _prices.UpdateAsync(id, request, ct));

    [HttpDelete("price-tiers/{id:int}")]
    public async Task<IActionResult> DeletePriceTier(int id, CancellationToken ct)
    {
        await _prices.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>UC-87 – khoảng giá tham khảo theo loại đường chạy + cự ly (không gồm giải đang sửa).</summary>
    [HttpGet("pricing/reference")]
    public async Task<ActionResult<PriceReferenceDto>> GetPriceReference([FromQuery] string discipline, [FromQuery] decimal distanceKm,
        [FromQuery] int? excludeEventId, [FromServices] IPriceReferenceService reference, CancellationToken ct) =>
        Ok(await reference.GetAsync(discipline, distanceKm, excludeEventId, ct));

    [HttpGet("events/{eventId:int}/age-groups")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<AgeGroupDto>>> GetAgeGroups(int eventId, CancellationToken ct) =>
        Ok(await _ageGroups.GetByEventAsync(eventId, ct));

    [HttpPost("events/{eventId:int}/age-groups")]
    public async Task<ActionResult<AgeGroupDto>> CreateAgeGroup(int eventId, UpsertAgeGroupRequest request,
        CancellationToken ct)
    {
        var created = await _ageGroups.CreateAsync(eventId, request, ct);
        return CreatedAtAction(nameof(GetAgeGroups), new { eventId }, created);
    }

    [HttpPut("age-groups/{id:int}")]
    public async Task<ActionResult<AgeGroupDto>> UpdateAgeGroup(int id, UpsertAgeGroupRequest request,
        CancellationToken ct) => Ok(await _ageGroups.UpdateAsync(id, request, ct));

    [HttpDelete("age-groups/{id:int}")]
    public async Task<IActionResult> DeleteAgeGroup(int id, CancellationToken ct)
    {
        await _ageGroups.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("categories/{categoryId:int}/checkpoints")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CheckpointDto>>> GetCheckpoints(int categoryId, CancellationToken ct) =>
        Ok(await _checkpoints.GetByCategoryAsync(categoryId, ct));

    [HttpPost("categories/{categoryId:int}/checkpoints")]
    public async Task<ActionResult<CheckpointDto>> CreateCheckpoint(int categoryId, UpsertCheckpointRequest request,
        CancellationToken ct)
    {
        var created = await _checkpoints.CreateAsync(categoryId, request, ct);
        return CreatedAtAction(nameof(GetCheckpoints), new { categoryId }, created);
    }

    [HttpPut("checkpoints/{id:int}")]
    public async Task<ActionResult<CheckpointDto>> UpdateCheckpoint(int id, UpsertCheckpointRequest request,
        CancellationToken ct) => Ok(await _checkpoints.UpdateAsync(id, request, ct));

    [HttpDelete("checkpoints/{id:int}")]
    public async Task<IActionResult> DeleteCheckpoint(int id, CancellationToken ct)
    {
        await _checkpoints.DeleteAsync(id, ct);
        return NoContent();
    }
}
