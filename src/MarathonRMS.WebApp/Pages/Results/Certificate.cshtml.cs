using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Results;

/// <summary>UC-64 – e-certificate dạng trang in (In / Lưu PDF bằng trình duyệt).</summary>
public class CertificateModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public CertificateModel(ResultApiClient results) => _results = results;

    [BindProperty(SupportsGet = true)] public int RegistrationId { get; set; }
    public CertificateDto? Certificate { get; private set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Certificate = await _results.GetCertificateAsync(RegistrationId, ct);
        }
        catch (ApiException ex)
        {
            var redirect = await HandleApiErrorAsync(ex);
            if (redirect is not null) return redirect;
            Error = ex.Message;
        }
        return Page();
    }
}
