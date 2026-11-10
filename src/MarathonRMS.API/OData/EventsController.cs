using MarathonRMS.DTOs.Events;
using MarathonRMS.Services.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace MarathonRMS.API.OData;

/// <summary>
/// UC-34 – danh sách giải công khai.
/// GET /odata/Events?$filter=City eq 'Hà Nội'&amp;$orderby=EventDate&amp;$top=12&amp;$skip=0&amp;$count=true
/// </summary>
[AllowAnonymous]
public class EventsController : ODataController
{
    private readonly IEventService _events;
    public EventsController(IEventService events) => _events = events;

    [EnableQuery(PageSize = 100, MaxTop = 100,
        AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy | AllowedQueryOptions.Top
                              | AllowedQueryOptions.Skip | AllowedQueryOptions.Count | AllowedQueryOptions.Select)]
    public IQueryable<EventSummaryDto> Get() => _events.QueryPublic();
}
