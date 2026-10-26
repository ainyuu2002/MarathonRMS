using MarathonRMS.DTOs.Admin;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.SuperAdmin;

/// <summary>UC-15 – cấu hình hệ thống.</summary>
public class SettingsModel : BasePageModel
{
    private readonly SuperAdminApiClient _superAdmin;
    public SettingsModel(SuperAdminApiClient superAdmin) => _superAdmin = superAdmin;

    public List<SystemSettingDto> Settings { get; private set; } = new();
    /// <summary>Khóa vừa lỗi + giá trị người dùng nhập, để giữ lại trên ô nhập.</summary>
    public string? FailedKey { get; private set; }
    public string? FailedValue { get; private set; }

    /// <summary>Nhóm theo tiền tố khóa ("Payment", "PolicyDefault"…).</summary>
    public IEnumerable<IGrouping<string, SystemSettingDto>> Groups => Settings.GroupBy(s => s.Key.Split('.')[0]);

    public static string GroupLabel(string prefix) => prefix switch
    {
        "Payment" => "Thanh toán",
        "Price" => "Giới hạn giá",
        "Fee" => "Giới hạn phí",
        "Media" => "Ảnh tải lên",
        "Privacy" => "Quyền riêng tư",
        "Platform" => "Nền tảng",
        "PolicyDefault" => "Mặc định chính sách cho giải mới",
        "Coin" => "Loyalty Coin",
        _ => prefix
    };

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Settings = await _superAdmin.GetSettingsAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string key, string value, CancellationToken ct)
    {
        try
        {
            var updated = await _superAdmin.UpdateSettingAsync(key, new UpdateSettingRequest { Value = value ?? "" }, ct);
            StatusMessage = $"Đã lưu {updated.Key} = {updated.Value}.";
            return RedirectToPage();
        }
        catch (ApiException ex)
        {
            if (await HandleApiErrorAsync(ex) is { } redirect) return redirect;
            ModelState.Clear(); // chỉ giữ thông báo có kèm tên khóa
            ModelState.AddModelError(string.Empty, $"{key}: {ex.Message}");
            (FailedKey, FailedValue) = (key, value);
            Settings = await _superAdmin.GetSettingsAsync(ct);
            return Page();
        }
    }
}
