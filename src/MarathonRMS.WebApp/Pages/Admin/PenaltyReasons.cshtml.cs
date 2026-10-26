using MarathonRMS.DTOs.Admin;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Admin;

/// <summary>UC-85 – danh mục lý do phạt / DSQ.</summary>
public class PenaltyReasonsModel : BasePageModel
{
    private readonly AdminApiClient _admin;
    public PenaltyReasonsModel(AdminApiClient admin) => _admin = admin;

    /// <summary>Mã lý do đang sửa; "new" = thêm mới.</summary>
    [BindProperty(SupportsGet = true)] public string? Edit { get; set; }
    [BindProperty] public UpsertPenaltyReasonRequest Input { get; set; } = new();

    public List<PenaltyReasonAdminDto> Reasons { get; private set; } = new();
    public bool IsNew => Edit == "new";
    public bool ShowForm => Edit is not null;

    public static readonly (string Value, string Label)[] Actions =
        { ("DSQ", "Truất quyền (DSQ)"), ("TimePenalty", "Cộng thời gian"), ("Warning", "Cảnh cáo") };

    public static string ActionLabel(string value) => Actions.FirstOrDefault(a => a.Value == value).Label ?? value;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Reasons = await _admin.GetPenaltyReasonsAsync(ct);
            if (Edit is { } code && !IsNew && Reasons.FirstOrDefault(r => r.Code == code) is { } r)
                Input = new UpsertPenaltyReasonRequest
                {
                    Code = r.Code, Name = r.Name, DefaultAction = r.DefaultAction, DefaultMinutes = r.DefaultMinutes, IsActive = r.IsActive
                };
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        if (!IsNew && Edit is not null)
        {
            Input.Code = Edit; // mã không đổi được sau khi tạo
            ModelState.Remove("Input.Code");
        }
        if (Input.DefaultAction != "TimePenalty")
        {
            Input.DefaultMinutes = null;
            ModelState.Remove("Input.DefaultMinutes");
        }
        if (ModelState.IsValid)
        {
            try
            {
                var saved = IsNew ? await _admin.CreatePenaltyReasonAsync(Input, ct) : await _admin.UpdatePenaltyReasonAsync(Edit!, Input, ct);
                StatusMessage = $"Đã lưu lý do {saved.Code}.";
                return RedirectToPage();
            }
            catch (ApiException ex)
            {
                if (await HandleApiErrorAsync(ex, nameof(Input)) is { } redirect) return redirect;
            }
        }
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string code, CancellationToken ct)
    {
        try
        {
            await _admin.DeletePenaltyReasonAsync(code, ct);
            StatusMessage = $"Đã xóa lý do {code}.";
            return RedirectToPage();
        }
        catch (ApiException ex)
        {
            if (await HandleApiErrorAsync(ex) is { } redirect) return redirect;
            return await ReloadAsync(ct);
        }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { Reasons = await _admin.GetPenaltyReasonsAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }
}
