using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>Phần A – GĐ3: UC-69 dashboard giải (Organizer), UC-71 dashboard hệ thống (Admin).</summary>
[ApiController]
[Route("api")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("events/{id:int}/dashboard"), Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<EventDashboardDto>> GetEvent(int id, CancellationToken ct) => Ok(await _dashboard.GetEventAsync(id, ct));

    [HttpGet("admin/dashboard"), Authorize(Policy = AppPolicies.AdminOrAbove)]
    public async Task<ActionResult<SystemDashboardDto>> GetSystem(CancellationToken ct) => Ok(await _dashboard.GetSystemAsync(ct));
}
