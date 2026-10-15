using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Legal;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Legal;

public interface ILegalService
{
    Task<LegalDocumentDto> GetCurrentAsync(string docType, CancellationToken ct = default);
}

public class LegalService : ILegalService
{
    private readonly ILegalDocumentRepository _repository;
    public LegalService(ILegalDocumentRepository repository) => _repository = repository;

    public async Task<LegalDocumentDto> GetCurrentAsync(string docType, CancellationToken ct = default)
    {
        if (!Enum.TryParse<LegalDocType>(docType, ignoreCase: true, out var type))
            throw new NotFoundException($"Không có loại văn bản '{docType}'.");
        var doc = await _repository.GetCurrentAsync(type, ct) ?? throw new NotFoundException("Chưa có văn bản.");
        return new LegalDocumentDto
        {
            DocType = doc.DocType.ToString(),
            Version = doc.Version,
            Content = doc.Content,
            PublishedAt = doc.PublishedAt
        };
    }
}
