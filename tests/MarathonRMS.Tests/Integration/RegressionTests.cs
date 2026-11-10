using System.Net.Http.Headers;

namespace MarathonRMS.Tests.Integration;

/// <summary>Test hồi quy cho các bug tìm thấy khi kiểm thử toàn hệ thống (docs/test-report.md, 10/11/2026).</summary>
[Collection("api")]
public class RegressionTests
{
    private readonly ApiFactory _api;
    public RegressionTests(ApiFactory api) => _api = api;

    private async Task<int> EventIdAsync(string slug) =>
        Convert.ToInt32(await _api.SqlAsync($"SELECT EventId FROM Events WHERE Slug = '{slug}'"));

    /// <summary>BUG-07 – BR-32: Organizer không tự sửa thông tin đã khóa trên đăng ký của chính mình.</summary>
    [Fact]
    public async Task Organizer_cannot_edit_locked_info_of_own_registration_br32()
    {
        var hoGuom = await EventIdAsync("demo-ho-guom-night-run-2026");
        var orgId = (string)(await _api.SqlAsync("SELECT Id FROM AspNetUsers WHERE Email = 'organizer.hn@mrms.local'"))!;
        var regId = Convert.ToInt32(await _api.SqlAsync($"""
            DECLARE @r int = (SELECT TOP(1) RegistrationId FROM Registrations WHERE EventId = {hoGuom} AND Status = 'Confirmed'
                              AND RegisteredByUserId = (SELECT Id FROM AspNetUsers WHERE Email = 'member2@mrms.local') ORDER BY RegistrationId DESC);
            UPDATE Registrations SET RegisteredByUserId = '{orgId}' WHERE RegistrationId = @r;
            SELECT @r;
            """));
        var r = await (await _api.As("organizer.hn")).Put($"api/organizer/registrations/{regId}/locked-info", new
        {
            fullName = "Tu Sua Ten", dateOfBirth = "1985-05-30", gender = "M", idType = "CCCD", idNumber = "012345678901",
            reason = "Tu sua thong tin cua minh"
        });
        r.Expect(403);
        Assert.Contains("BR-32", r.Text);
    }

    /// <summary>BUG-08 – chú thích ảnh dài hơn cột (300) ⇒ 400 thay vì 500; BUG-14 – file rỗng báo đúng.</summary>
    [Fact]
    public async Task Media_upload_rejects_long_caption_and_empty_file_br51()
    {
        var hoGuom = await EventIdAsync("demo-ho-guom-night-run-2026");
        var org = await _api.As("organizer.hn");
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

        async Task<Resp> Upload(byte[] bytes, string caption)
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "anh.png");
            form.Add(new StringContent(caption), "caption");
            var res = await org.PostAsync($"api/events/{hoGuom}/media", form);
            return new Resp(res.StatusCode, await res.Content.ReadAsStringAsync(), null, res.Content.Headers.ContentType?.MediaType);
        }

        (await Upload(png, new string('x', 1000))).Expect(400);
        var empty = (await Upload(Array.Empty<byte>(), "anh rong")).Expect(400);
        Assert.Contains("rỗng", empty.Text);
    }

    /// <summary>BUG-09 – chuỗi rất dài trong $filter của OData công khai ⇒ 400 thay vì 500.</summary>
    [Theory]
    [InlineData("odata/Events?$filter=contains(Name,'{0}')")]
    [InlineData("odata/Participants?$filter=contains(DisplayName,'{0}')")]
    public async Task Very_long_odata_filter_is_400(string template) =>
        (await (await _api.As(null)).Get(string.Format(template, new string('a', 4500)))).Expect(400);

    /// <summary>BUG-15 – thông báo kiểm tra dữ liệu mặc định là tiếng Việt.</summary>
    [Fact]
    public async Task Default_validation_messages_are_vietnamese()
    {
        var r = (await (await _api.As(null)).Post("api/auth/register",
            new { fullName = "A", email = "x@test.local", password = "Demo@12345", acceptTerms = true, acceptPrivacy = true })).Expect(400);
        Assert.DoesNotContain("The field", r.Text);
        var p = (await (await _api.As("organizer.hn")).Post("api/events", new { })).Expect(400);
        Assert.DoesNotContain("field is required", p.Text);
        Assert.Contains("Vui lòng nhập", p.Text);
        // N1: [EmailAddress] / [Phone] có sẵn câu tiếng Anh của framework – cũng phải được dịch
        var e = (await (await _api.As(null)).Post("api/auth/register",
            new { fullName = "Nguyen Van A", email = "khong-phai-email", password = "Demo@12345", acceptTerms = true, acceptPrivacy = true })).Expect(400);
        Assert.DoesNotContain("not a valid", e.Text);
    }

    /// <summary>N-02: hồ sơ cá nhân dùng chung quy tắc số giấy tờ với đăng ký giải (BR-07).</summary>
    [Fact]
    public async Task Profile_rejects_malformed_cccd_br07()
    {
        var m = await _api.As(await _api.NewMemberAsync("prof"));
        (await m.Put("api/participants/me", new { fullName = "Nguyen Van A", idType = "CCCD", idNumber = "ABC" })).Expect(400);
        (await m.Put("api/participants/me", new { fullName = "Nguyen Van A", idType = "CCCD", idNumber = " 001095000123 " })).Expect(200);
    }

    /// <summary>N5: $filter quá dài bị chặn trước khi xuống DB.</summary>
    [Fact]
    public async Task Long_odata_option_is_rejected_before_database()
    {
        var r = (await (await _api.As(null)).Get($"odata/Events?$filter=contains(Name,'{new string('a', 1200)}')")).Expect(400);
        Assert.Contains("quá dài", r.Text);
    }
}
