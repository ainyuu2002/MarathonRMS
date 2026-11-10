using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-22 – quản lý mức giá của một cự ly.</summary>
public class PriceTiersModel : BasePageModel
{
    private readonly EventApiClient _events;
    public PriceTiersModel(EventApiClient events) => _events = events;

    [BindProperty(SupportsGet = true)] public int EventId { get; set; }
    [BindProperty(SupportsGet = true)] public int CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public int? PriceTierId { get; set; }
    [BindProperty] public UpsertPriceTierRequest Input { get; set; } = new();
    public EventDetailDto Event { get; private set; } = new();
    public RaceCategoryDto Category { get; private set; } = new();
    public List<PriceTierDto> PriceTiers { get; private set; } = new();
    /// <summary>UC-87 – khoảng giá tham khảo (phần A); null nếu không lấy được.</summary>
    public PriceReferenceDto? Reference { get; private set; }
    public bool IsEditing => PriceTierId.HasValue;
    public bool CoverageComplete => CoversWindow();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            if (PriceTierId is int id)
            {
                var tier = PriceTiers.FirstOrDefault(t => t.Id == id);
                if (tier is null) return NotFound();
                Input = new UpsertPriceTierRequest
                {
                    Name = tier.Name, Price = tier.Price, ValidFrom = tier.ValidFrom, ValidTo = tier.ValidTo
                };
            }
            else
            {
                var from = PriceTiers.Count == 0 ? Event.RegistrationOpenAt : PriceTiers.Max(t => t.ValidTo);
                Input = new UpsertPriceTierRequest
                {
                    Name = PriceTiers.Count == 0 ? "Early Bird" : "Mức tiếp theo",
                    ValidFrom = from,
                    ValidTo = Event.RegistrationCloseAt
                };
            }
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            var saved = PriceTierId is int id
                ? await _events.UpdatePriceTierAsync(id, Input, ct)
                : await _events.CreatePriceTierAsync(CategoryId, Input, ct);
            StatusMessage = PriceTierId is null ? "Đã thêm mức giá." : "Đã cập nhật mức giá.";
            if (saved.Warnings.Count > 0) StatusMessage += " Cảnh báo: " + string.Join(" ", saved.Warnings);
            return RedirectToPage(new { eventId = EventId, categoryId = CategoryId });
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        try
        {
            await _events.DeletePriceTierAsync(id, ct);
            StatusMessage = "Đã xóa mức giá.";
            return RedirectToPage(new { eventId = EventId, categoryId = CategoryId });
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
        var ev = await _events.GetAsync(EventId, ct);
        EnsureOwnOrganization(ev.OrganizationId);
        Event = ev;
        Category = Event.Categories.FirstOrDefault(c => c.Id == CategoryId) ?? throw new ApiException(
            System.Net.HttpStatusCode.NotFound, "Không tìm thấy cự ly.");
        PriceTiers = await _events.GetPriceTiersAsync(CategoryId, ct);
        // UC-87: gợi ý giá – lỗi thì bỏ qua, không chặn trang
        try { Reference = await _events.GetPriceReferenceAsync(Category.Discipline, Category.DistanceKm, EventId, ct); }
        catch (ApiException) { Reference = null; }
    }

    private bool CoversWindow()
    {
        var ordered = PriceTiers.OrderBy(t => t.ValidFrom).ToList();
        if (ordered.Count == 0 || ordered[0].ValidFrom != Event.RegistrationOpenAt || ordered[^1].ValidTo != Event.RegistrationCloseAt)
            return false;
        return ordered.Zip(ordered.Skip(1), (a, b) => a.ValidTo == b.ValidFrom).All(x => x);
    }
}
