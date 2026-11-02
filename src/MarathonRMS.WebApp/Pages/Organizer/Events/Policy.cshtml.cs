using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-19 – cấu hình chính sách và văn bản miễn trừ của giải.</summary>
public class PolicyModel : BasePageModel
{
    private readonly EventApiClient _events;
    public PolicyModel(EventApiClient events) => _events = events;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public UpdateEventPolicyRequest PolicyInput { get; set; } = new();
    [BindProperty] public UpdateEventWaiverRequest WaiverInput { get; set; } = new();
    public EventDetailDto Event { get; private set; } = new();
    public int WaiverVersion { get; private set; } = 1;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(populatePolicy: true, populateWaiver: true, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostPolicyAsync(CancellationToken ct)
    {
        RemoveModelStatePrefix(nameof(WaiverInput));
        if (!ModelState.IsValid) return await ReloadAsync(populatePolicy: false, populateWaiver: true, ct);
        try
        {
            await _events.UpdatePolicyAsync(Id, PolicyInput, ct);
            StatusMessage = "Đã cập nhật chính sách giải.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(PolicyInput)) ??
                   await ReloadAsync(populatePolicy: false, populateWaiver: true, ct);
        }
    }

    public async Task<IActionResult> OnPostWaiverAsync(CancellationToken ct)
    {
        RemoveModelStatePrefix(nameof(PolicyInput));
        if (!ModelState.IsValid) return await ReloadAsync(populatePolicy: true, populateWaiver: false, ct);
        try
        {
            await _events.UpdateWaiverAsync(Id, WaiverInput, ct);
            StatusMessage = "Đã cập nhật văn bản miễn trừ.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(WaiverInput)) ??
                   await ReloadAsync(populatePolicy: true, populateWaiver: false, ct);
        }
    }

    private async Task<IActionResult> ReloadAsync(bool populatePolicy, bool populateWaiver, CancellationToken ct)
    {
        try { await LoadAsync(populatePolicy, populateWaiver, ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(bool populatePolicy, bool populateWaiver, CancellationToken ct)
    {
        Event = await _events.GetAsync(Id, ct);
        var policy = await _events.GetPolicyAsync(Id, ct);
        var waiver = await _events.GetWaiverAsync(Id, ct);
        WaiverVersion = waiver.WaiverVersion;

        if (populatePolicy) PolicyInput = new UpdateEventPolicyRequest
        {
            AgeCalculationMode = policy.AgeCalculationMode,
            GuardianConsentUnderAge = policy.GuardianConsentUnderAge,
            MaxDependentsPerAccount = policy.MaxDependentsPerAccount,
            InfoEditDeadline = policy.InfoEditDeadline,
            TransferPolicy = policy.TransferPolicy,
            TransferFee = policy.TransferFee,
            MaxTransfers = policy.MaxTransfers,
            TransferDeadline = policy.TransferDeadline,
            CategoryChangeFee = policy.CategoryChangeFee,
            MaxCategoryChanges = policy.MaxCategoryChanges,
            CategoryChangeDeadline = policy.CategoryChangeDeadline,
            LockSpecialEntries = policy.LockSpecialEntries,
            OverallRankingBasis = policy.OverallRankingBasis,
            AgeGroupRankingBasis = policy.AgeGroupRankingBasis,
            TieBreakRule = policy.TieBreakRule,
            LateStartToleranceMinutes = policy.LateStartToleranceMinutes,
            ComplaintWindowMinutes = policy.ComplaintWindowMinutes,
            ComplaintFee = policy.ComplaintFee,
            AwardStackingPolicy = policy.AwardStackingPolicy,
            ForceMajeurePolicy = policy.ForceMajeurePolicy
        };
        if (populateWaiver) WaiverInput = new UpdateEventWaiverRequest { WaiverText = waiver.WaiverText };
    }

    private void RemoveModelStatePrefix(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(k => k == prefix || k.StartsWith($"{prefix}.", StringComparison.Ordinal)).ToList())
            ModelState.Remove(key);
    }
}
