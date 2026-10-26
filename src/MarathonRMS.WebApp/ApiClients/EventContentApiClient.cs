using System.Net.Http.Headers;
using MarathonRMS.DTOs.EventContent;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>
/// Trang giới thiệu giải (UC-88) + album (UC-89).
/// B: trang chi tiết giải gọi GetPublicAsync rồi render partial _EventContent / _EventGallery.
/// </summary>
public class EventContentApiClient : ApiClientBase
{
    public EventContentApiClient(HttpClient http) : base(http) { }

    public Task<EventContentDto> GetPublicAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<EventContentDto>($"api/events/{eventId}/content", ct);
    public Task<EventContentDto> GetForManageAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<EventContentDto>($"api/events/{eventId}/content/manage", ct);
    public Task<EventContentDto> UpdateThemeAsync(int eventId, UpdateThemeRequest request, CancellationToken ct = default) =>
        PutAsync<EventContentDto>($"api/events/{eventId}/content/theme", request, ct);

    public Task<ContentSectionDto> AddSectionAsync(int eventId, UpsertContentSectionRequest request, CancellationToken ct = default) =>
        PostAsync<ContentSectionDto>($"api/events/{eventId}/content/sections", request, ct);
    public Task<ContentSectionDto> UpdateSectionAsync(int eventId, int sectionId, UpsertContentSectionRequest request, CancellationToken ct = default) =>
        PutAsync<ContentSectionDto>($"api/events/{eventId}/content/sections/{sectionId}", request, ct);
    public Task DeleteSectionAsync(int eventId, int sectionId, CancellationToken ct = default) =>
        DeleteAsync($"api/events/{eventId}/content/sections/{sectionId}", ct);
    public Task<List<ContentSectionDto>> ReorderSectionsAsync(int eventId, ReorderSectionsRequest request, CancellationToken ct = default) =>
        PutAsync<List<ContentSectionDto>>($"api/events/{eventId}/content/sections/order", request, ct);

    public Task<ScheduleItemDto> AddScheduleItemAsync(int eventId, UpsertScheduleItemRequest request, CancellationToken ct = default) =>
        PostAsync<ScheduleItemDto>($"api/events/{eventId}/content/schedule", request, ct);
    public Task<ScheduleItemDto> UpdateScheduleItemAsync(int eventId, int itemId, UpsertScheduleItemRequest request, CancellationToken ct = default) =>
        PutAsync<ScheduleItemDto>($"api/events/{eventId}/content/schedule/{itemId}", request, ct);
    public Task DeleteScheduleItemAsync(int eventId, int itemId, CancellationToken ct = default) =>
        DeleteAsync($"api/events/{eventId}/content/schedule/{itemId}", ct);

    public Task<List<EventMediaDto>> GetMediaAsync(int eventId, string? kind = null, CancellationToken ct = default) =>
        GetAsync<List<EventMediaDto>>(Query($"api/events/{eventId}/media", ("kind", kind)), ct);

    public async Task<EventMediaDto> UploadMediaAsync(int eventId, IFormFile file, string? caption, string kind = "Gallery", CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var filePart = new StreamContent(stream);
        filePart.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
        form.Add(filePart, "file", file.FileName);
        if (!string.IsNullOrWhiteSpace(caption)) form.Add(new StringContent(caption), "caption");
        form.Add(new StringContent(kind), "kind");
        return await PostMultipartAsync<EventMediaDto>($"api/events/{eventId}/media", form, ct);
    }

    public Task<EventMediaDto> UpdateMediaAsync(int eventId, int mediaId, UpdateMediaRequest request, CancellationToken ct = default) =>
        PutAsync<EventMediaDto>($"api/events/{eventId}/media/{mediaId}", request, ct);
    public Task DeleteMediaAsync(int eventId, int mediaId, CancellationToken ct = default) =>
        DeleteAsync($"api/events/{eventId}/media/{mediaId}", ct);
}
