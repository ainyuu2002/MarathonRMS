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
    public RegistrationsController(IRegistrationService registrations) => _registrations = registrations;

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
