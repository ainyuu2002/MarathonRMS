using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>UC-43/45 – danh sách đăng ký của tài khoản và hủy đăng ký chưa thanh toán.</summary>
public class RegistrationsModel : BasePageModel
{
    private readonly RegistrationApiClient _registrations;
    public RegistrationsModel(RegistrationApiClient registrations) => _registrations = registrations;

    public List<MyRegistrationDto> Items { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostCancelAsync(int id, CancellationToken ct)
    {
        try
        {
            await _registrations.CancelAsync(id, ct);
            StatusMessage = "Đã hủy đăng ký và trả lại suất giữ chỗ.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await LoadAsync(ct); }
    }

    private async Task<IActionResult> LoadAsync(CancellationToken ct)
    {
        try { Items = await _registrations.GetMineAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
