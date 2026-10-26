using MarathonRMS.DTOs.Common;
using MarathonRMS.Repositories;

namespace MarathonRMS.Services.Common;

/// <summary>
/// Kiểm tra phạm vi BTC (BR-30) và xung đột lợi ích (BR-32) – dùng chung cho mọi Service của B và C.
/// Ném ForbiddenException (→ 403) khi vi phạm.
/// </summary>
public interface IOrganizationScope
{
    /// <summary>Organizer của đúng BTC sở hữu (hoặc Admin/SuperAdmin) – dùng cho thao tác thiết lập, kết quả.</summary>
    void EnsureCanManage(int organizationId);

    /// <summary>Staff hoặc Organizer của đúng BTC (hoặc Admin/SuperAdmin) – dùng cho race kit, bấm giờ.</summary>
    void EnsureCanOperate(int organizationId);

    /// <summary>BR-32: không thao tác (ghi thời gian, phát kit, phạt, sửa kết quả…) trên đăng ký của chính mình.</summary>
    void EnsureNotOwnRegistration(string? runnerLinkedUserId, string? registeredByUserId);

    /// <summary>Id BTC của người gọi; ném 403 nếu không thuộc BTC nào.</summary>
    int RequireOrganizationId();

    /// <summary>BTC bị Admin tạm khóa (UC-10) ⇒ Organizer/Staff không được thao tác ghi trên giải của BTC đó. Admin bỏ qua.</summary>
    Task EnsureOrganizationActiveAsync(int organizationId, CancellationToken ct = default);
}

public class OrganizationScope : IOrganizationScope
{
    private readonly ICurrentUserService _currentUser;
    private readonly IOrganizationRepository _organizations;

    public OrganizationScope(ICurrentUserService currentUser, IOrganizationRepository organizations)
    {
        _currentUser = currentUser;
        _organizations = organizations;
    }

    public void EnsureCanManage(int organizationId)
    {
        if (_currentUser.IsAdminOrAbove) return;
        if (!_currentUser.IsInRole(AppRoles.Organizer) || _currentUser.OrganizationId != organizationId)
            throw new ForbiddenException("Bạn chỉ được quản lý dữ liệu của ban tổ chức mình.", "BR-30");
    }

    public void EnsureCanOperate(int organizationId)
    {
        if (_currentUser.IsAdminOrAbove) return;
        var isOperator = _currentUser.IsInRole(AppRoles.Organizer) || _currentUser.IsInRole(AppRoles.Staff);
        if (!isOperator || _currentUser.OrganizationId != organizationId)
            throw new ForbiddenException("Bạn chỉ được thao tác trên giải của ban tổ chức mình.", "BR-30");
    }

    public void EnsureNotOwnRegistration(string? runnerLinkedUserId, string? registeredByUserId)
    {
        var me = _currentUser.UserId;
        if (me is null) return;
        if (me == runnerLinkedUserId || me == registeredByUserId)
            throw new ForbiddenException("Không được tự thao tác trên đăng ký của chính mình hoặc do mình đăng ký.", "BR-32");
    }

    public int RequireOrganizationId() =>
        _currentUser.OrganizationId ?? throw new ForbiddenException("Tài khoản không thuộc ban tổ chức nào.", "BR-30");

    public async Task EnsureOrganizationActiveAsync(int organizationId, CancellationToken ct = default)
    {
        if (_currentUser.IsAdminOrAbove) return;
        var org = await _organizations.GetByIdAsync(organizationId, ct) ?? throw new NotFoundException("Không tìm thấy ban tổ chức.");
        if (!org.IsActive)
            throw new ForbiddenException("Ban tổ chức đang bị tạm khóa. Liên hệ quản trị viên nền tảng.", "BR-30");
    }
}
