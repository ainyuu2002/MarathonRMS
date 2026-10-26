using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace MarathonRMS.API.Controllers;

/// <summary>
/// UC-17 – nhật ký hệ thống, chỉ SuperAdmin. GET /odata/AuditLogs?$filter=…&amp;$orderby=CreatedAt desc&amp;$top=20&amp;$skip=0&amp;$count=true
/// </summary>
[Authorize(Policy = AppPolicies.SuperAdminOnly)]
public class AuditLogsController : ODataController
{
    private readonly IAuditLogService _logs;
    public AuditLogsController(IAuditLogService logs) => _logs = logs;

    [EnableQuery(PageSize = 100, MaxTop = 100,
        AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy | AllowedQueryOptions.Top
                              | AllowedQueryOptions.Skip | AllowedQueryOptions.Count | AllowedQueryOptions.Select)]
    public IQueryable<AuditLogDto> Get() => _logs.Query();
}

/// <summary>Giá trị lọc nhanh cho trang nhật ký (REST).</summary>
[ApiController]
[Route("api/superadmin/audit-logs")]
[Authorize(Policy = AppPolicies.SuperAdminOnly)]
public class AuditLogFacetsController : ControllerBase
{
    private readonly IAuditLogService _logs;
    public AuditLogFacetsController(IAuditLogService logs) => _logs = logs;

    [HttpGet("facets")]
    public async Task<ActionResult<AuditLogFacetsDto>> GetFacets(CancellationToken ct) => Ok(await _logs.GetFacetsAsync(ct));
}
