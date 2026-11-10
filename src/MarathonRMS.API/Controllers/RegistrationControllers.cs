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
