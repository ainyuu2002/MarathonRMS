using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-91 – Admin chạy ngay việc tự xóa dữ liệu y tế đã quá hạn giữ (BR-52); bình thường tác vụ nền tự chạy.</summary>
[ApiController]
[Route("api/admin/privacy")]
[Authorize(Policy = AppPolicies.AdminOrAbove)]
public class PrivacyController : ControllerBase
{
    private readonly IMedicalDataRetentionService _retention;
    public PrivacyController(IMedicalDataRetentionService retention) => _retention = retention;

    [HttpPost("medical-purge")]
    public async Task<ActionResult<MedicalPurgeSummaryDto>> PurgeMedicalData(CancellationToken ct) =>
        Ok(await _retention.PurgeDueAsync(ct));
}
