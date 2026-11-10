using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>Phần A – UC-46 đổi cự ly (chênh lệch + phí, thanh toán thử), UC-47 gửi yêu cầu chuyển nhượng BIB.</summary>
public class RegistrationChangeModel : BasePageModel
{
    private readonly RegistrationChangeApiClient _api;
    public RegistrationChangeModel(RegistrationChangeApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public CreateTransferRequest TransferInput { get; set; } = new();
    public RegistrationChangeOptionsDto Options { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Options = await _api.GetOptionsAsync(Id, ct); }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return NotFound(); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostChangeAsync(int newCategoryId, decimal expectedTotal, CancellationToken ct)
    {
        ModelState.Clear();
        try
        {
            var result = await _api.ChangeCategoryAsync(Id, new ChangeCategoryRequest { NewCategoryId = newCategoryId, ExpectedTotal = expectedTotal }, ct);
            StatusMessage = $"Đã đổi sang cự ly {result.NewCategoryName}" +
                            (result.AmountPaid > 0 ? $", thanh toán {result.AmountPaid.ToString("N0", VnFormat.Culture)}đ (mã {result.TxnRef})" : "") +
                            (result.NewBibNumber is int bib ? $". BIB mới: {bib}." : ".");
            return RedirectToPage("/Member/Registrations");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await OnGetAsync(ct); }
    }

    public async Task<IActionResult> OnPostTransferAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await OnGetAsync(ct);
        try
        {
            var t = await _api.CreateTransferAsync(Id, TransferInput, ct);
            StatusMessage = $"Đã gửi yêu cầu chuyển nhượng tới {t.ToName} ({t.ToEmail}). Suất được chuyển khi người nhận chấp nhận.";
            return RedirectToPage("/Member/Transfers");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(TransferInput)) ?? await OnGetAsync(ct); }
    }

    public async Task<IActionResult> OnPostCancelTransferAsync(int transferId, CancellationToken ct)
    {
        ModelState.Clear();
        try
        {
            await _api.CancelAsync(transferId, ct);
            StatusMessage = "Đã hủy yêu cầu chuyển nhượng.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await OnGetAsync(ct); }
    }
}
