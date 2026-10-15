namespace MarathonRMS.DTOs.Common;

/// <summary>Tên role và policy dùng chung cho API và WebApp.</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Organizer = "Organizer";
    public const string Staff = "Staff";
    public const string Member = "Member";

    public static readonly string[] All = { SuperAdmin, Admin, Organizer, Staff, Member };
}

public static class AppPolicies
{
    public const string SuperAdminOnly = nameof(SuperAdminOnly);
    public const string AdminOrAbove = nameof(AdminOrAbove);
    public const string OrganizerOnly = nameof(OrganizerOnly);
    public const string StaffOrOrganizer = nameof(StaffOrOrganizer);
    public const string MemberOnly = nameof(MemberOnly);
}

public static class AppClaims
{
    /// <summary>Id BTC của Organizer/Staff (BR-30).</summary>
    public const string OrganizationId = "org_id";
    /// <summary>JWT được WebApp cất trong cookie đăng nhập.</summary>
    public const string AccessToken = "access_token";
}
