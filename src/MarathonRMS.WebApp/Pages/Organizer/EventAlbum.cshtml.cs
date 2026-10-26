using MarathonRMS.DTOs.Coupons;
using MarathonRMS.DTOs.EventContent;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer;

/// <summary>UC-89 – album ảnh của giải: tải nhiều ảnh, sửa chú thích / thứ tự, ẩn hiện, xóa (BR-51).</summary>
public class EventAlbumModel : BasePageModel
{
    private readonly EventContentApiClient _content;
    private readonly CouponApiClient _coupons;
    public EventAlbumModel(EventContentApiClient content, CouponApiClient coupons)
    {
        _content = content;
        _coupons = coupons;
    }

    [BindProperty(SupportsGet = true)] public int? EventId { get; set; }
    [BindProperty] public List<IFormFile> Files { get; set; } = new();
    [BindProperty] public string? Caption { get; set; }

    public string EventName { get; private set; } = "";
    public string? UploadSummary { get; private set; }
    public List<EventMediaDto> Media { get; private set; } = new();
    public List<CouponEventOptionDto> Events { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    /// <summary>Tải lần lượt từng ảnh; ảnh lỗi (sai định dạng, quá dung lượng…) được báo riêng, ảnh hợp lệ vẫn được lưu.</summary>
    public async Task<IActionResult> OnPostUploadAsync(CancellationToken ct)
    {
        if (EventId is not int eventId) return RedirectToPage();
        if (Files.Count == 0)
        {
            ModelState.AddModelError(nameof(Files), "Vui lòng chọn ít nhất một ảnh.");
            return await ReloadAsync(ct);
        }

        var saved = 0;
        foreach (var file in Files)
        {
            try
            {
                await _content.UploadMediaAsync(eventId, file, Caption, "Gallery", ct);
                saved++;
            }
            catch (ApiException ex)
            {
                if (await HandleApiErrorAsync(ex) is { } redirect) return redirect;
                // Gắn tên file vào lỗi vừa thêm
                var last = ModelState[string.Empty]?.Errors.LastOrDefault();
                if (last is not null)
                {
                    ModelState[string.Empty]!.Errors.Remove(last);
                    ModelState.AddModelError(string.Empty, $"{file.FileName}: {last.ErrorMessage}");
                }
            }
        }

        if (ModelState.ErrorCount == 0)
        {
            StatusMessage = $"Đã tải lên {saved} ảnh.";
            return RedirectToPage(new { EventId });
        }
        // Trả trang (không redirect) để giữ lỗi từng file ⇒ báo số ảnh đã lưu ngay trên trang
        if (saved > 0) UploadSummary = $"Đã tải lên {saved}/{Files.Count} ảnh. Các ảnh dưới đây chưa được lưu:";
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, string? caption, bool isVisible, int displayOrder, CancellationToken ct)
    {
        try
        {
            await _content.UpdateMediaAsync(EventId!.Value, id, new UpdateMediaRequest { Caption = caption, IsVisible = isVisible, DisplayOrder = displayOrder }, ct);
            StatusMessage = "Đã cập nhật ảnh.";
            return RedirectToPage(new { EventId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        try
        {
            await _content.DeleteMediaAsync(EventId!.Value, id, ct);
            StatusMessage = "Đã xóa ảnh.";
            return RedirectToPage(new { EventId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Events = await _coupons.GetEventOptionsAsync(ct);
        if (EventId is not int id) return;
        EventName = Events.FirstOrDefault(e => e.EventId == id)?.EventName ?? "";
        Media = await _content.GetMediaAsync(id, "Gallery", ct);
    }
}
