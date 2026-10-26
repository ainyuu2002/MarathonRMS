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
        await ReadAsync<T>(await SendAsync(() => Http.GetAsync(url, ct)), ct);

    protected async Task<T> PostAsync<T>(string url, object body, CancellationToken ct = default) =>
        await ReadAsync<T>(await SendAsync(() => Http.PostAsJsonAsync(url, body, Json, ct)), ct);

    protected async Task PostAsync(string url, object body, CancellationToken ct = default) =>
        await EnsureSuccessAsync(await SendAsync(() => Http.PostAsJsonAsync(url, body, Json, ct)), ct);

    /// <summary>POST không có body, đọc kết quả.</summary>
    protected async Task<T> PostAsync<T>(string url, CancellationToken ct = default) =>
        await ReadAsync<T>(await SendAsync(() => Http.PostAsync(url, null, ct)), ct);

    protected async Task<T> PutAsync<T>(string url, object body, CancellationToken ct = default) =>
        await ReadAsync<T>(await SendAsync(() => Http.PutAsJsonAsync(url, body, Json, ct)), ct);

    protected async Task<T> PatchAsync<T>(string url, object body, CancellationToken ct = default) =>
        await ReadAsync<T>(await SendAsync(() => Http.PatchAsJsonAsync(url, body, Json, ct)), ct);

    protected async Task DeleteAsync(string url, CancellationToken ct = default) =>
        await EnsureSuccessAsync(await SendAsync(() => Http.DeleteAsync(url, ct)), ct);

    /// <summary>Ghép query string, bỏ tham số rỗng. VD Query("api/x", ("page", 2), ("q", null)).</summary>
    protected static string Query(string path, params (string Key, object? Value)[] parameters)
    {
        var parts = parameters.Where(p => p.Value is not null && p.Value.ToString() != "")
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!.ToString()!)}");
        var qs = string.Join("&", parts);
        return qs.Length == 0 ? path : $"{path}?{qs}";
    }

    /// <summary>Không kết nối được API (API chưa chạy, sai địa chỉ) ⇒ ApiException 503 thay vì trang lỗi 500.</summary>
    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try { return await send(); }
        catch (HttpRequestException)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, "Không kết nối được máy chủ. Vui lòng thử lại sau ít phút.");
        }
    }

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
