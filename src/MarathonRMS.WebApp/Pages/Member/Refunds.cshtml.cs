using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>Phần A – UC-50: hoàn tiền của tôi khi giải bị hủy, khai tài khoản nhận tiền.</summary>
public class RefundsModel : BasePageModel
{
    private readonly EventLifecycleApiClient _api;
    public RefundsModel(EventLifecycleApiClient api) => _api = api;

    public List<RefundDto> Items { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Items = await _api.GetMyRefundsAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostBankAsync(int refundId, string? bankInfo, CancellationToken ct)
    {
        try
        {
            await _api.UpdateBankInfoAsync(refundId, new UpdateRefundBankInfoRequest { BankInfo = bankInfo ?? "" }, ct);
            StatusMessage = "Đã lưu thông tin nhận tiền.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await OnGetAsync(ct); }
    }
}
