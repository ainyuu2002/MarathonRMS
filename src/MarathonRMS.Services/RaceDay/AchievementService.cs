using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Results;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.RaceDay;

/// <summary>
/// UC-64 – hồ sơ thành tích: các giải đã chạy, thành tích tốt nhất theo cự ly, e-certificate, tùy chọn công khai.
/// Công khai dùng đồng ý tài khoản "hiện tên công khai" (ConsentType.PublicListing – UC-04 của A) và BR-37:
/// người dưới 18 không có hồ sơ công khai; đăng ký chọn ẩn tên / lúc chạy chưa đủ 18 tuổi không hiện trên hồ sơ công khai.
/// </summary>
public interface IAchievementService
{
    Task<AchievementProfileDto> GetMineAsync(CancellationToken ct = default);
    Task<AchievementProfileDto> GetPublicAsync(int participantId, CancellationToken ct = default);
    Task<CertificateDto> GetCertificateAsync(int registrationId, CancellationToken ct = default);
}

public class AchievementService : IAchievementService
{
    private readonly IAchievementRepository _achievements;
    private readonly IConsentRecordRepository _consents;
    private readonly ICurrentUserService _currentUser;
    private readonly IPrivacyFilter _privacy;
    private readonly IDateTimeProvider _clock;

    public AchievementService(IAchievementRepository achievements, IConsentRecordRepository consents,
        ICurrentUserService currentUser, IPrivacyFilter privacy, IDateTimeProvider clock)
    {
        _achievements = achievements;
        _consents = consents;
        _currentUser = currentUser;
        _privacy = privacy;
        _clock = clock;
    }

    public async Task<AchievementProfileDto> GetMineAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Hãy đăng nhập.");
        var participant = await _achievements.GetSelfParticipantAsync(userId, ct);
        if (participant is null) return new AchievementProfileDto();

        var results = await _achievements.GetPublishedByParticipantAsync(participant.ParticipantId, ct);
        return Build(participant, results, await IsPublicAsync(participant, ct));
    }

    public async Task<AchievementProfileDto> GetPublicAsync(int participantId, CancellationToken ct = default)
    {
        var participant = await _achievements.GetParticipantAsync(participantId, ct);
        if (participant is null || !await IsPublicAsync(participant, ct))
            throw new NotFoundException("Hồ sơ thành tích không tồn tại hoặc không công khai.");

        var results = (await _achievements.GetPublishedByParticipantAsync(participantId, ct))
            .Where(r => !r.Registration.HideFromPublicList &&
                        !_privacy.IsMinor(r.Registration.DateOfBirth, r.Registration.Event.EventDate))
            .ToList();
        var profile = Build(participant, results, isPublic: true);
        profile.ParticipantId = participant.ParticipantId;
        return profile;
    }

    public async Task<CertificateDto> GetCertificateAsync(int registrationId, CancellationToken ct = default)
    {
        var result = await _achievements.GetForCertificateAsync(registrationId, ct)
                     ?? throw new NotFoundException("Không có e-certificate cho đăng ký này.");
        var g = result.Registration;
        var ev = g.Event;
        if (result.Status != ResultStatus.Finished || ev.ResultsStatus != ResultsStatus.Official ||
            g.Status != RegistrationStatus.Confirmed)
            throw new NotFoundException("E-certificate chỉ có khi VĐV về đích và kết quả đã chính thức.");

        var me = _currentUser.UserId;
        var isOwner = me is not null && (me == g.Participant.LinkedUserId || me == g.RegisteredByUserId);
        var isPublicName = !g.HideFromPublicList && !_privacy.IsMinor(g.DateOfBirth, ev.EventDate);
        if (!isOwner && !IsOperatorOf(ev.OrganizationId) && !isPublicName)
            throw new NotFoundException("Không có e-certificate cho đăng ký này.");

        return new CertificateDto
        {
            RegistrationId = g.RegistrationId,
            RunnerName = g.FullName,
            EventName = ev.Name,
            OrganizationName = ev.Organization.Name,
            EventDate = ev.EventDate,
            VenueName = ev.Venue.Name,
            City = ev.Venue.City,
            CategoryName = g.Category.Name,
            DistanceKm = g.Category.DistanceKm,
            BibNumber = g.BibNumber,
            FinalTimeMs = result.FinalTimeMs,
            GunTimeMs = result.GunTimeMs,
            ChipTimeMs = result.ChipTimeMs,
            OverallRank = result.OverallRank,
            GenderRank = result.GenderRank,
            Gender = g.Gender.ToString(),
            AgeGroupName = result.AgeGroup?.Name,
            AgeGroupRank = result.AgeGroupRank,
            FinishersInCategory = await _achievements.CountFinishersAsync(g.CategoryId, ct),
            OfficialAt = ev.ResultsOfficialAt
        };
    }

    /// <summary>Có tài khoản, đã bật "hiện tên công khai" và đủ 18 tuổi (BR-37).</summary>
    private async Task<bool> IsPublicAsync(Participant participant, CancellationToken ct)
    {
        if (participant.LinkedUserId is null) return false;
        if (participant.DateOfBirth is DateOnly dob && _privacy.IsMinor(dob, _clock.Today)) return false;
        var latest = await _consents.GetLatestPerTypeAsync(participant.LinkedUserId, ct);
        return latest.Any(c => c.ConsentType == ConsentType.PublicListing && c.Granted && c.WithdrawnAt == null);
    }

    private bool IsOperatorOf(int organizationId) =>
        _currentUser.IsAdminOrAbove ||
        ((_currentUser.IsInRole(AppRoles.Organizer) || _currentUser.IsInRole(AppRoles.Staff)) &&
         _currentUser.OrganizationId == organizationId);

    private static AchievementProfileDto Build(Participant participant, IReadOnlyList<Result> results, bool isPublic)
    {
        var items = results.Select(r => new AchievementItemDto
        {
            RegistrationId = r.RegistrationId,
            EventId = r.Registration.EventId,
            EventName = r.Registration.Event.Name,
            EventDate = r.Registration.Event.EventDate,
            CategoryName = r.Registration.Category.Name,
            Discipline = r.Registration.Category.Discipline.ToString(),
            DistanceKm = r.Registration.Category.DistanceKm,
            BibNumber = r.Registration.BibNumber,
            Status = r.Status.ToString(),
            FinalTimeMs = r.FinalTimeMs,
            OverallRank = r.OverallRank,
            GenderRank = r.GenderRank,
            AgeGroupName = r.AgeGroup?.Name,
            AgeGroupRank = r.AgeGroupRank,
            IsProvisional = r.Registration.Event.ResultsStatus == ResultsStatus.Provisional,
            HasCertificate = r.Status == ResultStatus.Finished && r.Registration.Event.ResultsStatus == ResultsStatus.Official
        }).ToList();

        var finished = items.Where(i => i.Status == nameof(ResultStatus.Finished)).ToList();
        return new AchievementProfileDto
        {
            ParticipantId = participant.ParticipantId,
            DisplayName = participant.FullName,
            ClubName = participant.ClubName,
            IsPublic = isPublic,
            FinishedCount = finished.Count,
            TotalDistanceKm = finished.Sum(i => i.DistanceKm),
            // Thành tích tốt nhất: chỉ kết quả có thời gian (cự ly bấm giờ), theo loại đường + cự ly
            PersonalBests = finished.Where(i => i.FinalTimeMs is not null)
                .GroupBy(i => (i.Discipline, i.DistanceKm))
                .Select(g => g.OrderBy(i => i.FinalTimeMs).First())
                .OrderBy(i => i.Discipline).ThenByDescending(i => i.DistanceKm)
                .Select(i => new PersonalBestDto
                {
                    Discipline = i.Discipline,
                    DistanceKm = i.DistanceKm,
                    FinalTimeMs = i.FinalTimeMs!.Value,
                    RegistrationId = i.RegistrationId,
                    EventName = i.EventName,
                    EventDate = i.EventDate
                }).ToList(),
            Items = items
        };
    }
}
