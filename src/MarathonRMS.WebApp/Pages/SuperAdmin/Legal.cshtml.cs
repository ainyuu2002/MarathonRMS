using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Legal;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.SuperAdmin;

/// <summary>UC-84 – đăng phiên bản mới của Điều khoản sử dụng / Chính sách quyền riêng tư.</summary>
public class LegalModel : BasePageModel
{
    private readonly SuperAdminApiClient _superAdmin;
    public LegalModel(SuperAdminApiClient superAdmin) => _superAdmin = superAdmin;

    /// <summary>
    /// Tab đang mở (Terms / Privacy). Kiểu string? – nếu để string, Razor Pages coi là bắt buộc và báo
    /// "The DocType field is required" khi mở trang không kèm ?DocType=. Giá trị lạ / bỏ trống ⇒ Terms.
    /// </summary>
    [BindProperty(SupportsGet = true)] public string? DocType { get; set; }

    private string CurrentDocType => DocType is "Terms" or "Privacy" ? DocType : "Terms";
    [BindProperty] public PublishLegalDocumentRequest Input { get; set; } = new();
    public List<LegalDocumentDto> Versions { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            Input = new PublishLegalDocumentRequest { DocType = CurrentDocType, Content = Versions.FirstOrDefault()?.Content ?? "" };
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Loại văn bản lấy theo tab đang mở (route), không lấy từ form
        Input.DocType = CurrentDocType;
        ModelState.Remove($"{nameof(Input)}.{nameof(Input.DocType)}");
        if (!ModelState.IsValid) { await LoadAsync(ct); return Page(); }
        try
        {
            var doc = await _superAdmin.PublishLegalAsync(Input, ct);
            StatusMessage = $"Đã đăng phiên bản {doc.Version}. Người dùng đồng ý phiên bản cũ không phải đồng ý lại.";
            return RedirectToPage(new { DocType = CurrentDocType });
        }
        catch (ApiException ex)
        {
            await HandleApiErrorAsync(ex, nameof(Input));
            await LoadAsync(ct);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        DocType = CurrentDocType;
        Versions = await _superAdmin.GetLegalVersionsAsync(DocType, ct);
    }
}
