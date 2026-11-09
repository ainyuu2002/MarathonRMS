using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>Phần A – UC-47: yêu cầu chuyển nhượng đã gửi và nhận; từ chối / hủy.</summary>
public class TransfersModel : BasePageModel
{
    private readonly RegistrationChangeApiClient _api;
    public TransfersModel(RegistrationChangeApiClient api) => _api = api;

    public List<TransferDto> Items { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Items = await _api.GetMyTransfersAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int transferId, CancellationToken ct)
    {
        try
        {
            await _api.RejectAsync(transferId, ct);
            StatusMessage = "Đã từ chối yêu cầu chuyển nhượng.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await OnGetAsync(ct); }
    }

    public async Task<IActionResult> OnPostCancelAsync(int transferId, CancellationToken ct)
    {
        try
        {
            await _api.CancelAsync(transferId, ct);
            StatusMessage = "Đã hủy yêu cầu chuyển nhượng.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await OnGetAsync(ct); }
    }
}
