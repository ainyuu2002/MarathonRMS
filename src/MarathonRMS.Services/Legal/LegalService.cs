using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Legal;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Legal;

/// <summary>UC-84 – điều khoản sử dụng / chính sách quyền riêng tư theo phiên bản. Chỉ thêm phiên bản mới, không sửa bản cũ.</summary>
public interface ILegalService
{
    Task<LegalDocumentDto> GetCurrentAsync(string docType, CancellationToken ct = default);
    Task<IReadOnlyList<LegalDocumentDto>> GetVersionsAsync(string docType, CancellationToken ct = default);
    Task<LegalDocumentDto> PublishAsync(PublishLegalDocumentRequest request, CancellationToken ct = default);
}

public class LegalService : ILegalService
{
    private readonly ILegalDocumentRepository _repository;
    private readonly IHtmlContentSanitizer _sanitizer;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public LegalService(ILegalDocumentRepository repository, IHtmlContentSanitizer sanitizer, ICurrentUserService currentUser,
        IAuditLogger audit, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _repository = repository;
        _sanitizer = sanitizer;
        _currentUser = currentUser;
        _audit = audit;
        _clock = clock;
        _uow = uow;
    }

    public async Task<LegalDocumentDto> GetCurrentAsync(string docType, CancellationToken ct = default)
    {
        var doc = await _repository.GetCurrentAsync(ParseType(docType), ct) ?? throw new NotFoundException("Chưa có văn bản.");
        return ToDto(doc);
    }

    public async Task<IReadOnlyList<LegalDocumentDto>> GetVersionsAsync(string docType, CancellationToken ct = default) =>
        (await _repository.GetVersionsAsync(ParseType(docType), ct)).Select(ToDto).ToList();

    /// <summary>Nội dung được lọc HTML (BR-50). Người dùng đã đồng ý phiên bản cũ không phải đồng ý lại (BR-46d).</summary>
    public async Task<LegalDocumentDto> PublishAsync(PublishLegalDocumentRequest request, CancellationToken ct = default)
    {
        var type = ParseType(request.DocType);
        var content = _sanitizer.Sanitize(request.Content);
        if (string.IsNullOrWhiteSpace(content))
            throw new BusinessRuleException("BR-50", "Nội dung sau khi lọc HTML bị trống.", 400);

        var current = await _repository.GetCurrentAsync(type, ct);
        var doc = new LegalDocument
        {
            DocType = type,
            Version = (current?.Version ?? 0) + 1,
            Content = content,
            PublishedAt = _clock.Now,
            PublishedById = _currentUser.UserId
        };
        await _repository.AddAsync(doc, ct);
        await _audit.LogAsync("Legal.Publish", nameof(LegalDocument), $"{type}:v{doc.Version}", null, new { doc.Version }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(doc);
    }

    private static LegalDocType ParseType(string docType) =>
        Enum.TryParse<LegalDocType>(docType, ignoreCase: true, out var t) ? t : throw new NotFoundException($"Không có loại văn bản '{docType}'.");

    private static LegalDocumentDto ToDto(LegalDocument d) => new()
    {
        DocType = d.DocType.ToString(), Version = d.Version, Content = d.Content, PublishedAt = d.PublishedAt
    };
}
