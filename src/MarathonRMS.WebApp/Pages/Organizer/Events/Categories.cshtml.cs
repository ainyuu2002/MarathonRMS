using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-21 – danh sách và form tạo/sửa cự ly của giải.</summary>
public class CategoriesModel : BasePageModel
{
    private readonly EventApiClient _events;
    public CategoriesModel(EventApiClient events) => _events = events;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty] public UpsertRaceCategoryRequest Input { get; set; } = new();
    public EventDetailDto Event { get; private set; } = new();
    public List<RaceCategoryDto> Categories { get; private set; } = new();
    public bool IsEditing => CategoryId.HasValue;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            if (CategoryId is int categoryId)
            {
                var category = Categories.FirstOrDefault(c => c.Id == categoryId);
                if (category is null) return NotFound();
                Input = ToRequest(category);
            }
            else
            {
                Input = new UpsertRaceCategoryRequest
                {
                    Capacity = 500,
                    StartAt = Event.EventDate.ToDateTime(new TimeOnly(5, 0)),
                    CutOffMinutes = 180,
                    MinAge = 16,
                    IsTimed = true,
                    IsRanked = true,
                    BibFrom = NextBibFrom(),
                    BibTo = NextBibFrom() + 999
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
            _ = CategoryId is int categoryId
                ? await _events.UpdateCategoryAsync(categoryId, Input, ct)
                : await _events.CreateCategoryAsync(Id, Input, ct);
            StatusMessage = CategoryId is null ? "Đã thêm cự ly." : "Đã cập nhật cự ly.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int categoryId, CancellationToken ct)
    {
        try
        {
            await _events.DeleteCategoryAsync(categoryId, ct);
            StatusMessage = "Đã xóa cự ly.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex)
        {
            var redirect = await HandleApiErrorAsync(ex);
            return redirect ?? await ReloadAsync(ct);
        }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Event = await _events.GetAsync(Id, ct);
        Categories = await _events.GetCategoriesAsync(Id, ct);
    }

    private int NextBibFrom()
    {
        if (Categories.Count == 0) return 1000;
        var next = ((Categories.Max(c => c.BibTo) / 1000) + 1) * 1000;
        return Math.Min(next, 998_000);
    }

    private static UpsertRaceCategoryRequest ToRequest(RaceCategoryDto category) => new()
    {
        Name = category.Name,
        Discipline = category.Discipline,
        CategoryType = category.CategoryType,
        DistanceKm = category.DistanceKm,
        ActualDistanceKm = category.ActualDistanceKm,
        ElevationGainM = category.ElevationGainM,
        ItraPoints = category.ItraPoints,
        Capacity = category.Capacity,
        StartAt = category.StartAt,
        CutOffMinutes = category.CutOffMinutes,
        MinAge = category.MinAge,
        MaxAge = category.MaxAge,
        EscortRequiredUnderAge = category.EscortRequiredUnderAge,
        IsTimed = category.IsTimed,
        IsRanked = category.IsRanked,
        BibFrom = category.BibFrom,
        BibTo = category.BibTo,
        BibColor = category.BibColor,
        MedalImageUrl = category.MedalImageUrl,
        RowVersion = category.RowVersion
    };
}
