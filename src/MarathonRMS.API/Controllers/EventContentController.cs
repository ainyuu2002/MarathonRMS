using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.EventContent;
using MarathonRMS.Services.EventContent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

/// <summary>UC-88 trang giới thiệu giải (khối nội dung, lịch trình, màu chủ đạo) và UC-89 album ảnh (BR-50, BR-51).</summary>
[ApiController]
[Route("api/events/{eventId:int}")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class EventContentController : ControllerBase
{
    private readonly IEventContentService _content;
    public EventContentController(IEventContentService content) => _content = content;

    /// <summary>Nội dung công khai: khối + lịch trình + album đang hiển thị. Giải Draft trả 404 với người ngoài BTC.</summary>
    [HttpGet("content")]
    [AllowAnonymous]
    public async Task<ActionResult<EventContentDto>> GetPublic(int eventId, CancellationToken ct) =>
        Ok(await _content.GetPublicAsync(eventId, ct));

    [HttpGet("content/manage")]
    public async Task<ActionResult<EventContentDto>> GetForManage(int eventId, CancellationToken ct) =>
        Ok(await _content.GetForManageAsync(eventId, ct));

    [HttpPut("content/theme")]
    public async Task<ActionResult<EventContentDto>> UpdateTheme(int eventId, UpdateThemeRequest request, CancellationToken ct) =>
        Ok(await _content.UpdateThemeAsync(eventId, request, ct));

    // ---------- Khối nội dung ----------
    [HttpPost("content/sections")]
    public async Task<ActionResult<ContentSectionDto>> AddSection(int eventId, UpsertContentSectionRequest request, CancellationToken ct) =>
        Ok(await _content.AddSectionAsync(eventId, request, ct));

    [HttpPut("content/sections/{sectionId:int}")]
    public async Task<ActionResult<ContentSectionDto>> UpdateSection(int eventId, int sectionId, UpsertContentSectionRequest request, CancellationToken ct) =>
        Ok(await _content.UpdateSectionAsync(sectionId, request, ct));

    [HttpDelete("content/sections/{sectionId:int}")]
    public async Task<IActionResult> DeleteSection(int eventId, int sectionId, CancellationToken ct)
    {
        await _content.DeleteSectionAsync(sectionId, ct);
        return NoContent();
    }

    [HttpPut("content/sections/order")]
    public async Task<ActionResult<IReadOnlyList<ContentSectionDto>>> Reorder(int eventId, ReorderSectionsRequest request, CancellationToken ct) =>
        Ok(await _content.ReorderSectionsAsync(eventId, request, ct));

    // ---------- Lịch trình ----------
    [HttpPost("content/schedule")]
    public async Task<ActionResult<ScheduleItemDto>> AddScheduleItem(int eventId, UpsertScheduleItemRequest request, CancellationToken ct) =>
        Ok(await _content.AddScheduleItemAsync(eventId, request, ct));

    [HttpPut("content/schedule/{itemId:int}")]
    public async Task<ActionResult<ScheduleItemDto>> UpdateScheduleItem(int eventId, int itemId, UpsertScheduleItemRequest request, CancellationToken ct) =>
        Ok(await _content.UpdateScheduleItemAsync(itemId, request, ct));

    [HttpDelete("content/schedule/{itemId:int}")]
    public async Task<IActionResult> DeleteScheduleItem(int eventId, int itemId, CancellationToken ct)
    {
        await _content.DeleteScheduleItemAsync(itemId, ct);
        return NoContent();
    }

    // ---------- Ảnh ----------
    /// <summary>Ảnh của giải. Người ngoài BTC chỉ thấy ảnh đang hiển thị. kind = Gallery | Inline (bỏ trống = tất cả).</summary>
    [HttpGet("media")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<EventMediaDto>>> GetMedia(int eventId, [FromQuery] string? kind, CancellationToken ct) =>
        Ok(await _content.GetMediaAsync(eventId, kind, ct));

    /// <summary>Tải ảnh lên (multipart/form-data: file, caption, kind).</summary>
    [HttpPost("media")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<EventMediaDto>> Upload(int eventId, IFormFile file, [FromForm] string? caption, [FromForm] string? kind,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await _content.UploadMediaAsync(eventId, kind ?? "Gallery", stream, file.FileName, file.Length, caption, ct));
    }

    [HttpPut("media/{mediaId:int}")]
    public async Task<ActionResult<EventMediaDto>> UpdateMedia(int eventId, int mediaId, UpdateMediaRequest request, CancellationToken ct) =>
        Ok(await _content.UpdateMediaAsync(mediaId, request, ct));

    [HttpDelete("media/{mediaId:int}")]
    public async Task<IActionResult> DeleteMedia(int eventId, int mediaId, CancellationToken ct)
    {
        await _content.DeleteMediaAsync(mediaId, ct);
        return NoContent();
    }
}
