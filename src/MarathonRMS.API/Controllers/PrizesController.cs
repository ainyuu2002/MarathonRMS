using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-28 – cơ cấu giải thưởng theo cự ly; UC-65 – danh sách nhận giải / Vinh danh (BR-29).</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class PrizesController : ControllerBase
{
    private readonly IPrizeService _prizes;
    public PrizesController(IPrizeService prizes) => _prizes = prizes;

    [HttpGet("events/{eventId:int}/prizes")]
    public async Task<ActionResult<EventPrizesDto>> GetForEvent(int eventId, CancellationToken ct) =>
        Ok(await _prizes.GetForEventAsync(eventId, ct));

    [HttpPost("categories/{categoryId:int}/prizes")]
    public async Task<ActionResult<PrizeDto>> Create(int categoryId, SavePrizeRequest request, CancellationToken ct) =>
        Ok(await _prizes.CreateAsync(categoryId, request, ct));

    [HttpPut("prizes/{prizeId:int}")]
    public async Task<ActionResult<PrizeDto>> Update(int prizeId, SavePrizeRequest request, CancellationToken ct) =>
        Ok(await _prizes.UpdateAsync(prizeId, request, ct));

    [HttpDelete("prizes/{prizeId:int}")]
    public async Task<IActionResult> Delete(int prizeId, CancellationToken ct)
    {
        await _prizes.DeleteAsync(prizeId, ct);
        return NoContent();
    }

    /// <summary>Vinh danh: công khai khi kết quả chính thức; BTC xem trước khi chưa chính thức.</summary>
    [HttpGet("events/{eventId:int}/winners")]
    [AllowAnonymous]
    public async Task<ActionResult<EventWinnersDto>> GetWinners(int eventId, CancellationToken ct) =>
        Ok(await _prizes.GetWinnersAsync(eventId, ct));
}
