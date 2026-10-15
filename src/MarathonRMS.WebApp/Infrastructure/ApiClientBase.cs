using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>Lớp cơ sở cho các typed HttpClient gọi API: gửi JSON, đọc ProblemDetails thành ApiException.</summary>
public abstract class ApiClientBase
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    protected readonly HttpClient Http;

    protected ApiClientBase(HttpClient http) => Http = http;

    protected async Task<T> GetAsync<T>(string url, CancellationToken ct = default) =>
        await ReadAsync<T>(await Http.GetAsync(url, ct), ct);

    protected async Task<T> PostAsync<T>(string url, object body, CancellationToken ct = default) =>
        await ReadAsync<T>(await Http.PostAsJsonAsync(url, body, Json, ct), ct);

    protected async Task PostAsync(string url, object body, CancellationToken ct = default) =>
        await EnsureSuccessAsync(await Http.PostAsJsonAsync(url, body, Json, ct), ct);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        string message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
            HttpStatusCode.NotFound => "Không tìm thấy dữ liệu.",
            _ => "Đã có lỗi xảy ra. Vui lòng thử lại."
        };
        string? code = null;
        var errors = new Dictionary<string, string[]>();

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } t) message = t;
            if (root.TryGetProperty("code", out var c)) code = c.GetString();
            if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Object)
                foreach (var p in errs.EnumerateObject())
                    errors[p.Name] = p.Value.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        }
        catch (JsonException) { /* body không phải ProblemDetails */ }

        throw new ApiException(response.StatusCode, message, code, errors);
    }
}
