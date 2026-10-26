using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Admin;

/// <summary>
/// UC-85 – Admin quản lý danh mục lý do phạt / DSQ (BR-28: phạt phải chọn lý do từ danh mục).
/// Lý do đã được dùng không xóa được (giữ lịch sử quyết định) – chỉ ngừng dùng (IsActive = false).
/// </summary>
public interface IPenaltyReasonService
{
    Task<IReadOnlyList<PenaltyReasonAdminDto>> GetAllAsync(CancellationToken ct = default);
    Task<PenaltyReasonAdminDto> CreateAsync(UpsertPenaltyReasonRequest request, CancellationToken ct = default);
    Task<PenaltyReasonAdminDto> UpdateAsync(string code, UpsertPenaltyReasonRequest request, CancellationToken ct = default);
    Task DeleteAsync(string code, CancellationToken ct = default);
}

public class PenaltyReasonService : IPenaltyReasonService
{
    private readonly IPenaltyReasonRepository _reasons;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public PenaltyReasonService(IPenaltyReasonRepository reasons, IAuditLogger audit, IUnitOfWork uow)
    {
        _reasons = reasons;
        _audit = audit;
        _uow = uow;
    }

    public async Task<IReadOnlyList<PenaltyReasonAdminDto>> GetAllAsync(CancellationToken ct = default)
    {
        var usage = await _reasons.GetUsageCountsAsync(ct);
        var reasons = await _reasons.Query().OrderByDescending(r => r.IsActive).ThenBy(r => r.Code).ToListAsync(ct);
        return reasons.Select(r => ToDto(r, usage.GetValueOrDefault(r.Code))).ToList();
    }

    public async Task<PenaltyReasonAdminDto> CreateAsync(UpsertPenaltyReasonRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _reasons.ExistsAsync(code, ct))
            throw new BusinessRuleException(ErrorCodes.Conflict, $"Mã {code} đã tồn tại.");
        var reason = new PenaltyReason { Code = code };
        Apply(reason, request);
        await _reasons.AddAsync(reason, ct);
        await _audit.LogAsync("PenaltyReason.Create", nameof(PenaltyReason), code, null, Snapshot(reason), ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(reason, 0);
    }

    public async Task<PenaltyReasonAdminDto> UpdateAsync(string code, UpsertPenaltyReasonRequest request, CancellationToken ct = default)
    {
        var reason = await _reasons.GetTrackedAsync(code, ct) ?? throw new NotFoundException("Không tìm thấy lý do phạt.");
        var old = Snapshot(reason);
        Apply(reason, request);
        await _audit.LogAsync("PenaltyReason.Update", nameof(PenaltyReason), code, old, Snapshot(reason), ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(reason, await _reasons.CountUsageAsync(code, ct));
    }

    public async Task DeleteAsync(string code, CancellationToken ct = default)
    {
        var reason = await _reasons.GetTrackedAsync(code, ct) ?? throw new NotFoundException("Không tìm thấy lý do phạt.");
        if (await _reasons.CountUsageAsync(code, ct) > 0)
            throw new BusinessRuleException("BR-28", "Lý do đã được dùng trong quyết định phạt nên không xóa được. Hãy chuyển sang ngừng dùng.");
        _reasons.Remove(reason);
        await _audit.LogAsync("PenaltyReason.Delete", nameof(PenaltyReason), code, Snapshot(reason), null, ct);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>Đồng bộ với CHECK CK_PenaltyReasons_Minutes: TimePenalty bắt buộc số phút; loại khác bỏ số phút.</summary>
    private static void Apply(PenaltyReason reason, UpsertPenaltyReasonRequest request)
    {
        var action = Enum.Parse<PenaltyAction>(request.DefaultAction);
        if (action == PenaltyAction.TimePenalty && request.DefaultMinutes is not (>= 1 and <= 600))
            throw new BusinessRuleException(ErrorCodes.Validation, "Phạt thời gian phải có số phút mặc định từ 1 đến 600.", 400);
        reason.Name = request.Name.Trim();
        reason.DefaultAction = action;
        reason.DefaultMinutes = action == PenaltyAction.TimePenalty ? request.DefaultMinutes : null;
        reason.IsActive = request.IsActive;
    }

    private static object Snapshot(PenaltyReason r) => new { r.Name, DefaultAction = r.DefaultAction.ToString(), r.DefaultMinutes, r.IsActive };

    private static PenaltyReasonAdminDto ToDto(PenaltyReason r, int usage) => new()
    {
        Code = r.Code, Name = r.Name, DefaultAction = r.DefaultAction.ToString(), DefaultMinutes = r.DefaultMinutes,
        IsActive = r.IsActive, UsageCount = usage
    };
}
