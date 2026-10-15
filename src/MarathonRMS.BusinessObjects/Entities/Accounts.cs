using MarathonRMS.BusinessObjects.Enums;
using Microsoft.AspNetCore.Identity;

namespace MarathonRMS.BusinessObjects.Entities;

/// <summary>Tài khoản đăng nhập (Identity). Organizer/Staff thuộc đúng 1 BTC qua OrganizationId (BR-30).</summary>
public class AppUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<Participant> Participants { get; set; } = new List<Participant>();
}

/// <summary>
/// Người chạy. Dòng Relationship = Self là hồ sơ chính chủ của tài khoản;
/// các dòng Child/Relative là người phụ thuộc không có tài khoản (D14).
/// </summary>
public class Participant
{
    public int ParticipantId { get; set; }
    public string OwnerUserId { get; set; } = "";
    public AppUser Owner { get; set; } = null!;
    public string? LinkedUserId { get; set; }
    public AppUser? LinkedUser { get; set; }
    public ParticipantRelationship Relationship { get; set; }

    public string FullName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? Nationality { get; set; }
    public IdType? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string? Phone { get; set; }
    public TShirtSize? TShirtSize { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? BloodType { get; set; }
    public string? MedicalNotes { get; set; }
    public string? ClubName { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
