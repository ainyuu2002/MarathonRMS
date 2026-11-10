using MarathonRMS.DTOs.Payments;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

public class CheckoutModel : BasePageModel
{
    private readonly RegistrationApiClient _registrations;
    public CheckoutModel(RegistrationApiClient registrations) => _registrations = registrations;

    [BindProperty(SupportsGet = true)] public int RegistrationId { get; set; }
    public MyRegistrationDto Registration { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => await LoadPageAsync(ct);

    public async Task<IActionResult> OnPostPayAsync(CancellationToken ct)
    {
        try
        {
            await _registrations.PayAsync(new CreatePaymentRequest
            {
                RegistrationId = RegistrationId, Purpose = "Registration", Provider = "Mock"
            }, ct);
            StatusMessage = "Thanh toán Mock thành công. Đăng ký đã được xác nhận.";
            return RedirectToPage(new { registrationId = RegistrationId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await LoadPageAsync(ct); }
    }

    private async Task<IActionResult> LoadPageAsync(CancellationToken ct)
    {
        try
        {
            Registration = (await _registrations.GetMineAsync(ct)).FirstOrDefault(r => r.Id == RegistrationId)
                ?? throw new ApiException(System.Net.HttpStatusCode.NotFound, "Không tìm thấy đăng ký.");
            return Page();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
    }
}
