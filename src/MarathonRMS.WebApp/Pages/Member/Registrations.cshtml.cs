using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>UC-43 đăng ký của tôi, UC-41 thanh toán Mock, UC-45 hủy đăng ký chưa thanh toán.</summary>
public class RegistrationsModel : BasePageModel
{
    private readonly RegistrationApiClient _registrations;
    public RegistrationsModel(RegistrationApiClient registrations) => _registrations = registrations;

    public List<MyRegistrationDto> Items { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Items = await _registrations.GetMineAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(int registrationId, CancellationToken ct)
    {
        try
        {
            var result = await _registrations.PayMockAsync(registrationId, ct);
            StatusMessage = $"Thanh toán thành công {result.Amount:N0}đ (mã giao dịch {result.TxnRef}). Đăng ký đã được xác nhận.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await OnGetAsync(ct); }
    }

    public async Task<IActionResult> OnPostCancelAsync(int registrationId, CancellationToken ct)
    {
        try
        {
            await _registrations.CancelAsync(registrationId, ct);
            StatusMessage = "Đã hủy đăng ký.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await OnGetAsync(ct); }
    }
}
