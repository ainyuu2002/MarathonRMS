using MarathonRMS.DTOs.Events;
using MarathonRMS.Services.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace MarathonRMS.API.Controllers;

/// <summary>
/// UC-34 – danh sách giải công khai (không gồm giải nháp):
/// GET /odata/Events?$filter=City eq 'Hà Nội' and EventDate ge 2026-11-10&amp;$orderby=EventDate&amp;$top=12&amp;$count=true
/// Quản lý giải (REST) ở EventManagementController.
/// </summary>
[AllowAnonymous]
public class EventsController : ODataController
{
    private readonly IEventService _events;
    public EventsController(IEventService events) => _events = events;

    [EnableQuery(PageSize = 50, MaxTop = 50,
        AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy | AllowedQueryOptions.Top
                              | AllowedQueryOptions.Skip | AllowedQueryOptions.Count | AllowedQueryOptions.Select)]
    public IQueryable<EventSummaryDto> Get() => _events.QueryPublic();
}
