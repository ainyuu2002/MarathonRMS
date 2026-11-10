using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Services.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace MarathonRMS.API.Controllers;

/// <summary>
/// Phần A – UC-38 (GĐ3): danh sách VĐV công khai, chỉ dữ liệu tối thiểu (BR-37):
/// GET /odata/Participants?$filter=EventId eq 2 and contains(DisplayName,'An')&amp;$orderby=BibNumber&amp;$top=50&amp;$count=true
/// Hồ sơ người chạy của tôi (REST /api/participants/me) ở MyParticipantController.
/// </summary>
[AllowAnonymous]
public class ParticipantsController : ODataController
{
    private readonly IPublicRunnerService _runners;
    public ParticipantsController(IPublicRunnerService runners) => _runners = runners;

    [EnableQuery(PageSize = 100, MaxTop = 100,
        AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy | AllowedQueryOptions.Top
                              | AllowedQueryOptions.Skip | AllowedQueryOptions.Count | AllowedQueryOptions.Select)]
    public IQueryable<PublicRunnerDto> Get() => _runners.Query();
}
