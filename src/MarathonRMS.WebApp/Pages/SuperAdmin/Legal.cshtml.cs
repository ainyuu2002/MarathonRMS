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

    [BindProperty(SupportsGet = true)] public string DocType { get; set; } = "Terms";
    [BindProperty] public PublishLegalDocumentRequest Input { get; set; } = new();
    public List<LegalDocumentDto> Versions { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            Input = new PublishLegalDocumentRequest { DocType = DocType, Content = Versions.FirstOrDefault()?.Content ?? "" };
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Loại văn bản lấy theo tab đang mở (route), không lấy từ form
        Input.DocType = DocType;
        ModelState.Remove($"{nameof(Input)}.{nameof(Input.DocType)}");
        if (!ModelState.IsValid) { await LoadAsync(ct); return Page(); }
        try
        {
            var doc = await _superAdmin.PublishLegalAsync(Input, ct);
            StatusMessage = $"Đã đăng phiên bản {doc.Version}. Người dùng đồng ý phiên bản cũ không phải đồng ý lại.";
            return RedirectToPage(new { DocType });
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
        if (DocType is not ("Terms" or "Privacy")) DocType = "Terms";
        Versions = await _superAdmin.GetLegalVersionsAsync(DocType, ct);
    }
}
