namespace MarathonRMS.DTOs.Legal;

public class LegalDocumentDto
{
    public string DocType { get; set; } = "";
    public int Version { get; set; }
    public string Content { get; set; } = "";
    public DateTime PublishedAt { get; set; }
}
