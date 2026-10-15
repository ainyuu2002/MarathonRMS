using MarathonRMS.DTOs.Legal;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Legal;

/// <summary>Hiển thị phiên bản hiện hành của điều khoản / chính sách quyền riêng tư (UC-84).</summary>
public abstract class LegalPageModel : BasePageModel
{
    private readonly LegalApiClient _legal;
    protected LegalPageModel(LegalApiClient legal) => _legal = legal;

    protected abstract string DocType { get; }
    public LegalDocumentDto? Document { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Document = await _legal.GetCurrentAsync(DocType, ct);
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex) ?? Page();
        }
        return Page();
    }
}

public class TermsModel : LegalPageModel
{
    public TermsModel(LegalApiClient legal) : base(legal) { }
    protected override string DocType => "terms";
}

public class PrivacyModel : LegalPageModel
{
    public PrivacyModel(LegalApiClient legal) : base(legal) { }
    protected override string DocType => "privacy";
}
