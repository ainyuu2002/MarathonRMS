namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>Model cho partial _RichTextEditor. FieldName khớp tên property được bind (VD "Input.Content").</summary>
public record RichTextEditorModel(string FieldName, string? Value, int MinHeightPx = 240)
{
    public string FieldId => FieldName.Replace('.', '_');
}
