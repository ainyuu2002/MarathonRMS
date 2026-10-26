using MarathonRMS.DTOs.Admin;
using MarathonRMS.Repositories;

namespace MarathonRMS.Services.Admin;

/// <summary>UC-17 – SuperAdmin xem nhật ký hệ thống (BR-36: chỉ đọc, không sửa/xóa).</summary>
public interface IAuditLogService
{
    /// <summary>IQueryable chiếu sang DTO để OData dịch $filter/$orderby/$top/$skip/$count ra SQL.</summary>
    IQueryable<AuditLogDto> Query();
    Task<AuditLogFacetsDto> GetFacetsAsync(CancellationToken ct = default);
}

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _logs;
    public AuditLogService(IAuditLogRepository logs) => _logs = logs;

    public IQueryable<AuditLogDto> Query() =>
        _logs.QueryWithUser().Select(r => new AuditLogDto
        {
            Id = r.Log.AuditLogId,
            UserId = r.Log.UserId,
            UserEmail = r.UserEmail,
            UserName = r.UserName,
            UserRole = r.Log.UserRole,
            Action = r.Log.Action,
            EntityType = r.Log.EntityType,
            EntityId = r.Log.EntityId,
            OldValues = r.Log.OldValues,
            NewValues = r.Log.NewValues,
            IpAddress = r.Log.IpAddress,
            CreatedAt = r.Log.CreatedAt
        });

    public async Task<AuditLogFacetsDto> GetFacetsAsync(CancellationToken ct = default) => new()
    {
        Actions = await _logs.GetDistinctActionsAsync(ct),
        EntityTypes = await _logs.GetDistinctEntityTypesAsync(ct)
    };
}
