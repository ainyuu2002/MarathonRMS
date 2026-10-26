using System.Text.Encodings.Web;
using System.Text.Json;
using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.SuperAdmin;

/// <summary>UC-17 – nhật ký hệ thống (BR-36), đọc qua OData.</summary>
public class AuditLogsModel : BasePageModel
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly SuperAdminApiClient _api;
    public AuditLogsModel(SuperAdminApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "op")] public string? Operation { get; set; }
    [BindProperty(SupportsGet = true)] public string? EntityType { get; set; }
    /// <summary>Email người thao tác hoặc mã đối tượng.</summary>
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public PagedResult<AuditLogDto> Data { get; private set; } = new();
    public AuditLogFacetsDto Facets { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var keyword = Keyword?.Trim();
        var filter = ODataQuery.And(
            From is DateTime f ? $"CreatedAt ge {ODataQuery.Date(f.Date)}" : null,
            To is DateTime t ? $"CreatedAt lt {ODataQuery.Date(t.Date.AddDays(1))}" : null,
            string.IsNullOrEmpty(Operation) ? null : $"Action eq {ODataQuery.Str(Operation)}",
            string.IsNullOrEmpty(EntityType) ? null : $"EntityType eq {ODataQuery.Str(EntityType)}",
            string.IsNullOrEmpty(keyword) ? null
                : $"contains(UserEmail,{ODataQuery.Str(keyword)}) or EntityId eq {ODataQuery.Str(keyword)}");
        try
        {
            Facets = await _api.GetAuditLogFacetsAsync(ct);
            Data = await _api.GetAuditLogsAsync(filter, P, 20, ct);
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    /// <summary>JSON trong nhật ký → dạng thụt lề dễ đọc.</summary>
    public static string Pretty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "—";
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, PrettyJson);
        }
        catch (JsonException) { return json; }
    }
}
