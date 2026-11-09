using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Payments;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Services.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-39, 43, 44, 45 (Member) và UC-48 sửa trường đã khóa (Organizer).</summary>
[ApiController]
[Route("api/registrations")]
[Authorize(Policy = AppPolicies.MemberOnly)]
public class RegistrationApiController : ControllerBase
{
    private readonly IRegistrationService _registrations;
    public RegistrationApiController(IRegistrationService registrations) => _registrations = registrations;

    [HttpPost]
    public async Task<ActionResult<MyRegistrationDto>> Create(CreateRegistrationRequest request, CancellationToken ct)
    {
        var created = await _registrations.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetMine), null, created);
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<MyRegistrationDto>>> GetMine(CancellationToken ct) =>
        Ok(await _registrations.GetMineAsync(ct));

    [HttpPut("{id:int}/info")]
    public async Task<ActionResult<MyRegistrationDto>> UpdateInfo(int id, UpdateRegistrationInfoRequest request, CancellationToken ct) =>
        Ok(await _registrations.UpdateInfoAsync(id, request, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _registrations.CancelAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/locked-info")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<IActionResult> UpdateLockedInfo(int id, UpdateLockedInfoRequest request, CancellationToken ct)
    {
        await _registrations.UpdateLockedInfoAsync(id, request, ct);
        return NoContent();
    }
}

/// <summary>UC-41 – thanh toán Mock.</summary>
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

/// <summary>
/// UC-48 – danh sách đăng ký của BTC: GET /odata/Registrations?$filter=EventId eq 3 and Status eq 'Confirmed'&amp;$orderby=RegisteredAt desc&amp;$count=true
/// Chỉ đăng ký thuộc giải của BTC người gọi (BR-30). Không có dữ liệu y tế.
/// </summary>
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class RegistrationsController : ODataController
{
    private readonly IRegistrationService _registrations;
    public RegistrationsController(IRegistrationService registrations) => _registrations = registrations;

    [EnableQuery(PageSize = 100, MaxTop = 100,
        AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy | AllowedQueryOptions.Top
                              | AllowedQueryOptions.Skip | AllowedQueryOptions.Count | AllowedQueryOptions.Select)]
    public IQueryable<RegistrationListItemDto> Get() => _registrations.QueryForOrganizer();
}
