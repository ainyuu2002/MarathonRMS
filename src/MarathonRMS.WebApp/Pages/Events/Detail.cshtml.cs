using System.Net;
using MarathonRMS.DTOs.EventContent;
using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Events;

/// <summary>UC-35 – chi tiết giải công khai.</summary>
public class DetailModel : BasePageModel
{
    private readonly EventApiClient _events;
    private readonly EventContentApiClient _content;

    public DetailModel(EventApiClient events, EventContentApiClient content)
    {
        _events = events;
        _content = content;
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Slug { get; set; }
    public EventDetailDto Event { get; private set; } = new();
    public EventContentDto PublicContent { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Event = await _events.GetAsync(Id, ct);
            PublicContent = await _content.GetPublicAsync(Id, ct);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return NotFound(); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public static string PriceText(decimal? price) => price switch
    {
        null => "Ngoài thời gian bán",
        0 => "Miễn phí",
        _ => $"{price.Value.ToString("N0", VnFormat.Culture)}đ"
    };

    public static string AgeText(RaceCategoryDto category) => category.MaxAge is int max
        ? $"{category.MinAge}–{max} tuổi"
        : $"Từ {category.MinAge} tuổi";

    public string AgeGroupScope(AgeGroupDto group) => group.CategoryId is null ? "Mọi cự ly" :
        Event.Categories.FirstOrDefault(c => c.Id == group.CategoryId)?.Name ?? "Cự ly";

    public static string TransferText(EventPolicyDto policy) => policy.TransferPolicy switch
    {
        "Free" => $"Được chuyển nhượng tối đa {policy.MaxTransfers} lần, không phí",
        "Paid" => $"Được chuyển nhượng tối đa {policy.MaxTransfers} lần, phí {policy.TransferFee.ToString("N0", VnFormat.Culture)}đ",
        _ => "Không cho phép chuyển nhượng"
    };

    public static string RankingText(string? basis) => basis switch
    {
        "GunTime" => "Gun time",
        "ChipTime" => "Chip time",
        _ => "Chưa công bố"
    };

    public static string ForceMajeureText(string value) => value switch
    {
        "Refund" => "Hoàn tiền",
        "CarryOver" => "Bảo lưu sang mùa sau",
        _ => "Không hoàn tiền"
    };
}
