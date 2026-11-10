using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>Phần A – UC-38: danh sách VĐV công khai (OData, BR-37).</summary>
public class PublicRunnerApiClient : ApiClientBase
{
    public PublicRunnerApiClient(HttpClient http) : base(http) { }

    public Task<PagedResult<PublicRunnerDto>> SearchAsync(string filter, int page, CancellationToken ct = default) =>
        GetODataPageAsync<PublicRunnerDto>("Participants", filter, "CategoryName,BibNumber,Id", page, 50, ct);
}
