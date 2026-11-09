using MarathonRMS.DTOs.Results;
using MarathonRMS.Services.RaceDay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-64 – hồ sơ thành tích (của tôi / công khai) và e-certificate.</summary>
[ApiController]
[Route("api")]
public class AchievementsController : ControllerBase
{
    private readonly IAchievementService _achievements;
    public AchievementsController(IAchievementService achievements) => _achievements = achievements;

    [HttpGet("achievements/me")]
    [Authorize]
    public async Task<ActionResult<AchievementProfileDto>> GetMine(CancellationToken ct) =>
        Ok(await _achievements.GetMineAsync(ct));

    /// <summary>Hồ sơ công khai – chỉ khi người chạy bật "hiện tên công khai" và đủ 18 tuổi (BR-37).</summary>
    [HttpGet("runners/{participantId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<AchievementProfileDto>> GetPublic(int participantId, CancellationToken ct) =>
        Ok(await _achievements.GetPublicAsync(participantId, ct));

    [HttpGet("results/registrations/{registrationId:int}/certificate")]
    [AllowAnonymous]
    public async Task<ActionResult<CertificateDto>> GetCertificate(int registrationId, CancellationToken ct) =>
        Ok(await _achievements.GetCertificateAsync(registrationId, ct));
}
