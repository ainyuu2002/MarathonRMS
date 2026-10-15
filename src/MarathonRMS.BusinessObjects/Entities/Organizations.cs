using MarathonRMS.BusinessObjects.Enums;

namespace MarathonRMS.BusinessObjects.Entities;

public class Organization
{
    public int OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public string ContactEmail { get; set; } = "";
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<AppUser> Members { get; set; } = new List<AppUser>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
}

/// <summary>Hồ sơ đăng ký làm BTC – Member nộp, Admin duyệt (BR-33).</summary>
public class OrganizationApplication
{
    public int ApplicationId { get; set; }
    public string ApplicantId { get; set; } = "";
    public AppUser Applicant { get; set; } = null!;
    public string OrganizationName { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string RepresentativeName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? DocumentUrl { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public string? RejectReason { get; set; }
    public string? ReviewedById { get; set; }
    public AppUser? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

public class Venue
{
    public int VenueId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
