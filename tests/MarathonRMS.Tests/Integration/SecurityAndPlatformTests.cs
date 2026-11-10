using System.Text.Json.Nodes;

namespace MarathonRMS.Tests.Integration;

/// <summary>Xác thực, phân quyền theo role và theo BTC (BR-30), OData, ProblemDetails, ràng buộc DB.</summary>
[Collection("api")]
public class SecurityAndPlatformTests
{
    private readonly ApiFactory _api;
    public SecurityAndPlatformTests(ApiFactory api) => _api = api;

    private async Task<int> EventIdBySlug(string slug)
    {
        var r = (await (await _api.As("organizer.hn")).Get("api/events/mine")).Expect(200);
        return r.Json!.AsArray().First(e => e!["slug"]!.GetValue<string>() == slug)!["id"]!.GetValue<int>();
    }

    // ---------- Xác thực ----------

    [Fact]
    public async Task Login_with_wrong_password_is_401_problem()
    {
        var r = await (await _api.As(null)).Post("api/auth/login", new { email = "member1@mrms.local", password = "wrong-pass-1" });
        r.Expect(401);
        Assert.Equal("application/problem+json", r.MediaType);
    }

    [Fact]
    public async Task Me_without_token_or_with_garbage_token_is_401()
    {
        (await (await _api.As(null)).Get("api/auth/me")).Expect(401);
        var c = _api.CreateClient();
        c.DefaultRequestHeaders.Authorization = new("Bearer", "abc.def.ghi");
        (await c.Get("api/auth/me")).Expect(401);
    }

    [Fact]
    public async Task Register_requires_terms_and_rejects_duplicate_email()
    {
        var anon = await _api.As(null);
        (await anon.Post("api/auth/register", new { fullName = "A B", email = "nt1@test.local", password = "Demo@12345", acceptTerms = false, acceptPrivacy = true }))
            .Expect(400);
        (await anon.Post("api/auth/register", new { fullName = "A B", email = "member1@mrms.local", password = "Demo@12345", acceptTerms = true, acceptPrivacy = true }))
            .Expect(409);
        (await anon.Post("api/auth/register", new { fullName = "A B", email = "short@test.local", password = "123", acceptTerms = true, acceptPrivacy = true }))
            .Expect(400);
    }

    [Fact]
    public async Task New_member_can_log_in_and_has_member_role()
    {
        var email = await _api.NewMemberAsync("newbie");
        var me = (await (await _api.As(email)).Get("api/auth/me")).Expect(200);
        Assert.Contains("Member", me.Text);
    }

    // ---------- Phân quyền theo role ----------

    [Theory]
    [InlineData(null, "api/admin/users", 401)]
    [InlineData("member1", "api/admin/users", 403)]
    [InlineData("organizer.hn", "api/admin/users", 403)]
    [InlineData("admin", "api/admin/users", 200)]
    [InlineData("admin", "api/superadmin/settings", 403)]
    [InlineData("superadmin", "api/superadmin/settings", 200)]
    [InlineData("organizer.hn", "api/superadmin/admins", 403)]
    [InlineData("staff.hn", "api/events/mine", 403)]
    [InlineData("member1", "api/events/mine", 403)]
    [InlineData("organizer.hn", "api/events/mine", 200)]
    [InlineData("member1", "odata/Registrations", 403)]
    [InlineData(null, "odata/Registrations", 401)]
    [InlineData("admin", "odata/AuditLogs", 403)]
    [InlineData("superadmin", "odata/AuditLogs?$top=5&$orderby=CreatedAt desc", 200)]
    [InlineData("member1", "api/admin/dashboard", 403)]
    [InlineData("admin", "api/admin/dashboard", 200)]
    [InlineData("member1", "api/race-day/events", 403)]
    [InlineData("staff.hn", "api/race-day/events", 200)]
    [InlineData(null, "api/legal/terms", 200)]
    [InlineData(null, "api/legal/privacy", 200)]
    [InlineData(null, "api/venues", 200)]
    [InlineData(null, "api/results/events", 200)]
    [InlineData(null, "odata/Events", 200)]
    [InlineData(null, "odata/Participants", 200)]
    [InlineData(null, "odata/Results", 200)]
    [InlineData("member1", "api/registrations/me", 200)]
    [InlineData("member1", "api/achievements/me", 200)]
    [InlineData("organizer.hn", "api/pricing/reference?discipline=Road&distanceKm=21.1", 200)]
    public async Task Role_matrix(string? user, string url, int expected) =>
        (await (await _api.As(user)).Get(url)).Expect(expected);

    // ---------- BR-30: Organizer chỉ thao tác giải của BTC mình ----------

    public static TheoryData<string, string> CrossOrgCalls => new()
    {
        { "GET", "api/events/{id}/dashboard" },
        { "GET", "api/events/{id}/bibs" },
        { "GET", "api/events/{id}/bibs/assignments" },
        { "POST", "api/events/{id}/bibs/assign" },
        { "GET", "api/events/{id}/results" },
        { "POST", "api/events/{id}/results/calculate" },
        { "POST", "api/events/{id}/results/provisional" },
        { "GET", "api/events/{id}/complaints" },
        { "GET", "api/events/{id}/refunds" },
        { "GET", "api/events/{id}/prizes" },
        { "GET", "api/events/{id}/content/manage" },
        { "PATCH", "api/events/{id}/status" },
        { "DELETE", "api/events/{id}" },
    };

    [Theory]
    [MemberData(nameof(CrossOrgCalls))]
    public async Task Organizer_of_other_org_is_blocked_br30(string method, string url)
    {
        var id = await EventIdBySlug("demo-cuc-phuong-trail-2026");
        var body = method == "PATCH" ? new { status = "Published" } : (object?)null;
        var r = await (await _api.As("organizer.sg")).Send(method, url.Replace("{id}", id.ToString()), body ?? (method == "GET" || method == "DELETE" ? null : new { }));
        r.ExpectAny(403, 404);
    }

    [Fact]
    public async Task Staff_of_other_org_cannot_search_race_kit_br30()
    {
        var id = await EventIdBySlug("demo-cuc-phuong-trail-2026");
        (await (await _api.As("staff.sg")).Post($"api/race-day/events/{id}/race-kit/search", new { keyword = "a" })).ExpectAny(403, 404);
    }

    [Fact]
    public async Task OData_registrations_of_other_org_are_not_visible_br30()
    {
        var id = await EventIdBySlug("demo-ho-guom-night-run-2026");
        var own = (await (await _api.As("organizer.hn")).Get($"odata/Registrations?$filter=EventId eq {id}&$count=true")).Expect(200);
        Assert.True(own.Json!["@odata.count"]!.GetValue<int>() > 0);
        var other = (await (await _api.As("organizer.sg")).Get($"odata/Registrations?$filter=EventId eq {id}&$count=true")).Expect(200);
        Assert.Equal(0, other.Json!["@odata.count"]!.GetValue<int>());
    }

    // ---------- OData ----------

    [Theory]
    [InlineData("odata/Events?$filter=Status eq 'RegistrationClosed'&$count=true")]
    [InlineData("odata/Events?$orderby=EventDate desc&$top=5")]
    [InlineData("odata/Events?$filter=contains(Name,'Trail')")]
    [InlineData("odata/Results?$top=10&$orderby=OverallRank")]
    [InlineData("odata/Participants?$count=true&$top=5")]
    public async Task OData_queries_work(string url)
    {
        var r = (await (await _api.As(null)).Get(url)).Expect(200);
        Assert.NotNull(r.Json!["value"]);
    }

    [Fact]
    public async Task OData_events_status_filter_returns_seeded_events()
    {
        var r = (await (await _api.As(null)).Get("odata/Events?$filter=Status eq 'RegistrationClosed'&$count=true")).Expect(200);
        Assert.True(r.Json!["@odata.count"]!.GetValue<int>() >= 2);
    }

    [Theory]
    [InlineData("odata/Events?$filter=NoSuchField eq 1")]
    [InlineData("odata/Events?$top=1000")]
    [InlineData("odata/Events?$filter=Name eq")]
    [InlineData("odata/Participants?$select=DateOfBirth")]
    [InlineData("odata/Participants?$select=IdNumber,Phone")]
    public async Task Bad_or_sensitive_OData_queries_are_400(string url) =>
        (await (await _api.As(null)).Get(url)).Expect(400);

    [Fact]
    public async Task Public_participant_list_has_no_personal_data_br37()
    {
        var r = (await (await _api.As(null)).Get("odata/Participants?$top=100")).Expect(200);
        foreach (var key in new[] { "DateOfBirth", "IdNumber", "Phone", "Email", "BloodType", "MedicalNotes", "UserId" })
            Assert.DoesNotContain($"\"{key}\"", r.Text);
    }

    [Fact]
    public async Task Public_results_have_no_personal_data_br37()
    {
        var r = (await (await _api.As(null)).Get("odata/Results?$top=100")).Expect(200);
        foreach (var key in new[] { "DateOfBirth", "IdNumber", "Phone", "BloodType", "MedicalNotes" })
            Assert.DoesNotContain($"\"{key}\"", r.Text);
    }

    // ---------- ProblemDetails ----------

    [Fact]
    public async Task Unknown_event_is_404_problem()
    {
        var r = (await (await _api.As(null)).Get("api/events/999999")).Expect(404);
        Assert.Equal("application/problem+json", r.MediaType);
    }

    [Fact]
    public async Task Invalid_event_body_is_400_problem()
    {
        var org = await _api.As("organizer.hn");
        (await org.Post("api/events", new { })).Expect(400);
        (await org.Send("POST", "api/events", "{ not json")).Expect(400);
        (await org.Send("POST", "api/events", "{\"name\": 123, \"venueId\": \"abc\"}")).Expect(400);
    }

    [Fact]
    public async Task Invalid_enum_in_query_is_400_not_500()
    {
        (await (await _api.As("organizer.hn")).Get("api/pricing/reference?discipline=Swim&distanceKm=10")).Expect(400);
        (await (await _api.As("organizer.hn")).Get("api/pricing/reference?discipline=Road&distanceKm=-1")).Expect(400);
    }

    // ---------- Ràng buộc DB (CHECK / UNIQUE) ----------

    [Theory]
    [InlineData("UPDATE TOP(1) PriceTiers SET Price = 1500")]                                          // CK_PriceTiers_Price (bội số 1.000)
    [InlineData("UPDATE TOP(1) PriceTiers SET Price = -1000")]
    [InlineData("UPDATE TOP(1) PriceTiers SET ValidTo = ValidFrom")]                                   // CK_PriceTiers_Range
    [InlineData("UPDATE TOP(1) RaceCategories SET BibTo = BibFrom")]                                   // CK_RaceCategories_Bib
    [InlineData("UPDATE TOP(1) Registrations SET Status = 'PendingPayment' WHERE BibNumber IS NOT NULL")]           // CK_Registrations_BibOnlyConfirmed
    [InlineData("UPDATE TOP(1) Registrations SET WaiverAccepted = 0")]                                 // CK_Registrations_Consents
    [InlineData("UPDATE TOP(1) Registrations SET IdType = NULL WHERE IdNumber IS NOT NULL")]
    [InlineData("UPDATE TOP(1) Events SET RegistrationCloseAt = DATEADD(day, -400, RegistrationOpenAt)")] // CK_Events_RegistrationWindow
    public async Task Check_constraints_reject_invalid_rows(string sql)
    {
        var ex = await Record.ExceptionAsync(() => _api.SqlAsync(sql));
        // UPDATE không khớp dòng nào thì không chứng minh được gì ⇒ phải có lỗi
        Assert.NotNull(ex);
        Assert.Contains("CHECK", ex!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duplicate_bib_in_same_event_is_rejected_by_unique_index_br19()
    {
        var sql = """
            DECLARE @e int, @a int, @b int, @bib int;
            SELECT TOP(1) @e = EventId FROM Registrations WHERE BibNumber IS NOT NULL GROUP BY EventId HAVING COUNT(*) > 1;
            SELECT TOP(1) @a = RegistrationId, @bib = BibNumber FROM Registrations WHERE EventId = @e AND BibNumber IS NOT NULL ORDER BY RegistrationId;
            SELECT TOP(1) @b = RegistrationId FROM Registrations WHERE EventId = @e AND BibNumber IS NOT NULL AND RegistrationId <> @a;
            UPDATE Registrations SET BibNumber = @bib WHERE RegistrationId = @b;
            """;
        var ex = await Record.ExceptionAsync(() => _api.SqlAsync(sql));
        Assert.NotNull(ex);
        Assert.Contains("duplicate", ex!.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- BR-50: lọc HTML ----------

    [Fact]
    public async Task Event_description_html_is_sanitized_br50()
    {
        var org = await _api.As("organizer.hn");
        var id = (await org.Post("api/events", new
        {
            name = "Giai thu XSS mo ta", venueId = 1, descriptionHtml = "<p>Hi</p><script>alert(1)</script><img src=x onerror=alert(2)>",
            eventDate = DateOnly.FromDateTime(VnTime.Now.AddDays(40)).ToString("yyyy-MM-dd"),
            endDate = DateOnly.FromDateTime(VnTime.Now.AddDays(40)).ToString("yyyy-MM-dd"),
            registrationOpenAt = VnTime.F(VnTime.Now.AddDays(1)), registrationCloseAt = VnTime.F(VnTime.Now.AddDays(20))
        })).Expect(201).Id;
        var d = (await org.Get($"api/events/{id}")).Expect(200);
        Assert.DoesNotContain("<script", d.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", d.Text, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- BR-35 ----------

    [Fact]
    public async Task Admin_cannot_manage_admins_and_last_superadmin_cannot_be_disabled_br35()
    {
        (await (await _api.As("admin")).Get("api/superadmin/admins")).Expect(403);
        var sa = await _api.As("superadmin");
        var me = (await sa.Get("api/auth/me")).Expect(200);
        var myId = me.Json!["id"]?.GetValue<string>() ?? me.Json!["userId"]!.GetValue<string>();
        (await sa.Patch($"api/superadmin/admins/{myId}/active", new { isActive = false })).ExpectAny(400, 409);
    }
}
