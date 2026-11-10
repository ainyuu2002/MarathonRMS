using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MarathonRMS.API.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace MarathonRMS.Tests.Integration;

/// <summary>
/// Chạy API thật (Development: tự migrate + nạp dữ liệu demo) trên DB LocalDB tạm <c>MarathonRMS_Test</c>.
/// DB bị xóa trước và sau khi chạy. Đổi server bằng biến môi trường <c>MRMS_TEST_SQL</c> (chuỗi kết nối tới master).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<AuthController>, IAsyncLifetime
{
    public const string DbName = "MarathonRMS_Test";
    public const string Password = "Demo@12345";

    private static readonly string SqlServer =
        Environment.GetEnvironmentVariable("MRMS_TEST_SQL") ?? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    public static string ConnectionString => $"{SqlServer};Database={DbName};MultipleActiveResultSets=True";

    private readonly Dictionary<string, string> _tokens = new();
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public ApiFactory()
    {
        // Program.cs đọc cấu hình ngay lúc CreateBuilder ⇒ đặt qua biến môi trường để chắc chắn được áp dụng
        Environment.SetEnvironmentVariable("ConnectionStrings__MarathonDb", ConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-test-signing-key-0123456789-abcdef");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(Path.Combine(RepoRoot(), "src", "MarathonRMS.API"));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc repo (global.json).");
    }

    public async Task InitializeAsync()
    {
        await DropDatabaseAsync();
        CreateClient(); // khởi động server (migrate + seed) ngay để lỗi khởi động lộ sớm
        await Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        SqlConnection.ClearAllPools();
        await DropDatabaseAsync();
    }

    private static async Task DropDatabaseAsync()
    {
        await using var conn = new SqlConnection($"{SqlServer};Database=master");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{DbName}') IS NOT NULL BEGIN ALTER DATABASE [{DbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DbName}]; END";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Chạy SQL trực tiếp trên DB test (kiểm tra ràng buộc DB, dựng tình huống).</summary>
    public async Task<object?> SqlAsync(string sql)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET QUOTED_IDENTIFIER ON; " + sql;
        return await cmd.ExecuteScalarAsync();
    }

    public async Task<HttpClient> As(string? user)
    {
        var client = CreateClient();
        if (user is null) return client;
        var email = user.Contains('@') ? user : $"{user}@mrms.local";
        await _tokenLock.WaitAsync();
        try
        {
            if (!_tokens.TryGetValue(email, out var token))
            {
                var res = await CreateClient().PostAsJsonAsync("api/auth/login", new { email, password = Password });
                res.EnsureSuccessStatusCode();
                token = (await res.Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
                _tokens[email] = token;
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        finally { _tokenLock.Release(); }
        return client;
    }

    /// <summary>Tạo Member mới (đăng ký tài khoản) và trả email.</summary>
    public async Task<string> NewMemberAsync(string prefix)
    {
        var email = $"{prefix}.{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 40)] + "@test.local";
        var res = await CreateClient().PostAsJsonAsync("api/auth/register",
            new { fullName = "Thanh Vien Thu", email, password = Password, acceptTerms = true, acceptPrivacy = true });
        Assert.True(res.IsSuccessStatusCode, $"register {email}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return email;
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory> { }

/// <summary>Gọi API và đọc JSON gọn.</summary>
public static class Http
{
    public static async Task<Resp> Send(this HttpClient c, string method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is string raw) req.Content = new StringContent(raw, Encoding.UTF8, "application/json");
        else if (body is not null) req.Content = JsonContent.Create(body);
        var res = await c.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        JsonNode? json = null;
        try { json = text.Length > 0 ? JsonNode.Parse(text) : null; } catch (JsonException) { }
        return new Resp(res.StatusCode, text, json, res.Content.Headers.ContentType?.MediaType);
    }

    public static Task<Resp> Get(this HttpClient c, string url) => c.Send("GET", url);
    public static Task<Resp> Post(this HttpClient c, string url, object? body = null) => c.Send("POST", url, body ?? new { });
    public static Task<Resp> Put(this HttpClient c, string url, object? body) => c.Send("PUT", url, body);
    public static Task<Resp> Patch(this HttpClient c, string url, object? body) => c.Send("PATCH", url, body);
    public static Task<Resp> Delete(this HttpClient c, string url) => c.Send("DELETE", url);
}

public sealed record Resp(HttpStatusCode Status, string Text, JsonNode? Json, string? MediaType)
{
    public int Code => (int)Status;

    public Resp Expect(int code)
    {
        Assert.True(Code == code, $"Mong đợi {code}, nhận {Code}: {Text[..Math.Min(Text.Length, 600)]}");
        return this;
    }

    public Resp ExpectAny(params int[] codes)
    {
        Assert.True(codes.Contains(Code), $"Mong đợi {string.Join("/", codes)}, nhận {Code}: {Text[..Math.Min(Text.Length, 600)]}");
        return this;
    }

    public int Id => Json!["id"]!.GetValue<int>();
}

/// <summary>Giờ VN – hệ thống lưu thời gian theo giờ VN (IDateTimeProvider).</summary>
public static class VnTime
{
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public static string F(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm:ss");
}
