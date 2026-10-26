using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Accounts;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.AspNetCore.Identity;

namespace MarathonRMS.Services.Accounts;

/// <summary>UC-03 – hồ sơ người chạy chính chủ.</summary>
public interface IProfileService
{
    Task<ParticipantProfileDto> GetMineAsync(CancellationToken ct = default);
    Task<ParticipantProfileDto> UpdateMineAsync(UpdateProfileRequest request, CancellationToken ct = default);
}

public class ProfileService : IProfileService
{
    private readonly IParticipantRepository _participants;
    private readonly IConsentService _consents;
    private readonly UserManager<AppUser> _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public ProfileService(IParticipantRepository participants, IConsentService consents, UserManager<AppUser> users,
        ICurrentUserService currentUser, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _participants = participants;
        _consents = consents;
        _users = users;
        _currentUser = currentUser;
        _clock = clock;
        _uow = uow;
    }

    public async Task<ParticipantProfileDto> GetMineAsync(CancellationToken ct = default)
    {
        var (user, self) = await LoadAsync(ct);
        return ToDto(self, user.Email);
    }

    public async Task<ParticipantProfileDto> UpdateMineAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var (user, self) = await LoadAsync(ct);

        if (request.DateOfBirth is DateOnly dob && (dob > _clock.Today || dob.Year < 1900))
            throw new BusinessRuleException("BR-05", "Ngày sinh không hợp lệ.", 400);
        if (!string.IsNullOrWhiteSpace(request.IdNumber) && string.IsNullOrWhiteSpace(request.IdType))
            throw new BusinessRuleException("BR-07", "Vui lòng chọn loại giấy tờ.", 400);

        // Dữ liệu sức khỏe là dữ liệu nhạy cảm (NĐ 356 – Điều 4) ⇒ phải có đồng ý riêng (BR-09)
        var hasHealthData = !string.IsNullOrWhiteSpace(request.BloodType) || !string.IsNullOrWhiteSpace(request.MedicalNotes);
        if (hasHealthData && !request.HealthDataConsent && !await _consents.HasGrantedAsync(ConsentType.HealthData, ct))
            throw new BusinessRuleException("BR-09", "Bạn cần đồng ý cho phép xử lý dữ liệu sức khỏe trước khi lưu nhóm máu/ghi chú y tế.", 400);

        self.FullName = request.FullName.Trim();
        self.DateOfBirth = request.DateOfBirth;
        self.Gender = EnumParser.ParseOptional<Gender>(request.Gender);
        self.Nationality = Trim(request.Nationality);
        self.IdType = EnumParser.ParseOptional<IdType>(request.IdType);
        self.IdNumber = Trim(request.IdNumber)?.ToUpperInvariant();
        self.Phone = Trim(request.Phone);
        self.TShirtSize = EnumParser.ParseOptional<TShirtSize>(request.TShirtSize);
        self.EmergencyContactName = Trim(request.EmergencyContactName);
        self.EmergencyContactPhone = Trim(request.EmergencyContactPhone);
        self.BloodType = Trim(request.BloodType);
        self.MedicalNotes = Trim(request.MedicalNotes);
        self.ClubName = Trim(request.ClubName);
        user.FullName = self.FullName;

        if (hasHealthData && request.HealthDataConsent)
            await _consents.RecordAsync(ConsentType.HealthData, granted: true, ct);

        await _users.UpdateAsync(user);
        await _uow.SaveChangesAsync(ct);
        return ToDto(self, user.Email);
    }

    private async Task<(AppUser User, Participant Self)> LoadAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Chưa đăng nhập.");
        var user = await _users.FindByIdAsync(userId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        var self = await _participants.GetSelfAsync(userId, ct);
        if (self is null)
        {
            // Tài khoản cũ chưa có hồ sơ chính chủ (VD Admin) – tạo khi cần
            self = new Participant
            {
                OwnerUserId = userId, LinkedUserId = userId, Relationship = ParticipantRelationship.Self,
                FullName = user.FullName, CreatedAt = _clock.Now
            };
            await _participants.AddAsync(self, ct);
        }
        return (user, self);
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static ParticipantProfileDto ToDto(Participant p, string? email) => new()
    {
        ParticipantId = p.ParticipantId,
        Relationship = p.Relationship.ToString(),
        FullName = p.FullName,
        DateOfBirth = p.DateOfBirth,
        Gender = p.Gender?.ToString(),
        Nationality = p.Nationality,
        IdType = p.IdType?.ToString(),
        IdNumber = p.IdNumber,
        Phone = p.Phone,
        Email = email,
        TShirtSize = p.TShirtSize?.ToString(),
        EmergencyContactName = p.EmergencyContactName,
        EmergencyContactPhone = p.EmergencyContactPhone,
        BloodType = p.BloodType,
        MedicalNotes = p.MedicalNotes,
        ClubName = p.ClubName,
        IsCompleteForRegistration = p.DateOfBirth is not null && p.Gender is not null && !string.IsNullOrEmpty(p.Nationality)
                                    && !string.IsNullOrEmpty(p.Phone) && p.TShirtSize is not null
                                    && !string.IsNullOrEmpty(p.EmergencyContactName) && !string.IsNullOrEmpty(p.EmergencyContactPhone)
    };
}

/// <summary>UC-04 – các đồng ý xử lý dữ liệu theo từng mục đích (Luật 91/2025 – Điều 9, 10; BR-09).</summary>
public interface IConsentService
{
    Task<IReadOnlyList<ConsentStatusDto>> GetMineAsync(CancellationToken ct = default);
    /// <summary>Bật/tắt đồng ý tùy chọn. Rút lại ⇒ ghi WithdrawnAt (không xóa – Điều 10 khoản 4).</summary>
    Task<IReadOnlyList<ConsentStatusDto>> SetAsync(SetConsentRequest request, CancellationToken ct = default);
    Task<bool> HasGrantedAsync(ConsentType type, CancellationToken ct = default);
    /// <summary>Thêm bản ghi đồng ý cấp tài khoản (không SaveChanges – gọi trong transaction của nghiệp vụ).</summary>
    Task RecordAsync(ConsentType type, bool granted, CancellationToken ct = default);
}

public class ConsentService : IConsentService
{
    private static readonly (ConsentType Type, string Description, bool Required)[] AccountConsents =
    {
        (ConsentType.Terms, "Điều khoản sử dụng", true),
        (ConsentType.Privacy, "Chính sách quyền riêng tư", true),
        (ConsentType.HealthData, "Cho phép xử lý dữ liệu sức khỏe (nhóm máu, ghi chú y tế) để hỗ trợ y tế khi thi đấu", false),
        (ConsentType.PhotoMarketing, "Cho phép dùng ảnh có mình cho mục đích quảng bá", false),
        (ConsentType.PublicListing, "Hiện tên mình trên danh sách VĐV và bảng xếp hạng công khai", false),
    };

    private readonly IConsentRecordRepository _records;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public ConsentService(IConsentRecordRepository records, ICurrentUserService currentUser, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _records = records;
        _currentUser = currentUser;
        _clock = clock;
        _uow = uow;
    }

    private string UserId => _currentUser.UserId ?? throw new ForbiddenException("Chưa đăng nhập.");

    public async Task<IReadOnlyList<ConsentStatusDto>> GetMineAsync(CancellationToken ct = default)
    {
        var latest = (await _records.GetLatestPerTypeAsync(UserId, ct)).ToDictionary(r => r.ConsentType);
        return AccountConsents.Select(c =>
        {
            latest.TryGetValue(c.Type, out var r);
            return new ConsentStatusDto
            {
                ConsentType = c.Type.ToString(),
                Description = c.Description,
                IsRequired = c.Required,
                Granted = r is { Granted: true, WithdrawnAt: null },
                DocumentVersion = r?.DocumentVersion,
                UpdatedAt = r?.WithdrawnAt ?? r?.CreatedAt
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<ConsentStatusDto>> SetAsync(SetConsentRequest request, CancellationToken ct = default)
    {
        var type = Enum.Parse<ConsentType>(request.ConsentType);
        var latest = (await _records.GetLatestPerTypeAsync(UserId, ct)).FirstOrDefault(r => r.ConsentType == type);
        var currentlyGranted = latest is { Granted: true, WithdrawnAt: null };

        if (request.Granted && !currentlyGranted)
            await RecordAsync(type, true, ct);
        else if (!request.Granted && currentlyGranted)
            latest!.WithdrawnAt = _clock.Now;

        await _uow.SaveChangesAsync(ct);
        return await GetMineAsync(ct);
    }

    public async Task<bool> HasGrantedAsync(ConsentType type, CancellationToken ct = default) =>
        (await _records.GetLatestPerTypeAsync(UserId, ct)).Any(r => r.ConsentType == type && r.Granted && r.WithdrawnAt is null);

    public Task RecordAsync(ConsentType type, bool granted, CancellationToken ct = default) =>
        _records.AddAsync(new ConsentRecord
        {
            UserId = UserId, ConsentType = type, DocumentVersion = 1, Granted = granted, CreatedAt = _clock.Now
        }, ct);
}
