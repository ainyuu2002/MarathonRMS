# Marathon Race Management System (MRMS)

PRN232 final project – a multi-organizer platform for running races (road & trail):
**ASP.NET Core Web API** (EF Core, SQL Server, Identity, JWT, OData) + **Razor Pages** client (cookie auth, Bootstrap 5).

## Yêu cầu môi trường

| Thành phần | Phiên bản | Ghi chú |
|---|---|---|
| .NET SDK | **8.0.4xx** | Khóa bằng `global.json` – máy có cả SDK 10 vẫn dùng SDK 8 |
| SQL Server | LocalDB (đi kèm Visual Studio) **hoặc** SQL Server bất kỳ | Xem bên dưới |
| dotnet-ef | 8.x | `dotnet tool install -g dotnet-ef --version 8.*` |
| Visual Studio 2022 (17.8+) hoặc VS Code + C# Dev Kit | | |

## Chạy lần đầu

```bash
# 1. Khóa ký JWT (mỗi máy tự đặt – KHÔNG commit). Chuỗi ngẫu nhiên ≥ 32 ký tự.
dotnet user-secrets set "Jwt:SigningKey" "<chuỗi-ngẫu-nhiên-ít-nhất-32-ký-tự>" --project src/MarathonRMS.API

# 2. Chạy API – tự áp migration và nạp dữ liệu demo ở môi trường Development
dotnet run --project src/MarathonRMS.API --launch-profile https      # https://localhost:7040/swagger

# 3. Chạy WebApp (terminal khác)
dotnet run --project src/MarathonRMS.WebApp --launch-profile https   # https://localhost:7140
```

Mặc định API dùng LocalDB: `Server=(localdb)\MSSQLLocalDB;Database=MarathonRMS`. Muốn dùng SQL Server khác, ghi đè bằng user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:MarathonDb" "Server=localhost;Database=MarathonRMS;Trusted_Connection=True;TrustServerCertificate=True" --project src/MarathonRMS.API
```

## Tài khoản demo

Được tạo tự động khi database còn trống (môi trường Development). Mật khẩu chung nằm ở `src/MarathonRMS.API/appsettings.Development.json` → `Seed:DemoPassword` (chỉ dùng cho máy local).

| Email | Vai trò | BTC |
|---|---|---|
| superadmin@mrms.local | SuperAdmin | – |
| admin@mrms.local | Admin | – |
| organizer.hn@mrms.local / staff.hn@mrms.local | Organizer / Staff (+ Member) | Hanoi Demo Running |
| organizer.sg@mrms.local / staff.sg@mrms.local | Organizer / Staff (+ Member) | Saigon Demo Sports |
| member1..3@mrms.local | Member | – |

Muốn nạp lại từ đầu: xóa database (`dotnet ef database drop --project src/MarathonRMS.BusinessObjects --startup-project src/MarathonRMS.API`) rồi chạy lại API.

## Cấu trúc

```
src/
├── MarathonRMS.BusinessObjects     Entities, Enums, DbContext, Fluent API (ràng buộc DB), Migrations, SeedData
├── MarathonRMS.DataAccessObjects   DAO – truy vấn EF Core (nhận DbContext qua DI, không SaveChanges)
├── MarathonRMS.Repositories        Repository + UnitOfWork
├── MarathonRMS.Services            Business rules (BR-xx), mapping DTO, JWT, hạ tầng (clock, audit, settings)
├── MarathonRMS.DTOs                DTO dùng chung API ↔ WebApp, AppRoles, ErrorCodes
├── MarathonRMS.API                 Controllers, OData, JWT, Swagger, xử lý lỗi, DbInitializer
└── MarathonRMS.WebApp              Razor Pages, typed HttpClient + JwtForwardingHandler, cookie auth
```

Phụ thuộc: `API → Services → Repositories → DataAccessObjects → BusinessObjects`; `API, Services → DTOs`; **`WebApp → DTOs` duy nhất** (WebApp không truy cập DB).

## Thành phần dùng chung (đã có – dùng lại, đừng viết mới)

| Ở đâu | Thành phần |
|---|---|
| `Services/Common` | `IOrganizationScope` (phạm vi BTC BR-30, BTC bị khóa, xung đột lợi ích BR-32) · `IPrivacyFilter` (ẩn tên công khai BR-37, che số giấy tờ) · `AgeCalculator` · `IHtmlContentSanitizer` (BR-50) · `IAuditLogger` · `IDateTimeProvider` · `ISystemSettingsProvider` · `ToPagedAsync` · `SlugHelper` · `EnumParser` |
| `WebApp` | `<status-badge status="…" />` · `_Pager` + `PagerModel` · `_RichTextEditor` + `_RichTextEditorAssets` · `_NavCards` · `BasePageModel.HandleApiErrorAsync` · `ApiClientBase` (Get/Post/Put/Patch/Delete, `Query`) |

Đăng ký DI / typed client mới: thêm trong khối chú thích của phần mình ở `ServiceCollectionExtensions.cs` và `WebApp/Program.cs`.

## Quy ước

- **Phiên bản package** chỉ khai báo trong `Directory.Packages.props`; csproj ghi `<PackageReference Include="..." />` không kèm Version.
- **Lỗi nghiệp vụ**: Service ném `BusinessRuleException("BR-xx", "...")` → API trả `ProblemDetails` có `code`; WebApp bắt `ApiException` qua `BasePageModel.HandleApiErrorAsync`.
- **Phân quyền WebApp** theo thư mục trang: `/SuperAdmin`, `/Admin`, `/Organizer`, `/Staff`, `/Member` (cấu hình trong `Program.cs`).
- **Migration**: chỉ một người tạo migration tại một thời điểm, đặt tên theo tính năng:
  ```bash
  dotnet ef migrations add <TenMigration> --project src/MarathonRMS.BusinessObjects --startup-project src/MarathonRMS.API --output-dir Migrations
  ```
- **Không commit secret**: khóa JWT, khóa VNPay, mật khẩu SQL dùng `dotnet user-secrets` / `.env`.
- **Mapping**: viết tay (LINQ `Select` sang DTO) – không dùng AutoMapper (bản miễn phí cuối có lỗ hổng bảo mật, bản vá dùng giấy phép thương mại).
