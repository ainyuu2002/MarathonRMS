using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Payments;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Services.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

[ApiController]
[Route("api/registrations")]
[Authorize(Policy = AppPolicies.MemberOnly)]
public class RegistrationsController : ControllerBase
{
    private readonly IRegistrationService _registrations;
    private readonly ICategoryChangeService _categoryChanges;
    private readonly ITransferService _transfers;
    public RegistrationsController(IRegistrationService registrations, ICategoryChangeService categoryChanges, ITransferService transfers)
    { _registrations = registrations; _categoryChanges = categoryChanges; _transfers = transfers; }

    [HttpPost]
    public async Task<ActionResult<MyRegistrationDto>> Create(CreateRegistrationRequest request, CancellationToken ct)
    {
        var created = await _registrations.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetMine), created);
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<MyRegistrationDto>>> GetMine(CancellationToken ct) =>
        Ok(await _registrations.GetMineAsync(ct));

    [HttpPut("{id:int}/info")]
    public async Task<ActionResult<MyRegistrationDto>> UpdateInfo(int id, UpdateRegistrationInfoRequest request,
        CancellationToken ct) => Ok(await _registrations.UpdateInfoAsync(id, request, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _registrations.CancelAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/change-category")]
    public async Task<ActionResult<CategoryChangeResultDto>> ChangeCategory(int id, ChangeCategoryRequest request, CancellationToken ct) =>
        Ok(await _categoryChanges.ChangeAsync(id, request, ct));

    [HttpPost("{id:int}/transfers")]
    public async Task<ActionResult<TransferDto>> CreateTransfer(int id, CreateTransferRequest request, CancellationToken ct) =>
        Ok(await _transfers.CreateAsync(id, request, ct));
}

[ApiController]
[Route("api/transfers")]
[Authorize(Policy = AppPolicies.MemberOnly)]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _service; public TransfersController(ITransferService service) => _service=service;
    [HttpGet("incoming")] public async Task<ActionResult<IReadOnlyList<TransferDto>>> Incoming(CancellationToken ct)=>Ok(await _service.GetIncomingAsync(ct));
    [HttpGet("outgoing")] public async Task<ActionResult<IReadOnlyList<TransferDto>>> Outgoing(CancellationToken ct)=>Ok(await _service.GetOutgoingAsync(ct));
    [HttpPost("{id:int}/accept")] public async Task<ActionResult<TransferDto>> Accept(int id,AcceptTransferRequest request,CancellationToken ct)=>Ok(await _service.AcceptAsync(id,request,ct));
    [HttpPost("{id:int}/reject")] public async Task<IActionResult> Reject(int id,CancellationToken ct){await _service.RejectAsync(id,ct);return NoContent();}
    [HttpPost("{id:int}/cancel")] public async Task<IActionResult> Cancel(int id,CancellationToken ct){await _service.CancelAsync(id,ct);return NoContent();}
}

[ApiController]
[Route("api")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class RefundsController : ControllerBase
{
    private readonly IRefundService _service; public RefundsController(IRefundService service)=>_service=service;
    [HttpGet("events/{eventId:int}/refunds")] public async Task<ActionResult<IReadOnlyList<RefundDto>>> Get(int eventId,CancellationToken ct)=>Ok(await _service.GetByEventAsync(eventId,ct));
    [HttpPost("refunds/{id:int}/complete")] public async Task<IActionResult> Complete(int id,CompleteRefundRequest request,CancellationToken ct){await _service.CompleteAsync(id,request,ct);return NoContent();}
    [HttpPost("refunds/{id:int}/reject")] public async Task<IActionResult> Reject(int id,RejectRefundRequest request,CancellationToken ct){await _service.RejectAsync(id,request,ct);return NoContent();}
}

[ApiController]
[Route("api/organizer/registrations")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class OrganizerRegistrationsController : ControllerBase
{
    private readonly IRegistrationService _registrations;
    public OrganizerRegistrationsController(IRegistrationService registrations) => _registrations = registrations;

    [HttpPut("{id:int}/locked-info")]
    public async Task<IActionResult> UpdateLockedInfo(int id, UpdateLockedInfoRequest request, CancellationToken ct)
    {
        await _registrations.UpdateLockedInfoAsync(id, request, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/payments")]
[Authorize(Policy = AppPolicies.MemberOnly)]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;
    public PaymentsController(IPaymentService payments) => _payments = payments;

    [HttpPost]
    public async Task<ActionResult<PaymentResultDto>> Create(CreatePaymentRequest request, CancellationToken ct) =>
        Ok(await _payments.CreateAsync(request, ct));
}
