using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Services.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace MarathonRMS.API.OData;

/// <summary>UC-48 – danh sách đăng ký thuộc đúng BTC của Organizer đang gọi.</summary>
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class RegistrationsController : ODataController
{
    private readonly IRegistrationService _registrations;
    public RegistrationsController(IRegistrationService registrations) => _registrations = registrations;

    [EnableQuery(PageSize = 100, MaxTop = 100,
        AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy | AllowedQueryOptions.Top
                              | AllowedQueryOptions.Skip | AllowedQueryOptions.Count | AllowedQueryOptions.Select)]
    public IQueryable<RegistrationListItemDto> Get() => _registrations.QueryForOrganizer();
}
