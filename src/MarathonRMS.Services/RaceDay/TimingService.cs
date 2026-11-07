using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.RaceDay;

/// <summary>UC-57 – ghi thời gian tại checkpoint theo BIB (BR-21, BR-32).</summary>
public class TimingService : ITimingService
{
    private readonly ITimingRepository _timing;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public TimingService(ITimingRepository timing, IOrganizationScope scope, ICurrentUserService currentUser,
        IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    {
        _timing = timing;
        _scope = scope;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    public async Task<TimingRecordDto> RecordAsync(RecordTimingRequest request, CancellationToken ct = default)
    {
        var ev = await LoadEventForWriteAsync(request.EventId, ct);
        var r = await _timing.GetConfirmedByBibAsync(ev.EventId, request.BibNumber, ct)
                ?? throw new NotFoundException($"Không có VĐV mang BIB {request.BibNumber} trong giải.");
        _scope.EnsureNotOwnRegistration(r.Participant.LinkedUserId, r.RegisteredByUserId);

        var category = r.Category;
        if (!category.IsTimed)
            throw new BusinessRuleException("BR-26", $"Cự ly {category.Name} không bấm giờ.");
        var checkpoint = category.Checkpoints.FirstOrDefault(c => c.CheckpointId == request.CheckpointId)
                         ?? throw new BusinessRuleException(ErrorCodes.Validation,
                             $"Checkpoint không thuộc cự ly {category.Name} của BIB {request.BibNumber}.", 400);

        var recordedAt = TruncateToMilliseconds(request.RecordedAt);
        if (recordedAt < category.StartAt)
            throw new BusinessRuleException("BR-21",
                $"Thời gian ghi nhận sớm hơn giờ xuất phát của cự ly {category.Name} ({category.StartAt:HH:mm dd/MM}).", 400);
        if (recordedAt > _clock.Now.AddMinutes(1))
            throw new BusinessRuleException("BR-21", "Thời gian ghi nhận đang ở tương lai.", 400);

        if (r.Result is { Status: ResultStatus.DNF, DnfReason: DnfReason.Voluntary or DnfReason.Medical } withdrawn)
        {
            var at = category.Checkpoints.FirstOrDefault(c => c.CheckpointId == withdrawn.DnfCheckpointId);
            if (at is not null && checkpoint.Sequence > at.Sequence)
                throw new BusinessRuleException("BR-23", $"BIB {request.BibNumber} đã bỏ cuộc tại {at.Name}.");
        }

        var records = await _timing.GetByRegistrationAsync(r.RegistrationId, ct);
        var existing = records.FirstOrDefault(t => t.CheckpointId == checkpoint.CheckpointId);
        if (existing is not null)
            throw new BusinessRuleException("BR-21",
                $"BIB {request.BibNumber} đã có thời gian tại {checkpoint.Name} ({existing.RecordedAt:HH:mm:ss}).");
        var earlier = records.Where(t => t.Checkpoint.Sequence < checkpoint.Sequence).MaxBy(t => t.RecordedAt);
        if (earlier is not null && recordedAt <= earlier.RecordedAt)
            throw new BusinessRuleException("BR-21",
                $"Thời gian tại {checkpoint.Name} phải muộn hơn thời gian tại {earlier.Checkpoint.Name} ({earlier.RecordedAt:HH:mm:ss}).");
        var later = records.Where(t => t.Checkpoint.Sequence > checkpoint.Sequence).MinBy(t => t.RecordedAt);
        if (later is not null && recordedAt >= later.RecordedAt)
            throw new BusinessRuleException("BR-21",
                $"Thời gian tại {checkpoint.Name} phải sớm hơn thời gian tại {later.Checkpoint.Name} ({later.RecordedAt:HH:mm:ss}).");

        var record = new TimingRecord
        {
            RegistrationId = r.RegistrationId,
            CheckpointId = checkpoint.CheckpointId,
            RecordedAt = recordedAt,
            Source = TimingSource.Manual,
            RecordedById = _currentUser.UserId!
        };
        await _timing.AddAsync(record, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique (RegistrationId, CheckpointId): hai người cùng ghi một lúc
            throw new BusinessRuleException("BR-21",
                $"BIB {request.BibNumber} vừa được ghi thời gian tại {checkpoint.Name} bởi người khác.");
        }

        return new TimingRecordDto
        {
            Id = record.TimingRecordId,
            RegistrationId = r.RegistrationId,
            BibNumber = request.BibNumber,
            FullName = r.FullName,
            CheckpointId = checkpoint.CheckpointId,
            CheckpointName = checkpoint.Name,
            RecordedAt = record.RecordedAt,
            Source = record.Source.ToString()
        };
    }

    public Task RecordWithdrawalAsync(RecordWithdrawalRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public async Task<IReadOnlyList<TimingRecordDto>> GetByRegistrationAsync(int registrationId, CancellationToken ct = default)
    {
        var r = await _timing.GetRegistrationAsync(registrationId, ct) ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        _scope.EnsureCanOperate(r.Event.OrganizationId);
        return (await _timing.GetByRegistrationAsync(registrationId, ct)).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TimingRecordDto>> GetByBibAsync(int eventId, int bibNumber, CancellationToken ct = default)
    {
        var ev = await _timing.GetEventAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanOperate(ev.OrganizationId);
        var r = await _timing.GetConfirmedByBibAsync(eventId, bibNumber, ct)
                ?? throw new NotFoundException($"Không có VĐV mang BIB {bibNumber} trong giải.");
        return (await _timing.GetByRegistrationAsync(r.RegistrationId, ct)).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TimingRecordDto>> GetRecentAsync(int eventId, int? checkpointId, int take,
        CancellationToken ct = default)
    {
        var ev = await _timing.GetEventAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanOperate(ev.OrganizationId);
        return (await _timing.GetRecentAsync(eventId, checkpointId, Math.Clamp(take, 1, 100), ct)).Select(ToDto).ToList();
    }

    public async Task DeleteAsync(int timingRecordId, CancellationToken ct = default)
    {
        var record = await _timing.GetTrackedAsync(timingRecordId, ct)
                     ?? throw new NotFoundException("Không tìm thấy bản ghi thời gian.");
        var ev = record.Registration.Event;
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        if (ev.ResultsStatus == ResultsStatus.Official)
            throw new BusinessRuleException("BR-27", "Kết quả của giải đã chính thức – không thể thay đổi dữ liệu bấm giờ.");

        await _audit.LogAsync("Timing.Delete", nameof(TimingRecord), timingRecordId.ToString(),
            new { record.RegistrationId, record.Registration.BibNumber, Checkpoint = record.Checkpoint.Name, record.RecordedAt },
            null, ct);
        _timing.Remove(record);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Event> LoadEventForWriteAsync(int eventId, CancellationToken ct)
    {
        var ev = await _timing.GetEventAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanOperate(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        if (ev.ResultsStatus == ResultsStatus.Official)
            throw new BusinessRuleException("BR-27", "Kết quả của giải đã chính thức – không thể thay đổi dữ liệu bấm giờ.");
        if (ev.Status is EventStatus.Draft or EventStatus.Published or EventStatus.Postponed or EventStatus.Cancelled)
            throw new BusinessRuleException(ErrorCodes.Conflict, "Giải chưa tới ngày thi hoặc đã hoãn/hủy.");
        return ev;
    }

    private static DateTime TruncateToMilliseconds(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, value.Kind);

    private static TimingRecordDto ToDto(TimingRecord t) => new()
    {
        Id = t.TimingRecordId,
        RegistrationId = t.RegistrationId,
        BibNumber = t.Registration.BibNumber ?? 0,
        FullName = t.Registration.FullName,
        CheckpointId = t.CheckpointId,
        CheckpointName = t.Checkpoint.Name,
        RecordedAt = t.RecordedAt,
        Source = t.Source.ToString()
    };
}
