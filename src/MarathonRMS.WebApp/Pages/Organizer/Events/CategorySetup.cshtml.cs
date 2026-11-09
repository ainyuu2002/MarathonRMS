using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-22 (mức giá) + UC-24 (checkpoint) của một cự ly.</summary>
public class CategorySetupModel : BasePageModel
{
    private readonly EventApiClient _events;
    public CategorySetupModel(EventApiClient events) => _events = events;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public int CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public int? PriceTierId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CheckpointId { get; set; }
    [TempData] public string? Warning { get; set; }

    public UpsertPriceTierRequest PriceInput { get; set; } = new();
    public UpsertCheckpointRequest CheckpointInput { get; set; } = new();
    public EventDetailDto Event { get; private set; } = new();
    public RaceCategoryDto Category { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            if (!await LoadAsync(ct)) return NotFound();
            PriceInput = PriceTierId is int tierId && Category.PriceTiers.FirstOrDefault(t => t.Id == tierId) is { } tier
                ? new UpsertPriceTierRequest { Name = tier.Name, Price = tier.Price, ValidFrom = tier.ValidFrom, ValidTo = tier.ValidTo }
                : NewPriceInput();
            CheckpointInput = CheckpointId is int cpId && Category.Checkpoints.FirstOrDefault(c => c.Id == cpId) is { } cp
                ? new UpsertCheckpointRequest
                {
                    Name = cp.Name, DistanceKm = cp.DistanceKm, Sequence = cp.Sequence, Type = cp.Type, IsMandatory = cp.IsMandatory,
                    CutOffAt = cp.CutOffAt, StationType = cp.StationType, Latitude = cp.Latitude, Longitude = cp.Longitude
                }
                : NewCheckpointInput();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostSavePriceAsync([Bind(Prefix = nameof(PriceInput))] UpsertPriceTierRequest input, CancellationToken ct)
    {
        PriceInput = input;
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            var saved = PriceTierId is int tierId
                ? await _events.UpdatePriceTierAsync(tierId, input, ct)
                : await _events.CreatePriceTierAsync(CategoryId, input, ct);
            StatusMessage = PriceTierId is null ? "Đã thêm mức giá." : "Đã cập nhật mức giá.";
            if (saved.Warnings.Count > 0) Warning = string.Join(" ", saved.Warnings);
            return RedirectToPage(new { id = Id, categoryId = CategoryId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(PriceInput)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostDeletePriceAsync(int priceTierId, CancellationToken ct)
    {
        try
        {
            await _events.DeletePriceTierAsync(priceTierId, ct);
            StatusMessage = "Đã xóa mức giá.";
            return RedirectToPage(new { id = Id, categoryId = CategoryId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostSaveCheckpointAsync([Bind(Prefix = nameof(CheckpointInput))] UpsertCheckpointRequest input,
        CancellationToken ct)
    {
        CheckpointInput = input;
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            _ = CheckpointId is int cpId
                ? await _events.UpdateCheckpointAsync(cpId, input, ct)
                : await _events.CreateCheckpointAsync(CategoryId, input, ct);
            StatusMessage = CheckpointId is null ? "Đã thêm checkpoint." : "Đã cập nhật checkpoint.";
            return RedirectToPage(new { id = Id, categoryId = CategoryId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(CheckpointInput)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostDeleteCheckpointAsync(int checkpointId, CancellationToken ct)
    {
        try
        {
            await _events.DeleteCheckpointAsync(checkpointId, ct);
            StatusMessage = "Đã xóa checkpoint.";
            return RedirectToPage(new { id = Id, categoryId = CategoryId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    /// <summary>Mức giá đang thiếu để phủ kín thời gian đăng ký (BR-47) – hiện nhắc trên trang.</summary>
    public bool PriceCoverageComplete
    {
        get
        {
            var tiers = Category.PriceTiers.OrderBy(t => t.ValidFrom).ToList();
            if (tiers.Count == 0 || tiers[0].ValidFrom != Event.RegistrationOpenAt || tiers[^1].ValidTo != Event.RegistrationCloseAt)
                return false;
            return tiers.Zip(tiers.Skip(1), (a, b) => a.ValidTo == b.ValidFrom).All(x => x);
        }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task<bool> LoadAsync(CancellationToken ct)
    {
        Event = await _events.GetAsync(Id, ct);
        var category = Event.Categories.FirstOrDefault(c => c.Id == CategoryId);
        if (category is null) return false;
        Category = category;
        return true;
    }

    /// <summary>Mức giá mới nối tiếp mức giá cuối, kéo tới hết thời gian đăng ký.</summary>
    private UpsertPriceTierRequest NewPriceInput()
    {
        var last = Category.PriceTiers.OrderBy(t => t.ValidTo).LastOrDefault();
        return new UpsertPriceTierRequest
        {
            Name = last is null ? "Early Bird" : "Regular",
            Price = last?.Price ?? 500_000,
            ValidFrom = last?.ValidTo ?? Event.RegistrationOpenAt,
            ValidTo = Event.RegistrationCloseAt
        };
    }

    private UpsertCheckpointRequest NewCheckpointInput()
    {
        var list = Category.Checkpoints;
        if (list.Count == 0)
            return new UpsertCheckpointRequest { Name = "Xuất phát", Type = "Start", Sequence = 1, DistanceKm = 0 };
        // Finish đặt thứ tự 100 để còn chỗ chèn các trạm ở giữa
        if (!list.Any(c => c.Type == "Finish"))
            return new UpsertCheckpointRequest { Name = "Về đích", Type = "Finish", Sequence = 100, DistanceKm = Category.DistanceKm };
        var used = list.Select(c => c.Sequence).ToHashSet();
        var nextFree = Enumerable.Range(1, 100).FirstOrDefault(n => !used.Contains(n) && list.Any(c => c.Sequence < n));
        return new UpsertCheckpointRequest
        {
            Name = $"CP{list.Count(c => c.Type == "Split") + 1}", Type = "Split", Sequence = nextFree == 0 ? 2 : nextFree,
            StationType = "Water", DistanceKm = list.Where(c => c.Type != "Finish").Max(c => c.DistanceKm) + 1
        };
    }
}
