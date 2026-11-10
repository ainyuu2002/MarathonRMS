using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Services.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>Phần A – UC-25 hoãn / hủy giải, UC-50 hoàn tiền.</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class EventLifecycleController : ControllerBase
{
    private readonly IEventLifecycleService _lifecycle;
    private readonly IRefundService _refunds;

    public EventLifecycleController(IEventLifecycleService lifecycle, IRefundService refunds)
    {
        _lifecycle = lifecycle;
        _refunds = refunds;
    }

    [HttpPost("events/{id:int}/postpone")]
    public async Task<ActionResult<EventLifecycleResultDto>> Postpone(int id, PostponeEventRequest request, CancellationToken ct) =>
        Ok(await _lifecycle.PostponeAsync(id, request, ct));

    [HttpPost("events/{id:int}/cancel")]
    public async Task<ActionResult<EventLifecycleResultDto>> Cancel(int id, CancelEventRequest request, CancellationToken ct) =>
        Ok(await _lifecycle.CancelAsync(id, request, ct));

    [HttpGet("events/{id:int}/refunds")]
    public async Task<ActionResult<IReadOnlyList<RefundDto>>> GetRefunds(int id, CancellationToken ct) =>
        Ok(await _refunds.GetByEventAsync(id, ct));

    [HttpPost("refunds/{id:int}/complete")]
    public async Task<ActionResult<RefundDto>> Complete(int id, CompleteRefundRequest request, CancellationToken ct) =>
        Ok(await _refunds.CompleteAsync(id, request, ct));

    [HttpPost("refunds/{id:int}/reject")]
    public async Task<ActionResult<RefundDto>> Reject(int id, RejectRefundRequest request, CancellationToken ct) =>
        Ok(await _refunds.RejectAsync(id, request, ct));
}

/// <summary>Phần A – UC-50: hoàn tiền của tôi (VĐV) và tài khoản nhận tiền.</summary>
[ApiController]
[Route("api/refunds")]
[Authorize(Policy = AppPolicies.MemberOnly)]
public class MyRefundsController : ControllerBase
{
    private readonly IRefundService _refunds;
    public MyRefundsController(IRefundService refunds) => _refunds = refunds;

    /// <summary>Hoàn tiền của tôi (VĐV).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<RefundDto>>> GetMine(CancellationToken ct) => Ok(await _refunds.GetMineAsync(ct));

    [HttpPut("{id:int}/bank-info")]
    public async Task<ActionResult<RefundDto>> UpdateBankInfo(int id, UpdateRefundBankInfoRequest request, CancellationToken ct) =>
        Ok(await _refunds.UpdateBankInfoAsync(id, request, ct));
}
