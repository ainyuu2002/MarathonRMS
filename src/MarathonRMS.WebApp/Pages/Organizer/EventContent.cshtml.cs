using MarathonRMS.DTOs.Coupons;
using MarathonRMS.DTOs.EventContent;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer;

/// <summary>UC-88 – soạn trang giới thiệu giải: màu chủ đạo, khối nội dung (sắp xếp được), lịch trình.</summary>
public class EventContentModel : BasePageModel
{
    private readonly EventContentApiClient _content;
    private readonly CouponApiClient _coupons;
    public EventContentModel(EventContentApiClient content, CouponApiClient coupons)
    {
        _content = content;
        _coupons = coupons;
    }

    [BindProperty(SupportsGet = true)] public int? EventId { get; set; }
    /// <summary>Khối đang sửa (0 = thêm mới).</summary>
    [BindProperty(SupportsGet = true)] public int? Section { get; set; }
    /// <summary>Mục lịch trình đang sửa (0 = thêm mới).</summary>
    [BindProperty(SupportsGet = true)] public int? Item { get; set; }
    [BindProperty(SupportsGet = true)] public bool Preview { get; set; }

    [BindProperty] public UpsertContentSectionRequest SectionInput { get; set; } = new();
    [BindProperty] public UpsertScheduleItemRequest ScheduleInput { get; set; } = new();
    [BindProperty] public UpdateThemeRequest ThemeInput { get; set; } = new();

    public EventContentDto? Data { get; private set; }
    /// <summary>Danh sách giải của BTC để chọn (B có thể thay bằng liên kết từ trang quản lý giải).</summary>
    public List<CouponEventOptionDto> Events { get; private set; } = new();

    public static readonly (string Value, string Label)[] SectionTypes =
        { ("Intro", "Giới thiệu"), ("Schedule", "Lịch trình (kèm bảng giờ)"), ("Faq", "Hỏi đáp"), ("Custom", "Tùy chỉnh") };

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            if (Data is null) return Page();
            ThemeInput.ThemeColor = Data.ThemeColor;
            if (Section is int sid && sid > 0 && Data.Sections.FirstOrDefault(s => s.Id == sid) is { } s)
                SectionInput = new UpsertContentSectionRequest { SectionType = s.SectionType, Title = s.Title, BodyHtml = s.BodyHtml, IsVisible = s.IsVisible };
            if (Item is int iid && iid > 0 && Data.Schedule.FirstOrDefault(i => i.Id == iid) is { } item)
                ScheduleInput = new UpsertScheduleItemRequest { StartsAt = item.StartsAt, EndsAt = item.EndsAt, Title = item.Title, Location = item.Location, Note = item.Note };
            else if (Item == 0)
                ScheduleInput = new UpsertScheduleItemRequest { StartsAt = DateTime.Today.AddHours(5) };
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostThemeAsync(CancellationToken ct)
    {
        ClearExcept(nameof(ThemeInput));
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        return await RunAsync(() => _content.UpdateThemeAsync(EventId!.Value, ThemeInput, ct), "Đã lưu màu chủ đạo.", nameof(ThemeInput), ct);
    }

    public async Task<IActionResult> OnPostSaveSectionAsync(CancellationToken ct)
    {
        ClearExcept(nameof(SectionInput));
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        return await RunAsync(() => Section is int sid && sid > 0
                ? _content.UpdateSectionAsync(EventId!.Value, sid, SectionInput, ct)
                : _content.AddSectionAsync(EventId!.Value, SectionInput, ct),
            "Đã lưu khối nội dung.", nameof(SectionInput), ct);
    }

    public Task<IActionResult> OnPostDeleteSectionAsync(int id, CancellationToken ct) =>
        RunAsync(() => _content.DeleteSectionAsync(EventId!.Value, id, ct), "Đã xóa khối nội dung.", null, ct);

    /// <summary>Đổi chỗ khối với khối liền trên (dir = -1) hoặc liền dưới (dir = 1).</summary>
    public async Task<IActionResult> OnPostMoveSectionAsync(int id, int dir, CancellationToken ct)
    {
        try
        {
            var current = await _content.GetForManageAsync(EventId!.Value, ct);
            var ids = current.Sections.Select(s => s.Id).ToList();
            var index = ids.IndexOf(id);
            var target = index + Math.Sign(dir);
            if (index >= 0 && target >= 0 && target < ids.Count)
            {
                (ids[index], ids[target]) = (ids[target], ids[index]);
                await _content.ReorderSectionsAsync(EventId.Value, new ReorderSectionsRequest { SectionIds = ids }, ct);
            }
            return RedirectToPage(new { EventId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostSaveScheduleAsync(CancellationToken ct)
    {
        ClearExcept(nameof(ScheduleInput));
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        return await RunAsync(() => Item is int iid && iid > 0
                ? _content.UpdateScheduleItemAsync(EventId!.Value, iid, ScheduleInput, ct)
                : _content.AddScheduleItemAsync(EventId!.Value, ScheduleInput, ct),
            "Đã lưu lịch trình.", nameof(ScheduleInput), ct);
    }

    public Task<IActionResult> OnPostDeleteScheduleAsync(int id, CancellationToken ct) =>
        RunAsync(() => _content.DeleteScheduleItemAsync(EventId!.Value, id, ct), "Đã xóa mục lịch trình.", null, ct);

    // ---------- Hỗ trợ ----------

    private async Task<IActionResult> RunAsync(Func<Task> action, string message, string? prefix, CancellationToken ct)
    {
        if (EventId is null) return RedirectToPage();
        try
        {
            await action();
            StatusMessage = message;
            return RedirectToPage(new { EventId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, prefix) ?? await ReloadAsync(ct); }
    }

    /// <summary>Mỗi form chỉ post một nhóm input – bỏ lỗi validation của các nhóm còn lại.</summary>
    private void ClearExcept(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(k => !k.StartsWith(prefix + ".") && k != prefix).ToList())
            ModelState.Remove(key);
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
        if (EventId is int id) Data = await _content.GetForManageAsync(id, ct);
    }
}
