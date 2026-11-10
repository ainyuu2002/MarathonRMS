using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Services.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>Phần A – UC-46 đổi cự ly, UC-47 chuyển nhượng BIB (Member, chủ đăng ký / người nhận).</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = AppPolicies.MemberOnly)]
public class RegistrationChangeController : ControllerBase
{
    private readonly IRegistrationChangeService _changes;
    public RegistrationChangeController(IRegistrationChangeService changes) => _changes = changes;

    [HttpGet("registrations/{id:int}/change-options")]
    public async Task<ActionResult<RegistrationChangeOptionsDto>> GetOptions(int id, CancellationToken ct) =>
        Ok(await _changes.GetOptionsAsync(id, ct));

    [HttpPost("registrations/{id:int}/category-change")]
    public async Task<ActionResult<CategoryChangeResultDto>> ChangeCategory(int id, ChangeCategoryRequest request, CancellationToken ct) =>
        Ok(await _changes.ChangeCategoryAsync(id, request, ct));

    [HttpPost("registrations/{id:int}/transfers")]
    public async Task<ActionResult<TransferDto>> CreateTransfer(int id, CreateTransferRequest request, CancellationToken ct)
    {
        var created = await _changes.CreateTransferAsync(id, request, ct);
        return CreatedAtAction(nameof(GetTransfer), new { transferId = created.Id }, created);
    }

    [HttpGet("transfers/me")]
    public async Task<ActionResult<IReadOnlyList<TransferDto>>> GetMine(CancellationToken ct) =>
        Ok(await _changes.GetMyTransfersAsync(ct));

    [HttpGet("transfers/{transferId:int}")]
    public async Task<ActionResult<TransferDto>> GetTransfer(int transferId, CancellationToken ct) =>
        Ok(await _changes.GetTransferAsync(transferId, ct));

    [HttpPost("transfers/{transferId:int}/accept")]
    public async Task<ActionResult<TransferDto>> Accept(int transferId, AcceptTransferRequest request, CancellationToken ct) =>
        Ok(await _changes.AcceptTransferAsync(transferId, request, ct));

    [HttpPost("transfers/{transferId:int}/reject")]
    public async Task<IActionResult> Reject(int transferId, CancellationToken ct)
    {
        await _changes.RejectTransferAsync(transferId, ct);
        return NoContent();
    }

    [HttpPost("transfers/{transferId:int}/cancel")]
    public async Task<IActionResult> Cancel(int transferId, CancellationToken ct)
    {
        await _changes.CancelTransferAsync(transferId, ct);
        return NoContent();
    }
}
