using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.RaceDay;

/// <summary>
/// UC-66 – khiếu nại kết quả. VĐV (hoặc người đăng ký hộ) gửi khi kết quả đang tạm thời và còn trong thời gian nhận khiếu nại
/// (BR-27); Organizer của BTC trả lời chấp nhận / từ chối (BR-30, BR-32, BR-36). Kết quả chỉ chốt chính thức khi hết khiếu nại mở.
/// Chấp nhận khiếu nại không tự sửa kết quả: Organizer sửa dữ liệu (thời gian, phạt) rồi kết quả được tính lại.
/// Phí khiếu nại (EventPolicies.ComplaintFee) được ghi nhận trên khiếu nại; thu tiền nối với thanh toán của phần B sau.
/// </summary>
public interface IComplaintService
{
    Task<ComplaintDto> CreateAsync(int resultId, CreateComplaintRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ComplaintDto>> GetMineAsync(CancellationToken ct = default);
    Task<EventComplaintsDto> GetForEventAsync(int eventId, string? status, CancellationToken ct = default);
    Task<ComplaintDto> ResolveAsync(int complaintId, ResolveComplaintRequest request, CancellationToken ct = default);
}

public class ComplaintService : IComplaintService
{
    private readonly IComplaintRepository _complaints;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public ComplaintService(IComplaintRepository complaints, IOrganizationScope scope, ICurrentUserService currentUser,
        IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    {
        _complaints = complaints;
        _scope = scope;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    /// <summary>Hạn nhận khiếu nại: từ lúc công bố tạm thời (hoặc lần kết quả thay đổi gần nhất) + ComplaintWindowMinutes.</summary>
    public static DateTime? WindowEndsAt(Event ev) =>
        ev.ResultsStatus == ResultsStatus.Provisional && ev.ResultsProvisionalAt is DateTime at
            ? at.AddMinutes(ev.Policy.ComplaintWindowMinutes)
            : null;

    public async Task<ComplaintDto> CreateAsync(int resultId, CreateComplaintRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Hãy đăng nhập để gửi khiếu nại.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        var preview = await _complaints.GetResultForComplaintAsync(resultId, ct) ?? throw new NotFoundException("Không tìm thấy kết quả.");
        // Cùng khóa với công bố chính thức: không thể vừa chốt kết quả vừa nhận thêm khiếu nại
        await _uow.LockAsync(RaceDayLocks.Results(preview.Registration.EventId), ct);
        var result = await _complaints.GetResultForComplaintAsync(resultId, ct) ?? throw new NotFoundException("Không tìm thấy kết quả.");
        var registration = result.Registration;
        var ev = registration.Event;

        if (userId != registration.Participant.LinkedUserId && userId != registration.RegisteredByUserId)
            throw new ForbiddenException("Chỉ VĐV hoặc người đăng ký hộ được khiếu nại kết quả này.");
        if (registration.Status != RegistrationStatus.Confirmed)
            throw new BusinessRuleException("BR-27", "Đăng ký không còn hiệu lực – không thể khiếu nại.");
        var endsAt = WindowEndsAt(ev);
        if (endsAt is null)
            throw new BusinessRuleException("BR-27", ev.ResultsStatus == ResultsStatus.Official
                ? "Kết quả đã chính thức – không nhận khiếu nại."
                : "Kết quả chưa công bố – chưa nhận khiếu nại.");
        if (_clock.Now > endsAt)
            throw new BusinessRuleException("BR-27", $"Đã hết thời gian nhận khiếu nại (đến {endsAt:HH:mm dd/MM/yyyy}).");
        if (await _complaints.HasOpenAsync(resultId, userId, ct))
            throw new BusinessRuleException("BR-27", "Bạn đã có khiếu nại đang chờ xử lý cho kết quả này.");

        var complaint = new ResultComplaint
        {
            ResultId = resultId,
            UserId = userId,
            Content = request.Content.Trim(),
            EvidenceUrl = string.IsNullOrWhiteSpace(request.EvidenceUrl) ? null : request.EvidenceUrl.Trim(),
            FeeAmount = ev.Policy.ComplaintFee,
            Status = ComplaintStatus.Open,
            CreatedAt = _clock.Now
        };
        await _complaints.AddAsync(complaint, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(complaint, result);
    }

    public async Task<IReadOnlyList<ComplaintDto>> GetMineAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Hãy đăng nhập.");
        return await Project(_complaints.QueryByUser(userId).OrderByDescending(c => c.CreatedAt)).ToListAsync(ct);
    }

    public async Task<EventComplaintsDto> GetForEventAsync(int eventId, string? status, CancellationToken ct = default)
    {
        var ev = await _complaints.GetEventWithPolicyAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId);

        var query = _complaints.QueryByEvent(eventId);
        if (Enum.TryParse<ComplaintStatus>(status, ignoreCase: true, out var parsed)) query = query.Where(c => c.Status == parsed);
        var items = await Project(query
                .OrderBy(c => c.Status == ComplaintStatus.Open ? 0 : 1)
                .ThenBy(c => c.CreatedAt))
            .ToListAsync(ct);

        return new EventComplaintsDto
        {
            EventId = ev.EventId,
            EventName = ev.Name,
            ResultsStatus = ev.ResultsStatus.ToString(),
            ComplaintWindowEndsAt = WindowEndsAt(ev),
            CanResolve = ev.ResultsStatus != ResultsStatus.Official,
            Items = items
        };
    }

    public async Task<ComplaintDto> ResolveAsync(int complaintId, ResolveComplaintRequest request, CancellationToken ct = default)
    {
        var complaint = await _complaints.GetTrackedAsync(complaintId, ct) ?? throw new NotFoundException("Không tìm thấy khiếu nại.");
        var registration = complaint.Result.Registration;
        var ev = registration.Event;
        _scope.EnsureCanManage(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        _scope.EnsureNotOwnRegistration(registration.Participant.LinkedUserId, registration.RegisteredByUserId);

        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RaceDayLocks.Results(ev.EventId), ct);
        await _complaints.ReloadAsync(complaint, ct);
        var current = await _complaints.GetEventWithPolicyAsync(ev.EventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        if (current.ResultsStatus == ResultsStatus.Official)
            throw new BusinessRuleException("BR-27", "Kết quả đã chính thức – không xử lý khiếu nại nữa.");
        if (complaint.Status != ComplaintStatus.Open)
            throw new BusinessRuleException(ErrorCodes.Conflict, "Khiếu nại đã được xử lý.");

        complaint.Status = Enum.Parse<ComplaintStatus>(request.Decision, ignoreCase: true);
        complaint.Response = request.Response.Trim();
        complaint.HandledById = _currentUser.UserId;
        complaint.HandledAt = _clock.Now;
        await _audit.LogAsync("Result.ComplaintResolve", nameof(ResultComplaint), complaintId.ToString(),
            new { Status = nameof(ComplaintStatus.Open) },
            new { Status = complaint.Status.ToString(), complaint.ResultId, registration.BibNumber, complaint.Response }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(complaint, complaint.Result);
    }

    private static IQueryable<ComplaintDto> Project(IQueryable<ResultComplaint> query) =>
        query.Select(c => new ComplaintDto
        {
            Id = c.ComplaintId,
            ResultId = c.ResultId,
            RegistrationId = c.Result.RegistrationId,
            EventId = c.Result.Registration.EventId,
            EventName = c.Result.Registration.Event.Name,
            CategoryName = c.Result.Registration.Category.Name,
            BibNumber = c.Result.Registration.BibNumber,
            RunnerName = c.Result.Registration.FullName,
            ResultStatus = c.Result.Status == ResultStatus.Finished ? "Finished"
                : c.Result.Status == ResultStatus.DNF ? "DNF"
                : c.Result.Status == ResultStatus.DSQ ? "DSQ"
                : "DNS",
            Content = c.Content,
            EvidenceUrl = c.EvidenceUrl,
            FeeAmount = c.FeeAmount,
            Status = c.Status == ComplaintStatus.Open ? "Open"
                : c.Status == ComplaintStatus.Accepted ? "Accepted"
                : "Rejected",
            Response = c.Response,
            CreatedAt = c.CreatedAt,
            HandledAt = c.HandledAt
        });

    private static ComplaintDto ToDto(ResultComplaint c, Result r) => new()
    {
        Id = c.ComplaintId,
        ResultId = c.ResultId,
        RegistrationId = r.RegistrationId,
        EventId = r.Registration.EventId,
        EventName = r.Registration.Event.Name,
        CategoryName = r.Registration.Category.Name,
        BibNumber = r.Registration.BibNumber,
        RunnerName = r.Registration.FullName,
        ResultStatus = r.Status.ToString(),
        Content = c.Content,
        EvidenceUrl = c.EvidenceUrl,
        FeeAmount = c.FeeAmount,
        Status = c.Status.ToString(),
        Response = c.Response,
        CreatedAt = c.CreatedAt,
        HandledAt = c.HandledAt
    };
}
