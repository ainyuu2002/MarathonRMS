using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.RaceDay;

/// <summary>
/// UC-91 – BR-52: kết quả chính thức được Privacy.MedicalRetentionDays ngày (mặc định 30) thì xóa nhóm máu và ghi chú y tế
/// trong snapshot đăng ký của giải đó (Luật 91/2025 – dữ liệu sức khỏe chỉ giữ trong thời gian cần thiết).
/// Hồ sơ người chạy gốc (Participants) không bị xóa – người dùng tự quản lý ở trang Hồ sơ.
/// Chạy định kỳ bởi MedicalDataRetentionWorker (API) hoặc Admin bấm chạy ngay. Chạy lại nhiều lần không sao (chỉ xóa phần còn).
/// </summary>
public interface IMedicalDataRetentionService
{
    Task<MedicalPurgeSummaryDto> PurgeDueAsync(CancellationToken ct = default);

    /// <summary>Thời điểm dữ liệu y tế của giải sẽ bị xóa (null nếu kết quả chưa chính thức).</summary>
    Task<DateTime?> GetPurgeAtAsync(Event ev, CancellationToken ct = default);
}

public class MedicalDataRetentionService : IMedicalDataRetentionService
{
    public const string RetentionDaysKey = "Privacy.MedicalRetentionDays";
    private const int DefaultRetentionDays = 30;

    private readonly IMedicalDataRepository _medical;
    private readonly ISystemSettingsProvider _settings;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public MedicalDataRetentionService(IMedicalDataRepository medical, ISystemSettingsProvider settings, IDateTimeProvider clock,
        IAuditLogger audit, IUnitOfWork uow)
    {
        _medical = medical;
        _settings = settings;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    public async Task<MedicalPurgeSummaryDto> PurgeDueAsync(CancellationToken ct = default)
    {
        var days = await RetentionDaysAsync(ct);
        var now = _clock.Now;
        var purged = new List<MedicalPurgeEventDto>();

        foreach (var ev in await _medical.GetDueEventsAsync(now.AddDays(-days), ct))
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);
            var count = await _medical.PurgeEventAsync(ev.EventId, ct);
            await _audit.LogAsync("Privacy.MedicalPurge", nameof(Event), ev.EventId.ToString(), null,
                new { Registrations = count, RetentionDays = days, ev.ResultsOfficialAt }, ct);
            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            purged.Add(new MedicalPurgeEventDto
            {
                EventId = ev.EventId,
                EventName = ev.Name,
                OfficialAt = ev.ResultsOfficialAt,
                Registrations = count
            });
        }

        return new MedicalPurgeSummaryDto { RetentionDays = days, RanAt = now, Events = purged };
    }

    public async Task<DateTime?> GetPurgeAtAsync(Event ev, CancellationToken ct = default) =>
        ev.ResultsOfficialAt is DateTime official ? official.AddDays(await RetentionDaysAsync(ct)) : null;

    private async Task<int> RetentionDaysAsync(CancellationToken ct) =>
        Math.Max(0, await _settings.GetIntAsync(RetentionDaysKey, DefaultRetentionDays, ct));
}
