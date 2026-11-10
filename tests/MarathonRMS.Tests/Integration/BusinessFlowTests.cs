using System.Text.Json.Nodes;

namespace MarathonRMS.Tests.Integration;

/// <summary>
/// Luồng nghiệp vụ qua HTTP: thiết lập giải → mở đăng ký → đăng ký, coupon, thanh toán Mock → BIB → hủy giải + hoàn tiền;
/// ngày thi → tính kết quả → tạm thời → khiếu nại → chính thức → giải thưởng (trên giải demo Cúc Phương).
/// </summary>
[Collection("api")]
public class BusinessFlowTests
{
    private const string Org = "organizer.hn";
    private readonly ApiFactory _api;
    public BusinessFlowTests(ApiFactory api) => _api = api;

    private sealed record OpenEvent(int EventId, int PaidCategory, int FreeCategory);

    /// <summary>UC-18…24 + chuyển trạng thái (BR-38): giải đường phố, cự ly có phí sức chứa 2 và cự ly miễn phí.</summary>
    private async Task<OpenEvent> CreateOpenEventAsync(string name, int capacity = 2)
    {
        var org = await _api.As(Org);
        var now = VnTime.Now.AddSeconds(-VnTime.Now.Second);
        var openAt = now.AddHours(-1);
        var closeAt = now.AddDays(20);
        var race = DateOnly.FromDateTime(now.AddDays(30));
        var start = race.ToDateTime(new TimeOnly(5, 0));

        var ev = (await org.Post("api/events", new
        {
            name, venueId = 1, eventDate = race.ToString("yyyy-MM-dd"), endDate = race.ToString("yyyy-MM-dd"),
            registrationOpenAt = VnTime.F(openAt), registrationCloseAt = VnTime.F(closeAt)
        })).Expect(201).Id;

        async Task<int> Category(string cname, decimal km, int cap, int bib, int price, bool timed)
        {
            var c = (await org.Post($"api/events/{ev}/categories", new
            {
                name = cname, discipline = "Road", categoryType = "Standard", distanceKm = km, capacity = cap,
                startAt = VnTime.F(start), cutOffMinutes = timed ? 120 : (int?)null, minAge = 5, isTimed = timed, isRanked = timed,
                bibFrom = bib, bibTo = bib + 999
            })).Expect(201).Id;
            (await org.Post($"api/categories/{c}/price-tiers", new { name = "Gia", price, validFrom = VnTime.F(openAt), validTo = VnTime.F(closeAt) }))
                .Expect(201);
            if (timed)
            {
                (await org.Post($"api/categories/{c}/checkpoints", new { name = "Start", distanceKm = 0, sequence = 1, type = "Start", stationType = "None" })).Expect(201);
                (await org.Post($"api/categories/{c}/checkpoints", new { name = "Finish", distanceKm = km, sequence = 100, type = "Finish", stationType = "None" })).Expect(201);
            }
            return c;
        }

        var paid = await Category("10K", 10, capacity, 1000, 300_000, true);
        var free = await Category("Fun run", 3, 50, 5000, 0, false);

        (await org.Put($"api/events/{ev}/policies", new
        {
            ageCalculationMode = "RaceDate", guardianConsentUnderAge = 18, maxDependentsPerAccount = 5, transferPolicy = "Free",
            maxTransfers = 1, maxCategoryChanges = 1, lockSpecialEntries = true, overallRankingBasis = "GunTime",
            ageGroupRankingBasis = "ChipTime", tieBreakRule = "Shared", complaintWindowMinutes = 1440, awardStackingPolicy = "HighestOnly",
            forceMajeurePolicy = "Refund", infoEditDeadline = VnTime.F(now.AddDays(10)),
            transferDeadline = VnTime.F(now.AddDays(15)), categoryChangeDeadline = VnTime.F(now.AddDays(15))
        })).Expect(200);
        (await org.Put($"api/events/{ev}/waiver", new { waiverText = "Toi cam ket du suc khoe." })).Expect(200);
        (await org.Patch($"api/events/{ev}/status", new { status = "Published" })).Expect(204);
        (await org.Patch($"api/events/{ev}/status", new { status = "RegistrationOpen" })).Expect(204);
        return new OpenEvent(ev, paid, free);
    }

    private static object Body(int participantId, int categoryId, string idNumber, string? coupon = null, string dob = "1995-05-05") => new
    {
        categoryId, participantId, fullName = "Nguyen Van Thu", bibName = "VAN THU", dateOfBirth = dob, gender = "M",
        nationality = "Viet Nam", idType = "CCCD", idNumber, phone = "0901234567", tShirtSize = "M",
        emergencyContactName = "Me", emergencyContactPhone = "0907654321", waiverAccepted = true, healthDeclarationAccepted = true,
        couponCode = coupon
    };

    private async Task<(HttpClient Client, int ParticipantId)> MemberAsync(string prefix)
    {
        var c = await _api.As(await _api.NewMemberAsync(prefix));
        var p = (await c.Get("api/participants/me")).Expect(200);
        return (c, p.Json!["participantId"]!.GetValue<int>());
    }

    private static string NewIdNumber() => "0010" + Random.Shared.NextInt64(10_000_000, 99_999_999);

    [Fact]
    public async Task Registration_payment_bib_and_cancellation_flow()
    {
        var ev = await CreateOpenEventAsync("Giai thu tich hop " + Guid.NewGuid().ToString("N")[..6]);
        var org = await _api.As(Org);
        (await org.Post("api/coupons", new
        {
            code = "IT" + Random.Shared.Next(100000, 999999), discountType = "Percent", discountValue = 50, maxUses = 5, eventId = ev.EventId,
            validFrom = VnTime.F(VnTime.Now.AddHours(-1)), validTo = VnTime.F(VnTime.Now.AddDays(5))
        })).Expect(201);
        var coupon = (await org.Get("api/coupons?pageSize=100")).Json!["items"]!.AsArray()
            .First(c => c!["eventId"]?.GetValue<int?>() == ev.EventId)!["code"]!.GetValue<string>();

        var (m1, p1) = await MemberAsync("m1");
        var (m2, p2) = await MemberAsync("m2");
        var (m3, p3) = await MemberAsync("m3");
        var (m4, p4) = await MemberAsync("m4");
        var id1 = NewIdNumber();

        // UC-39: BR-09 đồng ý bắt buộc, BR-05 tuổi, coupon BR-11
        (await m1.Post("api/registrations", new
        {
            categoryId = ev.PaidCategory, participantId = p1, fullName = "Nguyen Van Thu", bibName = "VAN THU", dateOfBirth = "1995-05-05",
            gender = "M", nationality = "Viet Nam", idType = "CCCD", idNumber = id1, phone = "0901234567", tShirtSize = "M",
            emergencyContactName = "Me", emergencyContactPhone = "0907654321", waiverAccepted = false, healthDeclarationAccepted = true
        })).Expect(400);
        (await m1.Post("api/registrations", Body(p2, ev.PaidCategory, id1))).ExpectAny(403, 404);          // hồ sơ người khác
        (await m1.Post("api/registrations", Body(p1, ev.PaidCategory, id1, coupon: "KHONGCO"))).ExpectAny(400, 404, 409);
        // BR-07: số giấy tờ phải đúng định dạng (BUG-11, BUG-12)
        (await m1.Post("api/registrations", Body(p1, ev.PaidCategory, "abc"))).Expect(400);
        (await m1.Post("api/registrations", Body(p1, ev.PaidCategory, "12345😀"))).Expect(400);
        var r1 = (await m1.Post("api/registrations", Body(p1, ev.PaidCategory, id1, coupon))).Expect(201);
        Assert.Equal("PendingPayment", r1.Json!["status"]!.GetValue<string>());
        Assert.Equal(150_000m, r1.Json!["finalPrice"]!.GetValue<decimal>());
        var reg1 = r1.Id;

        // BR-01: một người một đăng ký còn hiệu lực / giải – theo hồ sơ và theo số giấy tờ
        (await m1.Post("api/registrations", Body(p1, ev.FreeCategory, NewIdNumber()))).Expect(409);
        (await m2.Post("api/registrations", Body(p2, ev.PaidCategory, id1))).Expect(409);

        // BR-04: sức chứa 2
        var reg2 = (await m2.Post("api/registrations", Body(p2, ev.PaidCategory, NewIdNumber()))).Expect(201).Id;
        (await m3.Post("api/registrations", Body(p3, ev.PaidCategory, NewIdNumber()))).Expect(409);

        // UC-41: thanh toán Mock – người khác không trả hộ được, trả lần 2 bị chặn
        (await m2.Post("api/payments", new { registrationId = reg1, purpose = "Registration", provider = "Mock" })).ExpectAny(403, 404);
        var pay = (await m1.Post("api/payments", new { registrationId = reg1, purpose = "Registration", provider = "Mock" })).ExpectAny(200, 201);
        Assert.Equal("Confirmed", pay.Json!["registrationStatus"]!.GetValue<string>());
        Assert.Equal(150_000m, pay.Json!["amount"]!.GetValue<decimal>());
        (await m1.Post("api/payments", new { registrationId = reg1, purpose = "Registration", provider = "Mock" })).ExpectAny(400, 409);

        // BR-14: đã thanh toán thì không tự hủy; chưa thanh toán thì hủy được và trả suất
        (await m1.Post($"api/registrations/{reg1}/cancel")).Expect(409);
        (await m2.Post($"api/registrations/{reg2}/cancel")).Expect(204);
        var reg3 = (await m3.Post("api/registrations", Body(p3, ev.PaidCategory, NewIdNumber()))).Expect(201).Id;

        // BR-12: giá 0 ⇒ Confirmed ngay
        var free = (await m4.Post("api/registrations", Body(p4, ev.FreeCategory, NewIdNumber()))).Expect(201);
        Assert.Equal("Confirmed", free.Json!["status"]!.GetValue<string>());

        // IDOR: người khác không sửa / hủy được đăng ký
        var info = new { bibName = "HACK", tShirtSize = "L", phone = "0901111111", emergencyContactName = "Bo", emergencyContactPhone = "0902222222" };
        (await m2.Put($"api/registrations/{reg1}/info", info)).ExpectAny(403, 404);
        (await m2.Post($"api/registrations/{reg3}/cancel")).ExpectAny(403, 404);
        (await m1.Put($"api/registrations/{reg1}/info", info)).Expect(200);

        // UC-52: gán BIB – chỉ đăng ký Confirmed, trong dải của cự ly (BR-19)
        (await org.Post($"api/events/{ev.EventId}/bibs/assign", new { })).Expect(200);
        var assigned = (await org.Get($"api/events/{ev.EventId}/bibs/assignments?pageSize=100")).Expect(200);
        var items = assigned.Json!["items"]!.AsArray();
        var bib1 = items.First(i => i!["registrationId"]!.GetValue<int>() == reg1)!["bibNumber"]!.GetValue<int?>();
        Assert.InRange(bib1!.Value, 1000, 1999);
        Assert.DoesNotContain(items, i => i!["registrationId"]!.GetValue<int>() == reg3 && i["bibNumber"]?.GetValue<int?>() is not null);
        var mine = (await m1.Get("api/registrations/me")).Expect(200).Json!.AsArray();
        Assert.Equal(bib1, mine.First(r => r!["id"]!.GetValue<int>() == reg1)!["bibNumber"]!.GetValue<int?>());

        // UC-48 / UC-69 / UC-38
        var odata = (await org.Get($"odata/Registrations?$filter=EventId eq {ev.EventId}&$count=true")).Expect(200);
        Assert.Equal(4, odata.Json!["@odata.count"]!.GetValue<int>());
        (await org.Get($"api/events/{ev.EventId}/dashboard")).Expect(200);
        var pub = (await (await _api.As(null)).Get($"odata/Participants?$filter=EventId eq {ev.EventId}&$count=true")).Expect(200);
        Assert.Equal(2, pub.Json!["@odata.count"]!.GetValue<int>()); // chỉ Confirmed
        Assert.DoesNotContain(id1, pub.Text);

        // UC-25 / UC-50: hủy giải ⇒ đơn chờ thanh toán bị hủy, đơn đã thanh toán ⇒ yêu cầu hoàn tiền = FinalPrice
        (await org.Post($"api/events/{ev.EventId}/cancel", new { reason = "", refundPaidRegistrations = true })).Expect(400);
        var cancel = (await org.Post($"api/events/{ev.EventId}/cancel", new { reason = "Thoi tiet xau", refundPaidRegistrations = true })).Expect(200);
        Assert.Equal("Cancelled", cancel.Json!["status"]!.GetValue<string>());
        Assert.Equal(1, cancel.Json!["cancelledUnpaid"]!.GetValue<int>());
        Assert.Equal(1, cancel.Json!["refundsCreated"]!.GetValue<int>());
        Assert.Equal(150_000m, cancel.Json!["refundTotal"]!.GetValue<decimal>());
        var refunds = (await org.Get($"api/events/{ev.EventId}/refunds")).Expect(200).Json!.AsArray();
        var refundId = refunds.Single()!["id"]!.GetValue<int>();
        (await (await _api.As("organizer.sg")).Post($"api/refunds/{refundId}/complete", new { transactionRef = "FT999999" })).ExpectAny(403, 404);
        (await org.Post($"api/refunds/{refundId}/complete", new { transactionRef = "" })).Expect(400);
        (await org.Post($"api/refunds/{refundId}/complete", new { transactionRef = "FT26111012345" })).ExpectAny(200, 204);
        var myRefunds = (await m1.Get("api/refunds/me")).Expect(200).Json!.AsArray();
        Assert.Contains(myRefunds, r => r!["status"]!.GetValue<string>() == "Completed");

        // Giải đã hủy: không đăng ký thêm
        var (m5, p5) = await MemberAsync("m5");
        (await m5.Post("api/registrations", Body(p5, ev.FreeCategory, NewIdNumber()))).ExpectAny(400, 409);
    }

    [Fact]
    public async Task Category_change_and_transfer_keep_bib_consistent_br17_br16()
    {
        var ev = await CreateOpenEventAsync("Giai thu chuyen nhuong " + Guid.NewGuid().ToString("N")[..6]);
        var org = await _api.As(Org);
        var (m1, p1) = await MemberAsync("tr1");
        var email2 = await _api.NewMemberAsync("tr2");
        var m2 = await _api.As(email2);

        var reg = (await m1.Post("api/registrations", Body(p1, ev.PaidCategory, NewIdNumber()))).Expect(201).Id;
        (await m1.Post("api/payments", new { registrationId = reg, purpose = "Registration", provider = "Mock" })).ExpectAny(200, 201);
        (await org.Post($"api/events/{ev.EventId}/bibs/assign", new { })).Expect(200);

        // UC-46: đổi xuống cự ly miễn phí – không hoàn, thu hồi BIB cũ và gán lại trong dải mới (BR-17)
        var options = (await m1.Get($"api/registrations/{reg}/change-options")).Expect(200);
        var quote = options.Json!["categoryOptions"]!.AsArray().First(o => o!["categoryId"]!.GetValue<int>() == ev.FreeCategory)!;
        Assert.Equal(0m, quote["total"]!.GetValue<decimal>());
        (await m1.Post($"api/registrations/{reg}/category-change", new { newCategoryId = ev.FreeCategory, expectedTotal = 99_000 })).ExpectAny(400, 409);
        var changed = (await m1.Post($"api/registrations/{reg}/category-change", new { newCategoryId = ev.FreeCategory, expectedTotal = 0 }))
            .ExpectAny(200, 201);
        Assert.InRange(changed.Json!["newBibNumber"]!.GetValue<int>(), 5000, 5999);
        // Đổi lại đúng cự ly đang ở ⇒ 409 (BUG-16), không phải 400 "cự ly không hợp lệ"
        (await m1.Post($"api/registrations/{reg}/category-change", new { newCategoryId = ev.FreeCategory, expectedTotal = 0 })).Expect(409);

        // UC-47: chuyển nhượng suất đã có BIB – người nhận giữ cự ly và BIB, đăng ký cũ ⇒ Transferred
        (await m1.Post($"api/registrations/{reg}/transfers", new { recipientEmail = "khongton.tai@test.local" })).ExpectAny(400, 404, 409);
        var transfer = (await m1.Post($"api/registrations/{reg}/transfers", new { recipientEmail = email2 })).ExpectAny(200, 201).Id;
        (await (await _api.As("member3")).Post($"api/transfers/{transfer}/accept", new { })).ExpectAny(400, 403, 404);
        var p2 = (await m2.Get("api/participants/me")).Expect(200).Json!["participantId"]!.GetValue<int>();
        var accepted = (await m2.Post($"api/transfers/{transfer}/accept", new
        {
            fullName = "Tran Van Nhan", bibName = "NHAN", dateOfBirth = "1993-03-03", gender = "M", nationality = "Viet Nam",
            idType = "CCCD", idNumber = NewIdNumber(), phone = "0912345678", tShirtSize = "L", emergencyContactName = "Vo",
            emergencyContactPhone = "0987654321", waiverAccepted = true, healthDeclarationAccepted = true
        })).ExpectAny(200, 201);
        var newReg = accepted.Json!["newRegistrationId"]!.GetValue<int>();
        var mineNew = (await m2.Get("api/registrations/me")).Expect(200).Json!.AsArray().First(r => r!["id"]!.GetValue<int>() == newReg)!;
        Assert.Equal(changed.Json!["newBibNumber"]!.GetValue<int>(), mineNew["bibNumber"]!.GetValue<int>());
        Assert.Equal(ev.FreeCategory, mineNew["categoryId"]!.GetValue<int>());
        var oldStatus = await _api.SqlAsync($"SELECT Status FROM Registrations WHERE RegistrationId = {reg}");
        Assert.Equal("Transferred", oldStatus);
        _ = p2;
    }

    [Fact]
    public async Task Admin_can_run_medical_purge_and_dashboards_uc91_uc71()
    {
        (await (await _api.As("admin")).Post("api/admin/privacy/medical-purge")).Expect(200);
        (await (await _api.As("organizer.hn")).Post("api/admin/privacy/medical-purge")).Expect(403);
        var dash = (await (await _api.As("admin")).Get("api/admin/dashboard")).Expect(200);
        Assert.NotNull(dash.Json);
    }

    [Fact]
    public async Task Last_slot_parallel_registrations_only_one_wins_br04()
    {
        var ev = await CreateOpenEventAsync("Giai thu song song " + Guid.NewGuid().ToString("N")[..6], capacity: 1);
        var members = new List<(HttpClient, int)>();
        for (var i = 0; i < 6; i++) members.Add(await MemberAsync("par" + i));
        var results = await Task.WhenAll(members.Select(m => m.Item1.Post("api/registrations", Body(m.Item2, ev.PaidCategory, NewIdNumber()))));
        Assert.Equal(1, results.Count(r => r.Code == 201));
        Assert.All(results, r => Assert.True(r.Code is 201 or 409, $"{r.Code}: {r.Text}"));
    }

    [Fact]
    public async Task Parallel_double_payment_creates_one_success()
    {
        var ev = await CreateOpenEventAsync("Giai thu thanh toan " + Guid.NewGuid().ToString("N")[..6]);
        var (m, p) = await MemberAsync("pay");
        var reg = (await m.Post("api/registrations", Body(p, ev.PaidCategory, NewIdNumber()))).Expect(201).Id;
        var res = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            m.Post("api/payments", new { registrationId = reg, purpose = "Registration", provider = "Mock" })));
        Assert.Equal(1, res.Count(r => r.Code is 200 or 201));
        Assert.All(res, r => Assert.True(r.Code is 200 or 201 or 409 or 400, $"{r.Code}: {r.Text}"));
        var count = await _api.SqlAsync($"SELECT COUNT(*) FROM Payments WHERE RegistrationId = {reg} AND Status = 'Success'");
        Assert.Equal(1, Convert.ToInt32(count));
    }

    [Fact]
    public async Task Postponed_event_can_still_move_to_next_status_uc25()
    {
        var ev = await CreateOpenEventAsync("Giai thu hoan " + Guid.NewGuid().ToString("N")[..6]);
        var org = await _api.As(Org);
        var detail = (await org.Get($"api/events/{ev.EventId}")).Expect(200);
        var oldDate = DateOnly.Parse(detail.Json!["eventDate"]!.GetValue<string>());
        (await org.Post($"api/events/{ev.EventId}/postpone", new { newEventDate = oldDate.AddDays(-1).ToString("yyyy-MM-dd"), reason = "Hoan vi mua bao lon" })).ExpectAny(400, 409);
        var r = (await org.Post($"api/events/{ev.EventId}/postpone", new { newEventDate = oldDate.AddDays(14).ToString("yyyy-MM-dd"), reason = "Hoan vi mua bao lon" }))
            .Expect(200);
        Assert.Equal(14, r.Json!["shiftedDays"]!.GetValue<int>());
        detail = (await org.Get($"api/events/{ev.EventId}")).Expect(200);
        Assert.Equal(oldDate.ToString("yyyy-MM-dd"), detail.Json!["postponedFromDate"]!.GetValue<string>());
        Assert.Equal("RegistrationOpen", detail.Json!["status"]!.GetValue<string>());
        (await org.Patch($"api/events/{ev.EventId}/status", new { status = "RegistrationClosed" })).Expect(204);
    }

    [Fact]
    public async Task Race_day_results_complaint_official_and_prizes()
    {
        var org = await _api.As(Org);
        var cuc = (await org.Get("api/events/mine")).Expect(200).Json!.AsArray()
            .First(e => e!["slug"]!.GetValue<string>() == "demo-cuc-phuong-trail-2026")!["id"]!.GetValue<int>();

        // UC-59: tính kết quả; người ngoài BTC không làm được
        (await (await _api.As("organizer.sg")).Post($"api/events/{cuc}/results/calculate")).ExpectAny(403, 404);
        (await org.Post($"api/events/{cuc}/results/calculate")).Expect(200);
        var rows = (await org.Get($"api/events/{cuc}/results/rows?pageSize=100")).Expect(200).Json!["items"]!.AsArray();
        Assert.NotEmpty(rows);
        Assert.Contains(rows, r => r!["status"]!.GetValue<string>() == "Finished");
        Assert.Contains(rows, r => r!["status"]!.GetValue<string>() == "DNF");
        Assert.Contains(rows, r => r!["status"]!.GetValue<string>() == "DNS");

        // UC-60: công bố tạm thời ⇒ công khai
        (await org.Post($"api/events/{cuc}/results/provisional")).Expect(200);

        // BUG-04: bổ sung giờ CP2 bị thiếu (VĐV BIB 2007 đang DNF MissedCheckpoint) khi kết quả Tạm thời ⇒ tự tính lại thành Finished
        var bib2007 = rows.First(r => r!["bibNumber"]?.GetValue<int?>() == 2007)!;
        Assert.Equal("DNF", bib2007["status"]!.GetValue<string>());
        var cpInfo = (string)(await _api.SqlAsync($"""
            SELECT TOP(1) CAST(cp.CheckpointId AS varchar(10)) + '|' + CONVERT(varchar(30), DATEADD(minute, 100, c.StartAt), 126)
            FROM Registrations r JOIN RaceCategories c ON c.CategoryId = r.CategoryId JOIN Checkpoints cp ON cp.CategoryId = c.CategoryId
            WHERE r.EventId = {cuc} AND r.BibNumber = 2007 AND cp.Type = 'Split' ORDER BY cp.Sequence DESC
            """))!;
        var parts2007 = cpInfo.Split('|');
        (await (await _api.As("staff.hn")).Post("api/race-day/timing",
            new { eventId = cuc, bibNumber = 2007, checkpointId = int.Parse(parts2007[0]), recordedAt = parts2007[1] })).Expect(200);
        rows = (await org.Get($"api/events/{cuc}/results/rows?pageSize=100")).Expect(200).Json!["items"]!.AsArray();
        Assert.Equal("Finished", rows.First(r => r!["bibNumber"]?.GetValue<int?>() == 2007)!["status"]!.GetValue<string>());

        // BUG-05: hai trạm ghi song song cho cùng BIB (CP sau lúc +90′, đích lúc +80′) ⇒ chỉ một bản ghi được nhận (BR-21)
        var cp2010 = ((string)(await _api.SqlAsync($"""
            SELECT STRING_AGG(CAST(cp.CheckpointId AS varchar(10)) + '@' + CONVERT(varchar(30), c.StartAt, 126), ',') WITHIN GROUP (ORDER BY cp.Sequence)
            FROM Registrations r JOIN RaceCategories c ON c.CategoryId = r.CategoryId JOIN Checkpoints cp ON cp.CategoryId = c.CategoryId
            WHERE r.EventId = {cuc} AND r.BibNumber = 2010 AND cp.Type IN ('Split', 'Finish')
            """))!).Split(',');
        var split = cp2010[0].Split('@');
        var finish = cp2010[^1].Split('@');
        var gun = DateTime.Parse(split[1]);
        var staff = await _api.As("staff.hn");
        var racing = await Task.WhenAll(
            staff.Post("api/race-day/timing", new { eventId = cuc, bibNumber = 2010, checkpointId = int.Parse(split[0]), recordedAt = VnTime.F(gun.AddMinutes(90)) }),
            org.Post("api/race-day/timing", new { eventId = cuc, bibNumber = 2010, checkpointId = int.Parse(finish[0]), recordedAt = VnTime.F(gun.AddMinutes(80)) }));
        Assert.Equal(1, racing.Count(x => x.Code == 200));
        Assert.All(racing, x => Assert.True(x.Code is 200 or 409, $"{x.Code}: {x.Text}"));
        var board = (await (await _api.As(null)).Get($"odata/Results?$filter=EventId eq {cuc}&$count=true")).Expect(200);
        Assert.True(board.Json!["@odata.count"]!.GetValue<int>() > 0);

        // UC-66: khiếu nại của member1 trên kết quả của mình; người khác không khiếu nại hộ
        var m1 = await _api.As("member1");
        var myReg = (await m1.Get("api/registrations/me")).Expect(200).Json!.AsArray()
            .First(r => r!["eventId"]!.GetValue<int>() == cuc)!["id"]!.GetValue<int>();
        var detail = (await (await _api.As(null)).Get($"api/results/registrations/{myReg}")).Expect(200);
        var resultId = detail.Json!["summary"]!["resultId"]!.GetValue<int>();
        foreach (var key in new[] { "dateOfBirth", "idNumber", "phone", "bloodType", "medicalNotes" })
            Assert.DoesNotContain($"\"{key}\"", detail.Text);
        (await (await _api.As("member2")).Post($"api/results/{resultId}/complaints", new { content = "Toi khieu nai thoi gian cua nguoi khac" })).ExpectAny(403, 404);
        (await m1.Post($"api/results/{resultId}/complaints", new { content = "ngan" })).Expect(400);
        var complaint = (await m1.Post($"api/results/{resultId}/complaints", new { content = "Thoi gian tai CP2 bi thieu, xin kiem tra lai" })).ExpectAny(200, 201);
        var complaintId = complaint.Id;

        // BR-27: còn khiếu nại chưa xử lý ⇒ chưa chốt được
        (await org.Post($"api/events/{cuc}/results/official")).Expect(409);
        // Hết cửa sổ khiếu nại (giả lập bằng cách lùi thời điểm công bố tạm) nhưng còn khiếu nại chờ ⇒ vẫn chặn
        await _api.SqlAsync($"UPDATE Events SET ResultsProvisionalAt = DATEADD(day, -3, ResultsProvisionalAt) WHERE EventId = {cuc}");
        (await org.Post($"api/events/{cuc}/results/official")).Expect(409);
        (await org.Post($"api/complaints/{complaintId}/resolve", new { decision = "Rejected", response = "Da kiem tra, du lieu dung." })).Expect(200);
        (await org.Post($"api/events/{cuc}/results/official")).Expect(200);

        // UC-28/65: danh sách nhận giải công khai; e-certificate của VĐV về đích
        (await (await _api.As(null)).Get($"api/events/{cuc}/winners")).Expect(200);
        var finisher = rows.First(r => r!["status"]!.GetValue<string>() == "Finished")!["registrationId"]!.GetValue<int>();
        (await (await _api.As(null)).Get($"api/results/registrations/{finisher}/certificate")).Expect(200);
        (await m1.Get("api/achievements/me")).Expect(200);
    }

    [Fact]
    public async Task Staff_cannot_record_timing_for_own_registration_br32()
    {
        // staff.hn không có đăng ký trong seed ⇒ dựng: gán chủ một đăng ký có BIB của giải Cúc Phương cho staff.hn
        var cuc = Convert.ToInt32(await _api.SqlAsync("SELECT EventId FROM Events WHERE Slug = 'demo-cuc-phuong-trail-2026'"));
        var staffId = (string)(await _api.SqlAsync("SELECT Id FROM AspNetUsers WHERE Email = 'staff.hn@mrms.local'"))!;
        var row = await _api.SqlAsync($"""
            DECLARE @r int = (SELECT TOP(1) RegistrationId FROM Registrations WHERE EventId = {cuc} AND BibNumber IS NOT NULL AND RegisteredByUserId = (SELECT Id FROM AspNetUsers WHERE Email = 'member3@mrms.local') ORDER BY RegistrationId DESC);
            UPDATE Registrations SET RegisteredByUserId = '{staffId}' WHERE RegistrationId = @r;
            SELECT CAST(BibNumber AS varchar(10)) + ',' + CAST(CategoryId AS varchar(10)) FROM Registrations WHERE RegistrationId = @r;
            """);
        var parts = ((string)row!).Split(',');
        var cp = Convert.ToInt32(await _api.SqlAsync($"SELECT TOP(1) CheckpointId FROM Checkpoints WHERE CategoryId = {parts[1]} AND Type = 'Finish'"));
        var r = await (await _api.As("staff.hn")).Post("api/race-day/timing",
            new { eventId = cuc, bibNumber = int.Parse(parts[0]), checkpointId = cp, recordedAt = VnTime.F(VnTime.Now.AddMinutes(-1)) });
        r.ExpectAny(403, 409);
        Assert.Contains("BR-32", r.Text);
    }
}
