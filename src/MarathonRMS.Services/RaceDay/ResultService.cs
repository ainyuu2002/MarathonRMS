using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.RaceDay;

/// <summary>
/// UC-59 – tính kết quả của giải; UC-60 – công bố tạm thời / chính thức (BR-27);
/// UC-61 – DSQ, phạt cộng phút (UC-67), thu hồi phạt (BR-28, BR-32, BR-36); UC-62 – bảng xếp hạng công khai (BR-37);
/// UC-63 – kết quả cá nhân, splits.
/// </summary>
public class ResultService : IResultService
{
    private readonly IResultRepository _results;
    private readonly IPenaltyReasonRepository _reasons;
    private readonly IResultRecalculation _recalc;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IPrivacyFilter _privacy;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public ResultService(IResultRepository results, IPenaltyReasonRepository reasons, IResultRecalculation recalc,
        IOrganizationScope scope, ICurrentUserService currentUser, IPrivacyFilter privacy, IDateTimeProvider clock,
        IAuditLogger audit, IUnitOfWork uow)
    {
        _results = results;
        _reasons = reasons;
        _recalc = recalc;
        _scope = scope;
        _currentUser = currentUser;
        _privacy = privacy;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    public async Task<ResultCalculationSummaryDto> CalculateAsync(int eventId, CancellationToken ct = default)
    {
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.Results(eventId), ct);
        var ev = await LoadEventAsync(eventId, ct);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        EnsureCanCalculate(ev);

        var summary = await _recalc.RecalculateAsync(ev, ct);
        await _audit.LogAsync("Result.Calculate", nameof(Event), eventId.ToString(), null,
            new { summary.Finished, summary.Dnf, summary.Dns, summary.Dsq }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return summary;
    }

    public async Task<ResultsOverviewDto> GetOverviewAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await LoadEventAsync(eventId, ct);
        var counts = await _results.CountByStatusAsync(eventId, ct);
        var windowEndsAt = ev.ResultsProvisionalAt?.AddMinutes(ev.Policy.ComplaintWindowMinutes);
        var openComplaints = ev.ResultsStatus == ResultsStatus.Provisional ? await _results.CountOpenComplaintsAsync(eventId, ct) : 0;
        return new ResultsOverviewDto
        {
            EventId = ev.EventId,
            EventName = ev.Name,
            EventDate = ev.EventDate,
            EventStatus = ev.Status.ToString(),
            ResultsStatus = ev.ResultsStatus.ToString(),
            ProvisionalAt = ev.ResultsProvisionalAt,
            OfficialAt = ev.ResultsOfficialAt,
            Finished = counts.GetValueOrDefault(ResultStatus.Finished),
            Dnf = counts.GetValueOrDefault(ResultStatus.DNF),
            Dns = counts.GetValueOrDefault(ResultStatus.DNS),
            Dsq = counts.GetValueOrDefault(ResultStatus.DSQ),
            LastUpdatedAt = await _results.GetLastUpdatedAtAsync(eventId, ct),
            CanCalculate = CanCalculate(ev),
            CanPublishProvisional = ev.ResultsStatus == ResultsStatus.Draft && counts.Values.Sum() > 0,
            CanFinalize = ev.ResultsStatus == ResultsStatus.Provisional && _clock.Now >= windowEndsAt && openComplaints == 0,
            ComplaintWindowEndsAt = windowEndsAt,
            OpenComplaints = openComplaints,
            Categories = ev.Categories.OrderBy(c => c.StartAt).ThenBy(c => c.Name)
                .Select(c => new ResultCategoryOptionDto { Id = c.CategoryId, Name = c.Name, IsTimed = c.IsTimed, IsRanked = c.IsRanked })
                .ToList()
        };
    }

    public async Task<PagedResult<LeaderboardRowDto>> GetForOrganizerAsync(int eventId, int? categoryId, string? status,
        string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        _ = await LoadEventAsync(eventId, ct);
        var query = _results.QueryByEvent(eventId);
        if (categoryId is int id) query = query.Where(r => r.Registration.CategoryId == id);
        if (Enum.TryParse<ResultStatus>(status, ignoreCase: true, out var parsed)) query = query.Where(r => r.Status == parsed);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = int.TryParse(k, out var bib)
                ? query.Where(r => r.Registration.BibNumber == bib)
                : query.Where(r => r.Registration.FullName.Contains(k));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, PagingExtensions.MaxPageSize);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.Registration.Category.StartAt).ThenBy(r => r.Registration.CategoryId)
            .ThenBy(r => r.Status == ResultStatus.Finished ? 0 : r.Status == ResultStatus.DNF ? 1 : r.Status == ResultStatus.DSQ ? 2 : 3)
            .ThenBy(r => r.OverallRank ?? int.MaxValue)
            .ThenBy(r => r.Registration.BibNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new LeaderboardRowDto
            {
                ResultId = r.ResultId,
                RegistrationId = r.RegistrationId,
                EventId = r.Registration.EventId,
                CategoryId = r.Registration.CategoryId,
                CategoryName = r.Registration.Category.Name,
                BibNumber = r.Registration.BibNumber,
                DisplayName = r.Registration.FullName,
                Gender = r.Registration.Gender.ToString(),
                AgeGroupId = r.AgeGroupId,
                AgeGroupName = r.AgeGroup != null ? r.AgeGroup.Name : null,
                ClubName = r.Registration.ClubName,
                GunTimeMs = r.GunTimeMs,
                ChipTimeMs = r.ChipTimeMs,
                FinalTimeMs = r.FinalTimeMs,
                PenaltySeconds = r.PenaltySeconds,
                Status = r.Status.ToString(),
                OverallRank = r.OverallRank,
                GenderRank = r.GenderRank,
                AgeGroupRank = r.AgeGroupRank,
                IsProvisional = r.Registration.Event.ResultsStatus == ResultsStatus.Provisional
            })
            .ToListAsync(ct);
        return new PagedResult<LeaderboardRowDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<ResultsStatusDto> PublishProvisionalAsync(int eventId, CancellationToken ct = default)
    {
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.Results(eventId), ct);
        var ev = await LoadEventForStatusChangeAsync(eventId, ct);
        if (ev.ResultsStatus != ResultsStatus.Draft)
            throw new BusinessRuleException("BR-27", "Kết quả của giải đã được công bố.");
        if (!await _results.AnyForEventAsync(eventId, ct))
            throw new BusinessRuleException("BR-27", "Chưa có kết quả – hãy tính kết quả trước khi công bố.");

        ev.ResultsStatus = ResultsStatus.Provisional;
        ev.ResultsProvisionalAt = _clock.Now;
        await _audit.LogAsync("Result.PublishProvisional", nameof(Event), eventId.ToString(),
            new { ResultsStatus = nameof(ResultsStatus.Draft) },
            new { ResultsStatus = nameof(ResultsStatus.Provisional), ev.ResultsProvisionalAt }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToStatusDto(ev);
    }

    public async Task<ResultsStatusDto> FinalizeAsync(int eventId, CancellationToken ct = default)
    {
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.Results(eventId), ct);
        var ev = await LoadEventForStatusChangeAsync(eventId, ct);
        if (ev.ResultsStatus != ResultsStatus.Provisional)
            throw new BusinessRuleException("BR-27", "Chỉ công bố chính thức khi kết quả đang ở trạng thái tạm thời.");
        var windowEndsAt = ev.ResultsProvisionalAt!.Value.AddMinutes(ev.Policy.ComplaintWindowMinutes);
        if (_clock.Now < windowEndsAt)
            throw new BusinessRuleException("BR-27", $"Còn trong thời gian nhận khiếu nại (đến {windowEndsAt:HH:mm dd/MM/yyyy}).");
        var openComplaints = await _results.CountOpenComplaintsAsync(eventId, ct);
        if (openComplaints > 0)
            throw new BusinessRuleException("BR-27", $"Còn {openComplaints} khiếu nại chưa xử lý.");

        ev.ResultsStatus = ResultsStatus.Official;
        ev.ResultsOfficialAt = _clock.Now;
        await _audit.LogAsync("Result.Finalize", nameof(Event), eventId.ToString(),
            new { ResultsStatus = nameof(ResultsStatus.Provisional) },
            new { ResultsStatus = nameof(ResultsStatus.Official), ev.ResultsOfficialAt }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToStatusDto(ev);
    }

    // ============================ Bảng xếp hạng công khai (UC-62) ============================

    /// <summary>
    /// Chỉ giải đã công bố kết quả. Tên công khai theo cùng quy tắc với IPrivacyFilter.PublicDisplayName (BR-37), viết dạng
    /// biểu thức (CASE) để OData dịch $filter/$orderby ra SQL – kể cả Gender/Status (không dùng ToString() của enum).
    /// </summary>
    public IQueryable<LeaderboardRowDto> QueryLeaderboard() =>
        _results.QueryPublished().Select(r => new LeaderboardRowDto
        {
            ResultId = r.ResultId,
            RegistrationId = r.RegistrationId,
            EventId = r.Registration.EventId,
            CategoryId = r.Registration.CategoryId,
            CategoryName = r.Registration.Category.Name,
            BibNumber = r.Registration.BibNumber,
            DisplayName = r.Registration.HideFromPublicList ||
                          r.Registration.DateOfBirth > r.Registration.Event.EventDate.AddYears(-PrivacyFilter.AdultAge)
                ? (r.Registration.BibNumber != null ? "VĐV #" + r.Registration.BibNumber.ToString() : "VĐV ẩn danh")
                : r.Registration.FullName,
            Gender = r.Registration.Gender == Gender.F ? "F" : "M",
            AgeGroupId = r.AgeGroupId,
            AgeGroupName = r.AgeGroup != null ? r.AgeGroup.Name : null,
            ClubName = r.Registration.HideFromPublicList ||
                       r.Registration.DateOfBirth > r.Registration.Event.EventDate.AddYears(-PrivacyFilter.AdultAge)
                ? null
                : r.Registration.ClubName,
            GunTimeMs = r.GunTimeMs,
            ChipTimeMs = r.ChipTimeMs,
            FinalTimeMs = r.FinalTimeMs,
            PenaltySeconds = r.PenaltySeconds,
            Status = r.Status == ResultStatus.Finished ? "Finished"
                : r.Status == ResultStatus.DNF ? "DNF"
                : r.Status == ResultStatus.DSQ ? "DSQ"
                : "DNS",
            OverallRank = r.OverallRank,
            GenderRank = r.GenderRank,
            AgeGroupRank = r.AgeGroupRank,
            IsProvisional = r.Registration.Event.ResultsStatus == ResultsStatus.Provisional,
            SortOrder = r.Status == ResultStatus.Finished ? (r.OverallRank ?? 999_999)
                : r.Status == ResultStatus.DNF ? 1_000_000
                : r.Status == ResultStatus.DSQ ? 2_000_000
                : 3_000_000
        });

    public async Task<IReadOnlyList<ResultsEventDto>> GetPublishedEventsAsync(CancellationToken ct = default) =>
        (await _results.GetPublishedEventsAsync(ct)).Select(e => new ResultsEventDto
        {
            EventId = e.EventId,
            EventName = e.Name,
            OrganizationName = e.Organization.Name,
            EventDate = e.EventDate,
            ResultsStatus = e.ResultsStatus.ToString(),
            Categories = e.Categories.OrderBy(c => c.StartAt).ThenBy(c => c.Name)
                .Select(c => new ResultCategoryOptionDto { Id = c.CategoryId, Name = c.Name, IsTimed = c.IsTimed, IsRanked = c.IsRanked })
                .ToList(),
            AgeGroups = e.AgeGroups.OrderBy(g => g.CategoryId != null).ThenBy(g => g.MinAge)
                .Select(g => new AgeGroupOptionDto { Id = g.AgeGroupId, Name = g.Name, CategoryId = g.CategoryId })
                .ToList()
        }).ToList();

    // ============================ Kết quả cá nhân (UC-63) ============================

    /// <summary>
    /// Công khai khi giải đã công bố kết quả. Chính VĐV (hoặc người đăng ký hộ) và Staff/Organizer của BTC xem đầy đủ;
    /// người xem khác: tên theo BR-37, không lộ lý do y tế và ghi chú của quyết định phạt.
    /// </summary>
    public async Task<ResultDetailDto> GetDetailAsync(int registrationId, CancellationToken ct = default)
    {
        var result = await _results.GetDetailByRegistrationAsync(registrationId, ct)
                     ?? throw new NotFoundException("Chưa có kết quả của VĐV này.");
        var registration = result.Registration;
        var ev = registration.Event;
        var me = _currentUser.UserId;
        var isOwner = me is not null && (me == registration.Participant.LinkedUserId || me == registration.RegisteredByUserId);
        var privileged = isOwner || IsOperatorOf(ev.OrganizationId);
        if ((ev.ResultsStatus == ResultsStatus.Draft || registration.Status != RegistrationStatus.Confirmed) && !privileged)
            throw new NotFoundException("Chưa có kết quả của VĐV này.");

        var summary = ToOrganizerRow(result);
        if (!privileged)
        {
            summary.DisplayName = _privacy.PublicDisplayName(registration.FullName, registration.HideFromPublicList,
                registration.DateOfBirth, ev.EventDate, registration.BibNumber);
            if (summary.DisplayName != registration.FullName) summary.ClubName = null;
        }

        var checkpoints = registration.Category.Checkpoints.ToDictionary(c => c.CheckpointId);
        var showReason = privileged || result.DnfReason != DnfReason.Medical;
        return new ResultDetailDto
        {
            Summary = summary,
            EventName = ev.Name,
            DnfReason = showReason ? result.DnfReason?.ToString() : null,
            DnfCheckpointName = result.DnfCheckpointId is int dnfAt && checkpoints.TryGetValue(dnfAt, out var dnfCheckpoint)
                ? dnfCheckpoint.Name
                : null,
            Splits = BuildSplits(registration, ev.Policy, checkpoints),
            Penalties = result.Penalties.Where(p => p.RevokedAt == null).OrderBy(p => p.IssuedAt)
                .Select(p =>
                {
                    var dto = ToPenaltyDto(p, p.Reason.Name,
                        p.CheckpointId is int at && checkpoints.TryGetValue(at, out var c) ? c.Name : null);
                    if (!privileged) dto.Note = null;
                    return dto;
                })
                .ToList()
        };
    }

    /// <summary>
    /// Thời gian tại từng checkpoint (trừ thảm xuất phát) tính từ mốc theo cơ sở xếp hạng chung của giải:
    /// giờ chip ⇒ từ thảm xuất phát của VĐV; giờ súng ⇒ từ giờ xuất phát của cự ly. Pace đoạn = giây/km.
    /// </summary>
    private static IReadOnlyList<SplitDto> BuildSplits(Registration registration, EventPolicy policy,
        IReadOnlyDictionary<int, Checkpoint> checkpoints)
    {
        var records = registration.TimingRecords
            .Where(t => checkpoints.ContainsKey(t.CheckpointId))
            .OrderBy(t => checkpoints[t.CheckpointId].Sequence)
            .ToList();
        var startRecord = records.FirstOrDefault(t => checkpoints[t.CheckpointId].Type == CheckpointType.Start);
        var origin = policy.OverallRankingBasis == RankingBasis.ChipTime && startRecord is not null
            ? startRecord.RecordedAt
            : registration.Category.StartAt;

        var splits = new List<SplitDto>();
        long previousElapsed = 0;
        decimal previousDistance = 0;
        foreach (var record in records.Where(t => checkpoints[t.CheckpointId].Type != CheckpointType.Start))
        {
            var checkpoint = checkpoints[record.CheckpointId];
            var elapsed = (long)(record.RecordedAt - origin).TotalMilliseconds;
            var segmentKm = checkpoint.DistanceKm - previousDistance;
            splits.Add(new SplitDto
            {
                CheckpointId = checkpoint.CheckpointId,
                CheckpointName = checkpoint.Name,
                DistanceKm = checkpoint.DistanceKm,
                Sequence = checkpoint.Sequence,
                RecordedAt = record.RecordedAt,
                ElapsedMs = elapsed,
                SegmentPaceSecondsPerKm = segmentKm > 0
                    ? (int)Math.Round((elapsed - previousElapsed) / 1000m / segmentKm)
                    : null
            });
            previousElapsed = elapsed;
            previousDistance = checkpoint.DistanceKm;
        }
        return splits;
    }

    private bool IsOperatorOf(int organizationId) =>
        _currentUser.IsAdminOrAbove ||
        ((_currentUser.IsInRole(AppRoles.Organizer) || _currentUser.IsInRole(AppRoles.Staff)) &&
         _currentUser.OrganizationId == organizationId);

    // ============================ Phạt / DSQ ============================

    public async Task<IReadOnlyList<PenaltyReasonDto>> GetPenaltyReasonsAsync(CancellationToken ct = default) =>
        (await _reasons.Query().Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync(ct))
        .Select(r => new PenaltyReasonDto
        {
            Code = r.Code,
            Name = r.Name,
            DefaultAction = r.DefaultAction.ToString(),
            DefaultMinutes = r.DefaultMinutes
        }).ToList();

    public async Task<ResultPenaltiesDto> GetPenaltiesAsync(int resultId, CancellationToken ct = default)
    {
        var result = await _results.GetWithPenaltiesAsync(resultId, ct) ?? throw new NotFoundException("Không tìm thấy kết quả.");
        _scope.EnsureCanManage(result.Registration.Event.OrganizationId);
        var checkpoints = result.Registration.Category.Checkpoints.OrderBy(c => c.Sequence).ToList();

        return new ResultPenaltiesDto
        {
            Result = ToOrganizerRow(result),
            ResultsStatus = result.Registration.Event.ResultsStatus.ToString(),
            Checkpoints = checkpoints.Select(c => new ResultCheckpointOptionDto { Id = c.CheckpointId, Name = c.Name }).ToList(),
            Penalties = result.Penalties.OrderByDescending(p => p.IssuedAt)
                .Select(p => ToPenaltyDto(p, p.Reason.Name, checkpoints.FirstOrDefault(c => c.CheckpointId == p.CheckpointId)?.Name))
                .ToList()
        };
    }

    public async Task<PenaltyDto> IssuePenaltyAsync(int resultId, IssuePenaltyRequest request, CancellationToken ct = default)
    {
        var result = await _results.GetWithPenaltiesAsync(resultId, ct) ?? throw new NotFoundException("Không tìm thấy kết quả.");
        var registration = result.Registration;
        var ev = registration.Event;
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        _scope.EnsureNotOwnRegistration(registration.Participant.LinkedUserId, registration.RegisteredByUserId);
        if (ev.ResultsStatus == ResultsStatus.Official)
            throw new BusinessRuleException("BR-27", "Kết quả của giải đã chính thức – không thể ban hành quyết định phạt.");

        var reason = await _reasons.GetTrackedAsync(request.ReasonCode.Trim(), ct);
        if (reason is null || !reason.IsActive)
            throw new BusinessRuleException("BR-28", "Lý do phạt phải chọn trong danh mục đang áp dụng.", 400);
        if (string.IsNullOrWhiteSpace(request.Note))
            throw new BusinessRuleException("BR-28", "Quyết định phạt phải kèm ghi chú (diễn biến, bằng chứng).", 400);

        var action = Enum.Parse<PenaltyAction>(request.Action, ignoreCase: true);
        int? minutes = null;
        if (action == PenaltyAction.TimePenalty)
        {
            minutes = request.Minutes ?? reason.DefaultMinutes;
            if (minutes is null or <= 0)
                throw new BusinessRuleException(ErrorCodes.Validation, "Phạt cộng giờ phải ghi số phút.", 400);
        }
        if (action == PenaltyAction.DSQ && result.Penalties.Any(p => p.RevokedAt == null && p.Action == PenaltyAction.DSQ))
            throw new BusinessRuleException("BR-28", "VĐV đã bị truất quyền thi đấu (DSQ).");

        var checkpoints = registration.Category.Checkpoints;
        if (request.CheckpointId is int checkpointId && checkpoints.All(c => c.CheckpointId != checkpointId))
            throw new BusinessRuleException(ErrorCodes.Validation, "Checkpoint không thuộc cự ly của VĐV.", 400);

        var penalty = new ResultPenalty
        {
            ResultId = result.ResultId,
            ReasonCode = reason.Code,
            Action = action,
            Minutes = minutes,
            CheckpointId = request.CheckpointId,
            Note = request.Note.Trim(),
            IssuedById = _currentUser.UserId!,
            IssuedAt = _clock.Now
        };

        await using var tx = await _uow.BeginTransactionAsync(ct);
        var current = await LockForResultChangeAsync(ev.EventId, ct);
        await _results.AddPenaltyAsync(penalty, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("Result.Penalty", nameof(ResultPenalty), penalty.PenaltyId.ToString(), null,
            new { result.ResultId, registration.BibNumber, penalty.ReasonCode, Action = action.ToString(), penalty.Minutes, penalty.Note }, ct);
        if (action != PenaltyAction.Warning)
            await _recalc.RecalculateAsync(current, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToPenaltyDto(penalty, reason.Name, checkpoints.FirstOrDefault(c => c.CheckpointId == penalty.CheckpointId)?.Name);
    }

    public async Task RevokePenaltyAsync(int penaltyId, CancellationToken ct = default)
    {
        var penalty = await _results.GetPenaltyTrackedAsync(penaltyId, ct)
                      ?? throw new NotFoundException("Không tìm thấy quyết định phạt.");
        var registration = penalty.Result.Registration;
        var ev = registration.Event;
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        _scope.EnsureNotOwnRegistration(registration.Participant.LinkedUserId, registration.RegisteredByUserId);
        if (ev.ResultsStatus == ResultsStatus.Official)
            throw new BusinessRuleException("BR-27", "Kết quả của giải đã chính thức – không thể thu hồi quyết định phạt.");
        if (penalty.RevokedAt is not null)
            throw new BusinessRuleException(ErrorCodes.Conflict, "Quyết định phạt đã được thu hồi.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        var current = await LockForResultChangeAsync(ev.EventId, ct);
        penalty.RevokedAt = _clock.Now;
        await _audit.LogAsync("Result.PenaltyRevoke", nameof(ResultPenalty), penaltyId.ToString(),
            new { penalty.ReasonCode, Action = penalty.Action.ToString(), penalty.Minutes },
            new { penalty.RevokedAt }, ct);
        await _uow.SaveChangesAsync(ct);
        if (penalty.Action != PenaltyAction.Warning)
            await _recalc.RecalculateAsync(current, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Giữ khóa kết quả của giải rồi đọc lại trạng thái: nếu vừa có người chốt chính thức trong lúc chờ khóa thì dừng (BR-27).
    /// </summary>
    private async Task<Event> LockForResultChangeAsync(int eventId, CancellationToken ct)
    {
        await _uow.LockAsync(RaceDayLocks.Results(eventId), ct);
        var ev = await _results.GetEventForResultsAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        if (ev.ResultsStatus == ResultsStatus.Official)
            throw new BusinessRuleException("BR-27", "Kết quả của giải đã chính thức – không thể thay đổi quyết định phạt.");
        return ev;
    }

    private static PenaltyDto ToPenaltyDto(ResultPenalty p, string reasonName, string? checkpointName) => new()
    {
        Id = p.PenaltyId,
        ReasonCode = p.ReasonCode,
        ReasonName = reasonName,
        Action = p.Action.ToString(),
        Minutes = p.Minutes,
        CheckpointName = checkpointName,
        Note = p.Note,
        IssuedAt = p.IssuedAt,
        RevokedAt = p.RevokedAt
    };

    private static LeaderboardRowDto ToOrganizerRow(Result r) => new()
    {
        ResultId = r.ResultId,
        RegistrationId = r.RegistrationId,
        EventId = r.Registration.EventId,
        CategoryId = r.Registration.CategoryId,
        CategoryName = r.Registration.Category.Name,
        BibNumber = r.Registration.BibNumber,
        DisplayName = r.Registration.FullName,
        Gender = r.Registration.Gender.ToString(),
        AgeGroupId = r.AgeGroupId,
        AgeGroupName = r.AgeGroup?.Name,
        ClubName = r.Registration.ClubName,
        GunTimeMs = r.GunTimeMs,
        ChipTimeMs = r.ChipTimeMs,
        FinalTimeMs = r.FinalTimeMs,
        PenaltySeconds = r.PenaltySeconds,
        Status = r.Status.ToString(),
        OverallRank = r.OverallRank,
        GenderRank = r.GenderRank,
        AgeGroupRank = r.AgeGroupRank,
        IsProvisional = r.Registration.Event.ResultsStatus == ResultsStatus.Provisional
    };

    // ============================ Kiểm tra ============================

    private async Task<Event> LoadEventAsync(int eventId, CancellationToken ct)
    {
        var ev = await _results.GetEventForResultsAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);
        return ev;
    }

    private async Task<Event> LoadEventForStatusChangeAsync(int eventId, CancellationToken ct)
    {
        var ev = await _results.GetEventTrackedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        return ev;
    }

    private static ResultsStatusDto ToStatusDto(Event ev) => new()
    {
        EventId = ev.EventId,
        ResultsStatus = ev.ResultsStatus.ToString(),
        ProvisionalAt = ev.ResultsProvisionalAt,
        OfficialAt = ev.ResultsOfficialAt
    };

    private bool CanCalculate(Event ev) =>
        ev.ResultsStatus != ResultsStatus.Official &&
        ev.Status is EventStatus.RegistrationClosed or EventStatus.Completed &&
        _clock.Today >= ev.EventDate;

    private void EnsureCanCalculate(Event ev)
    {
        if (ev.ResultsStatus == ResultsStatus.Official)
            throw new BusinessRuleException("BR-27", "Kết quả của giải đã chính thức – không tính lại.");
        if (ev.Status is not (EventStatus.RegistrationClosed or EventStatus.Completed))
            throw new BusinessRuleException(ErrorCodes.Conflict, "Chỉ tính kết quả khi giải đã đóng đăng ký hoặc đã kết thúc.");
        if (_clock.Today < ev.EventDate)
            throw new BusinessRuleException(ErrorCodes.Conflict, "Chưa tới ngày thi – chưa thể tính kết quả.");
    }
}
