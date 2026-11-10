using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Registrations;

/// <summary>Phần A – UC-38 (GĐ3): danh sách VĐV công khai của giải (BR-37).</summary>
public interface IPublicRunnerService
{
    /// <summary>Nguồn cho GET /odata/Participants. Che tên trong SQL để $filter theo tên không lộ người đã ẩn.</summary>
    IQueryable<PublicRunnerDto> Query();
}

public class PublicRunnerService : IPublicRunnerService
{
    private readonly IPublicRunnerRepository _repo;
    public PublicRunnerService(IPublicRunnerRepository repo) => _repo = repo;

    // Cùng quy tắc với IPrivacyFilter.PublicDisplayName và bảng xếp hạng (UC-62): ẩn khi người chạy chọn ẩn hoặc dưới 18 tuổi tại ngày thi
    public IQueryable<PublicRunnerDto> Query() =>
        _repo.QueryConfirmedPublic().Select(r => new PublicRunnerDto
        {
            Id = r.RegistrationId,
            EventId = r.EventId,
            CategoryId = r.CategoryId,
            CategoryName = r.Category.Name,
            BibNumber = r.BibNumber,
            DisplayName = r.HideFromPublicList || r.DateOfBirth > r.Event.EventDate.AddYears(-PrivacyFilter.AdultAge)
                ? (r.BibNumber != null ? "VĐV #" + r.BibNumber.ToString() : "VĐV ẩn danh")
                : r.FullName,
            Gender = r.Gender == Gender.F ? "F" : "M",
            ClubName = r.HideFromPublicList || r.DateOfBirth > r.Event.EventDate.AddYears(-PrivacyFilter.AdultAge)
                ? null
                : r.ClubName
        });
}
