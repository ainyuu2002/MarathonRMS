using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-23 – quản lý nhóm tuổi của giải.</summary>
public class AgeGroupsModel : BasePageModel
{
    private readonly EventApiClient _events;
    public AgeGroupsModel(EventApiClient events) => _events = events;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public int? AgeGroupId { get; set; }
    [BindProperty] public UpsertAgeGroupRequest Input { get; set; } = new();
    public EventDetailDto Event { get; private set; } = new();
    public List<AgeGroupDto> AgeGroups { get; private set; } = new();
    public bool IsEditing => AgeGroupId.HasValue;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            if (AgeGroupId is int id)
            {
                var group = AgeGroups.FirstOrDefault(g => g.Id == id);
                if (group is null) return NotFound();
                Input = new UpsertAgeGroupRequest
                {
                    CategoryId = group.CategoryId, Name = group.Name, MinAge = group.MinAge, MaxAge = group.MaxAge
                };
            }
            else Input = new UpsertAgeGroupRequest { MinAge = NextMinAge() };
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            _ = AgeGroupId is int id
                ? await _events.UpdateAgeGroupAsync(id, Input, ct)
                : await _events.CreateAgeGroupAsync(Id, Input, ct);
            StatusMessage = AgeGroupId is null ? "Đã thêm nhóm tuổi." : "Đã cập nhật nhóm tuổi.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int ageGroupId, CancellationToken ct)
    {
        try
        {
            await _events.DeleteAgeGroupAsync(ageGroupId, ct);
            StatusMessage = "Đã xóa nhóm tuổi.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    public string CategoryName(int? categoryId) => categoryId is null ? "Mọi cự ly" :
        Event.Categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "Cự ly đã xóa";

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var ev = await _events.GetAsync(Id, ct);
        EnsureOwnOrganization(ev.OrganizationId);
        Event = ev;
        AgeGroups = await _events.GetAgeGroupsAsync(Id, ct);
    }

    private int NextMinAge()
    {
        var global = AgeGroups.Where(g => g.CategoryId is null).ToList();
        return global.Count == 0 ? 16 : (global.Max(g => g.MaxAge) ?? global.Max(g => g.MinAge)) + 1;
    }
}
