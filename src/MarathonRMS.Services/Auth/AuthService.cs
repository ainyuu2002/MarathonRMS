using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Auth;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.AspNetCore.Identity;

namespace MarathonRMS.Services.Auth;

public interface IAuthService
{
    Task<CurrentUserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<CurrentUserDto> GetCurrentUserAsync(string userId, CancellationToken ct = default);
    /// <summary>Cấp lại JWT với role/BTC hiện tại – dùng sau khi được duyệt làm BTC, được mời làm Staff, đổi quyền.</summary>
    Task<AuthResponse> RefreshAsync(string userId, CancellationToken ct = default);
    /// <summary>UC-06: đổi mật khẩu (kiểm tra mật khẩu hiện tại, áp quy tắc độ mạnh của Identity, ghi nhật ký).</summary>
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly ITokenService _tokens;
    private readonly IParticipantRepository _participants;
    private readonly IConsentRecordRepository _consents;
    private readonly ILegalDocumentRepository _legal;
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;

    public AuthService(UserManager<AppUser> users, SignInManager<AppUser> signIn, ITokenService tokens,
        IParticipantRepository participants, IConsentRecordRepository consents, ILegalDocumentRepository legal,
        IOrganizationRepository organizations, IUnitOfWork uow, IDateTimeProvider clock, IAuditLogger audit)
    {
        _audit = audit;
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
        _participants = participants;
        _consents = consents;
        _legal = legal;
        _organizations = organizations;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>UC-01: tạo tài khoản Member + hồ sơ người chạy chính chủ + bản ghi đồng ý (BR-09).</summary>
    public async Task<CurrentUserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (!request.AcceptTerms || !request.AcceptPrivacy)
            throw new BusinessRuleException("BR-09", "Bạn phải đồng ý Điều khoản sử dụng và Chính sách quyền riêng tư.", 400);
        if (await _users.FindByEmailAsync(request.Email) is not null)
            throw new BusinessRuleException(ErrorCodes.EmailTaken, "Email đã được sử dụng.");

        var terms = await _legal.GetCurrentAsync(LegalDocType.Terms, ct)
                    ?? throw new InvalidOperationException("Chưa có Điều khoản sử dụng.");
        var privacy = await _legal.GetCurrentAsync(LegalDocType.Privacy, ct)
                      ?? throw new InvalidOperationException("Chưa có Chính sách quyền riêng tư.");

        var now = _clock.Now;
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName.Trim(),
            CreatedAt = now,
            IsActive = true
        };

        await using var tx = await _uow.BeginTransactionAsync(ct);
        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new BusinessRuleException(ErrorCodes.Validation, string.Join(" ", created.Errors.Select(e => e.Description)), 400);
        await _users.AddToRoleAsync(user, AppRoles.Member);

        await _participants.AddAsync(new Participant
        {
            OwnerUserId = user.Id,
            LinkedUserId = user.Id,
            Relationship = ParticipantRelationship.Self,
            FullName = user.FullName,
            CreatedAt = now
        }, ct);
        await _consents.AddAsync(Consent(user.Id, ConsentType.Terms, terms.Version, now), ct);
        await _consents.AddAsync(Consent(user.Id, ConsentType.Privacy, privacy.Version, now), ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetCurrentUserAsync(user.Id, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email)
                   ?? throw new BusinessRuleException(ErrorCodes.InvalidCredentials, "Email hoặc mật khẩu không đúng.", 401);
        if (!user.IsActive)
            throw new BusinessRuleException(ErrorCodes.AccountDisabled, "Tài khoản đã bị khóa bởi quản trị viên.", 403);

        var result = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            throw new BusinessRuleException(ErrorCodes.AccountLocked, "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Thử lại sau.", 403);
        if (!result.Succeeded)
            throw new BusinessRuleException(ErrorCodes.InvalidCredentials, "Email hoặc mật khẩu không đúng.", 401);

        var roles = await _users.GetRolesAsync(user);
        var (token, expires) = _tokens.CreateToken(user, roles);
        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAt = expires,
            User = await ToDtoAsync(user, roles, ct)
        };
    }

    public async Task<AuthResponse> RefreshAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        if (!user.IsActive)
            throw new BusinessRuleException(ErrorCodes.AccountDisabled, "Tài khoản đã bị khóa bởi quản trị viên.", 403);
        var roles = await _users.GetRolesAsync(user);
        var (token, expires) = _tokens.CreateToken(user, roles);
        return new AuthResponse { AccessToken = token, ExpiresAt = expires, User = await ToDtoAsync(user, roles, ct) };
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        if (request.NewPassword == request.CurrentPassword)
            throw new BusinessRuleException(ErrorCodes.Validation, "Mật khẩu mới phải khác mật khẩu hiện tại.", 400);

        var result = await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var message = result.Errors.Any(e => e.Code == "PasswordMismatch")
                ? "Mật khẩu hiện tại không đúng."
                : "Mật khẩu mới chưa đủ mạnh: tối thiểu 8 ký tự, có chữ hoa, chữ thường và chữ số.";
            throw new BusinessRuleException(ErrorCodes.Validation, message, 400);
        }
        // Không lưu mật khẩu vào nhật ký – chỉ ghi nhận sự kiện
        await _audit.LogAsync("Account.ChangePassword", nameof(AppUser), user.Id, ct: ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        return await ToDtoAsync(user, await _users.GetRolesAsync(user), ct);
    }

    private async Task<CurrentUserDto> ToDtoAsync(AppUser user, IList<string> roles, CancellationToken ct)
    {
        var org = user.OrganizationId is int id ? await _organizations.GetByIdAsync(id, ct) : null;
        return new CurrentUserDto
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName,
            Roles = roles,
            OrganizationId = user.OrganizationId,
            OrganizationName = org?.Name
        };
    }

    private static ConsentRecord Consent(string userId, ConsentType type, int version, DateTime now) =>
        new() { UserId = userId, ConsentType = type, DocumentVersion = version, Granted = true, CreatedAt = now };
}
