using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MarathonRMS.BusinessObjects.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "LegalDocuments",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    PublishedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocuments", x => x.DocumentId);
                    table.CheckConstraint("CK_LegalDocuments_Version", "[Version] >= 1");
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    OrganizationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    TaxCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "PenaltyReasons",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefaultAction = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DefaultMinutes = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenaltyReasons", x => x.Code);
                    table.CheckConstraint("CK_PenaltyReasons_Minutes", "[DefaultAction] <> 'TimePenalty' OR [DefaultMinutes] > 0");
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DataType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    VenueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.VenueId);
                    table.CheckConstraint("CK_Venues_Latitude", "[Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_Venues_Longitude", "[Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "OrganizationId");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    VenueId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    DescriptionHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BannerUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WaiverText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WaiverVersion = table.Column<int>(type: "int", nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegistrationOpenAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RegistrationCloseAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    PostponedFromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResultsStatus = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ResultsProvisionalAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ResultsOfficialAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    EmergencyPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    InsuranceNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventId);
                    table.CheckConstraint("CK_Events_EndDate", "[EndDate] >= [EventDate]");
                    table.CheckConstraint("CK_Events_RegistrationWindow", "[RegistrationOpenAt] < [RegistrationCloseAt] AND CAST([RegistrationCloseAt] AS date) <= [EventDate]");
                    table.CheckConstraint("CK_Events_WaiverVersion", "[WaiverVersion] >= 1");
                    table.ForeignKey(
                        name: "FK_Events_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "OrganizationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "VenueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationApplications",
                columns: table => new
                {
                    ApplicationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicantId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OrganizationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RepresentativeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DocumentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    OrganizationId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationApplications", x => x.ApplicationId);
                    table.CheckConstraint("CK_OrgApplications_RejectReason", "[Status] <> 'Rejected' OR [RejectReason] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_OrganizationApplications_AspNetUsers_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationApplications_AspNetUsers_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrganizationApplications_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "OrganizationId");
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    ParticipantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LinkedUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Relationship = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IdType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    IdNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    TShirtSize = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    BloodType = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    MedicalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClubName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.ParticipantId);
                    table.CheckConstraint("CK_Participants_IdNumber_Type", "[IdNumber] IS NULL OR [IdType] IS NOT NULL");
                    table.CheckConstraint("CK_Participants_Self_Linked", "[Relationship] = 'Self' OR [LinkedUserId] IS NULL");
                    table.ForeignKey(
                        name: "FK_Participants_AspNetUsers_LinkedUserId",
                        column: x => x.LinkedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Participants_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventMedia",
                columns: table => new
                {
                    MediaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Kind = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    UploadedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventMedia", x => x.MediaId);
                    table.ForeignKey(
                        name: "FK_EventMedia_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventPolicies",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "int", nullable: false),
                    AgeCalculationMode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    GuardianConsentUnderAge = table.Column<int>(type: "int", nullable: false),
                    MaxDependentsPerAccount = table.Column<int>(type: "int", nullable: false),
                    InfoEditDeadline = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    TransferPolicy = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    TransferFee = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    MaxTransfers = table.Column<int>(type: "int", nullable: false),
                    TransferDeadline = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CategoryChangeFee = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    MaxCategoryChanges = table.Column<int>(type: "int", nullable: false),
                    CategoryChangeDeadline = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LockSpecialEntries = table.Column<bool>(type: "bit", nullable: false),
                    OverallRankingBasis = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    AgeGroupRankingBasis = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    TieBreakRule = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    LateStartToleranceMinutes = table.Column<int>(type: "int", nullable: true),
                    ComplaintWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    ComplaintFee = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    AwardStackingPolicy = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ForceMajeurePolicy = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPolicies", x => x.EventId);
                    table.CheckConstraint("CK_EventPolicies_ComplaintWindow", "[ComplaintWindowMinutes] BETWEEN 10 AND 20160");
                    table.CheckConstraint("CK_EventPolicies_Fees", "[TransferFee] >= 0 AND [CategoryChangeFee] >= 0 AND [ComplaintFee] >= 0");
                    table.CheckConstraint("CK_EventPolicies_GuardianAge", "[GuardianConsentUnderAge] BETWEEN 0 AND 21");
                    table.CheckConstraint("CK_EventPolicies_LateStart", "[LateStartToleranceMinutes] IS NULL OR [LateStartToleranceMinutes] > 0");
                    table.CheckConstraint("CK_EventPolicies_MaxChanges", "[MaxTransfers] >= 0 AND [MaxCategoryChanges] >= 0");
                    table.CheckConstraint("CK_EventPolicies_MaxDependents", "[MaxDependentsPerAccount] BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_EventPolicies_PaidTransfer", "[TransferPolicy] <> 'Paid' OR [TransferFee] > 0");
                    table.ForeignKey(
                        name: "FK_EventPolicies_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RaceCategories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Discipline = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CategoryType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DistanceKm = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    ActualDistanceKm = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    ElevationGainM = table.Column<int>(type: "int", nullable: true),
                    ItraPoints = table.Column<byte>(type: "tinyint", nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    StartAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CutOffMinutes = table.Column<int>(type: "int", nullable: true),
                    MinAge = table.Column<int>(type: "int", nullable: false),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    EscortRequiredUnderAge = table.Column<int>(type: "int", nullable: true),
                    IsTimed = table.Column<bool>(type: "bit", nullable: false),
                    IsRanked = table.Column<bool>(type: "bit", nullable: false),
                    BibFrom = table.Column<int>(type: "int", nullable: false),
                    BibTo = table.Column<int>(type: "int", nullable: false),
                    BibColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MedalImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceCategories", x => x.CategoryId);
                    table.CheckConstraint("CK_RaceCategories_Age", "[MinAge] BETWEEN 0 AND 100 AND ([MaxAge] IS NULL OR [MaxAge] >= [MinAge])");
                    table.CheckConstraint("CK_RaceCategories_Bib", "[BibFrom] > 0 AND [BibFrom] < [BibTo]");
                    table.CheckConstraint("CK_RaceCategories_Capacity", "[Capacity] > 0");
                    table.CheckConstraint("CK_RaceCategories_CutOff", "[IsTimed] = 0 OR [CutOffMinutes] > 0");
                    table.CheckConstraint("CK_RaceCategories_Distance", "[DistanceKm] > 0 AND ([ActualDistanceKm] IS NULL OR [ActualDistanceKm] > 0)");
                    table.CheckConstraint("CK_RaceCategories_Elevation", "[ElevationGainM] IS NULL OR [ElevationGainM] >= 0");
                    table.CheckConstraint("CK_RaceCategories_Itra", "[ItraPoints] IS NULL OR [ItraPoints] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_RaceCategories_RankedNeedsTimed", "[IsRanked] = 0 OR [IsTimed] = 1");
                    table.ForeignKey(
                        name: "FK_RaceCategories_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgeGroups",
                columns: table => new
                {
                    AgeGroupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: false),
                    MaxAge = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgeGroups", x => x.AgeGroupId);
                    table.CheckConstraint("CK_AgeGroups_Range", "[MinAge] >= 0 AND ([MaxAge] IS NULL OR [MaxAge] >= [MinAge])");
                    table.ForeignKey(
                        name: "FK_AgeGroups_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgeGroups_RaceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "RaceCategories",
                        principalColumn: "CategoryId");
                });

            migrationBuilder.CreateTable(
                name: "Checkpoints",
                columns: table => new
                {
                    CheckpointId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DistanceKm = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    CutOffAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    StationType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checkpoints", x => x.CheckpointId);
                    table.CheckConstraint("CK_Checkpoints_Coordinates", "([Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90) AND ([Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180)");
                    table.CheckConstraint("CK_Checkpoints_Distance", "[DistanceKm] >= 0");
                    table.CheckConstraint("CK_Checkpoints_Sequence", "[Sequence] >= 1");
                    table.ForeignKey(
                        name: "FK_Checkpoints_RaceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "RaceCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceTiers",
                columns: table => new
                {
                    PriceTierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceTiers", x => x.PriceTierId);
                    table.CheckConstraint("CK_PriceTiers_Price", "[Price] >= 0 AND [Price] % 1000 = 0");
                    table.CheckConstraint("CK_PriceTiers_Range", "[ValidFrom] < [ValidTo]");
                    table.ForeignKey(
                        name: "FK_PriceTiers_RaceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "RaceCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Registrations",
                columns: table => new
                {
                    RegistrationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    ParticipantId = table.Column<int>(type: "int", nullable: false),
                    RegisteredByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntryType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    BibNumber = table.Column<int>(type: "int", nullable: true),
                    OriginalPrice = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    CoinDiscountAmount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    FinalPrice = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    CouponId = table.Column<int>(type: "int", nullable: true),
                    QrToken = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ChangeCount = table.Column<int>(type: "int", nullable: false),
                    TransferCount = table.Column<int>(type: "int", nullable: false),
                    HideFromPublicList = table.Column<bool>(type: "bit", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BibName = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    IdType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    IdNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TShirtSize = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmergencyContactPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    BloodType = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    MedicalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClubName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GuardianName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GuardianPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    GuardianIdNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    GuardianConsent = table.Column<bool>(type: "bit", nullable: false),
                    EscortName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EscortPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    WaiverAccepted = table.Column<bool>(type: "bit", nullable: false),
                    HealthDeclarationAccepted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.RegistrationId);
                    table.CheckConstraint("CK_Registrations_Bib", "[BibNumber] IS NULL OR [BibNumber] > 0");
                    table.CheckConstraint("CK_Registrations_BibOnlyConfirmed", "[BibNumber] IS NULL OR [Status] IN ('Confirmed','Transferred')");
                    table.CheckConstraint("CK_Registrations_Consents", "[WaiverAccepted] = 1 AND [HealthDeclarationAccepted] = 1");
                    table.CheckConstraint("CK_Registrations_Counts", "[ChangeCount] >= 0 AND [TransferCount] >= 0");
                    table.CheckConstraint("CK_Registrations_IdNumber_Type", "[IdNumber] IS NULL OR [IdType] IS NOT NULL");
                    table.CheckConstraint("CK_Registrations_Prices", "[OriginalPrice] >= 0 AND [DiscountAmount] >= 0 AND [CoinDiscountAmount] >= 0 AND [FinalPrice] >= 0 AND [FinalPrice] = [OriginalPrice] - [DiscountAmount] - [CoinDiscountAmount]");
                    table.ForeignKey(
                        name: "FK_Registrations_AspNetUsers_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Registrations_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Registrations_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "ParticipantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Registrations_RaceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "RaceCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    ConsentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RegistrationId = table.Column<int>(type: "int", nullable: true),
                    ConsentType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DocumentVersion = table.Column<int>(type: "int", nullable: false),
                    Granted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.ConsentId);
                    table.CheckConstraint("CK_ConsentRecords_Version", "[DocumentVersion] >= 1");
                    table.ForeignKey(
                        name: "FK_ConsentRecords_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "RegistrationId");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    TxnRef = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ProviderTransactionNo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ResponseCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentId);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Payments_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "RegistrationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaceKitPickups",
                columns: table => new
                {
                    PickupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationId = table.Column<int>(type: "int", nullable: false),
                    PickedUpAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    StaffId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IdChecked = table.Column<bool>(type: "bit", nullable: false),
                    IsProxy = table.Column<bool>(type: "bit", nullable: false),
                    ProxyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProxyIdNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceKitPickups", x => x.PickupId);
                    table.CheckConstraint("CK_RaceKitPickups_Proxy", "[IsProxy] = 0 OR ([ProxyName] IS NOT NULL AND [ProxyIdNumber] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RaceKitPickups_AspNetUsers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaceKitPickups_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "RegistrationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    ResultId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationId = table.Column<int>(type: "int", nullable: false),
                    GunTimeMs = table.Column<long>(type: "bigint", nullable: true),
                    ChipTimeMs = table.Column<long>(type: "bigint", nullable: true),
                    PenaltySeconds = table.Column<int>(type: "int", nullable: false),
                    FinalTimeMs = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DnfReason = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    DnfCheckpointId = table.Column<int>(type: "int", nullable: true),
                    OverallRank = table.Column<int>(type: "int", nullable: true),
                    GenderRank = table.Column<int>(type: "int", nullable: true),
                    AgeGroupId = table.Column<int>(type: "int", nullable: true),
                    AgeGroupRank = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.ResultId);
                    table.CheckConstraint("CK_Results_ChipLeGun", "[ChipTimeMs] IS NULL OR [GunTimeMs] IS NULL OR [ChipTimeMs] <= [GunTimeMs]");
                    table.CheckConstraint("CK_Results_DnfReason", "[Status] <> 'DNF' OR [DnfReason] IS NOT NULL");
                    table.CheckConstraint("CK_Results_Penalty", "[PenaltySeconds] >= 0");
                    table.CheckConstraint("CK_Results_Ranks", "([OverallRank] IS NULL OR [OverallRank] >= 1) AND ([GenderRank] IS NULL OR [GenderRank] >= 1) AND ([AgeGroupRank] IS NULL OR [AgeGroupRank] >= 1)");
                    table.ForeignKey(
                        name: "FK_Results_AgeGroups_AgeGroupId",
                        column: x => x.AgeGroupId,
                        principalTable: "AgeGroups",
                        principalColumn: "AgeGroupId");
                    table.ForeignKey(
                        name: "FK_Results_Checkpoints_DnfCheckpointId",
                        column: x => x.DnfCheckpointId,
                        principalTable: "Checkpoints",
                        principalColumn: "CheckpointId");
                    table.ForeignKey(
                        name: "FK_Results_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "RegistrationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimingRecords",
                columns: table => new
                {
                    TimingRecordId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationId = table.Column<int>(type: "int", nullable: false),
                    CheckpointId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    Source = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    RecordedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimingRecords", x => x.TimingRecordId);
                    table.ForeignKey(
                        name: "FK_TimingRecords_Checkpoints_CheckpointId",
                        column: x => x.CheckpointId,
                        principalTable: "Checkpoints",
                        principalColumn: "CheckpointId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimingRecords_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "RegistrationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultPenalties",
                columns: table => new
                {
                    PenaltyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultId = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(40)", nullable: false),
                    Action = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Minutes = table.Column<int>(type: "int", nullable: true),
                    CheckpointId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IssuedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultPenalties", x => x.PenaltyId);
                    table.CheckConstraint("CK_ResultPenalties_Minutes", "[Action] <> 'TimePenalty' OR [Minutes] > 0");
                    table.ForeignKey(
                        name: "FK_ResultPenalties_PenaltyReasons_ReasonCode",
                        column: x => x.ReasonCode,
                        principalTable: "PenaltyReasons",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultPenalties_Results_ResultId",
                        column: x => x.ResultId,
                        principalTable: "Results",
                        principalColumn: "ResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "0b8f0e6c-1a11-4c6e-9d01-000000000001", "0b8f0e6c-1a11-4c6e-9d01-000000000001", "SuperAdmin", "SUPERADMIN" },
                    { "0b8f0e6c-1a11-4c6e-9d01-000000000002", "0b8f0e6c-1a11-4c6e-9d01-000000000002", "Admin", "ADMIN" },
                    { "0b8f0e6c-1a11-4c6e-9d01-000000000003", "0b8f0e6c-1a11-4c6e-9d01-000000000003", "Organizer", "ORGANIZER" },
                    { "0b8f0e6c-1a11-4c6e-9d01-000000000004", "0b8f0e6c-1a11-4c6e-9d01-000000000004", "Staff", "STAFF" },
                    { "0b8f0e6c-1a11-4c6e-9d01-000000000005", "0b8f0e6c-1a11-4c6e-9d01-000000000005", "Member", "MEMBER" }
                });

            migrationBuilder.InsertData(
                table: "LegalDocuments",
                columns: new[] { "DocumentId", "Content", "DocType", "PublishedAt", "PublishedById", "Version" },
                values: new object[,]
                {
                    { 1, "<h2>Điều khoản sử dụng</h2><p>Phiên bản 1 – nội dung mẫu. SuperAdmin cập nhật tại trang quản trị.</p>", "Terms", new DateTime(2026, 10, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1 },
                    { 2, "<h2>Chính sách quyền riêng tư</h2><p>Phiên bản 1 – nội dung mẫu, căn cứ Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 và Nghị định 356/2025/NĐ-CP.</p>", "Privacy", new DateTime(2026, 10, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1 }
                });

            migrationBuilder.InsertData(
                table: "PenaltyReasons",
                columns: new[] { "Code", "DefaultAction", "DefaultMinutes", "IsActive", "Name" },
                values: new object[,]
                {
                    { "BIB_NOT_VISIBLE", "Warning", null, true, "Không đeo BIB đúng quy định" },
                    { "BIB_TRANSFER", "DSQ", null, true, "Chuyển nhượng hoặc dùng BIB trái phép" },
                    { "FALSE_INFO", "DSQ", null, true, "Khai sai thông tin cá nhân (tuổi, giới tính…)" },
                    { "LITTERING", "TimePenalty", 15, true, "Xả rác trên cung đường" },
                    { "MISCONDUCT", "DSQ", null, true, "Hành vi phi thể thao" },
                    { "MISSED_CHECKPOINT", "DSQ", null, true, "Không qua checkpoint bắt buộc" },
                    { "MISSING_GEAR", "TimePenalty", 30, true, "Thiếu vật dụng bắt buộc" },
                    { "MULTIPLE_BIB", "DSQ", null, true, "Đeo nhiều BIB / chạy hộ" },
                    { "OTHER", "Warning", null, true, "Lý do khác (ghi rõ trong ghi chú)" },
                    { "PRO_IN_AGEGROUP", "DSQ", null, true, "VĐV chuyên nghiệp đăng ký nhóm tuổi phong trào" },
                    { "REFUSE_MEDICAL", "DSQ", null, true, "Không tuân thủ yêu cầu của đội y tế" },
                    { "SHORTCUT", "DSQ", null, true, "Đi tắt, không chạy đúng cung đường" },
                    { "UNAUTHORISED_ASSIST", "DSQ", null, true, "Nhận hỗ trợ trái phép (pacer, xe, người ngoài)" },
                    { "UNDERAGE", "DSQ", null, true, "Không đủ điều kiện tuổi" },
                    { "WHEELED_DEVICE", "DSQ", null, true, "Dùng phương tiện có bánh xe" }
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Key", "DataType", "Description", "UpdatedAt", "UpdatedById", "Value" },
                values: new object[,]
                {
                    { "Fee.Max", "Decimal", "Phí chuyển nhượng/đổi cự ly/khiếu nại tối đa (VND) – BR-48", null, null, "1000000" },
                    { "Media.MaxFileSizeMB", "Int", "Dung lượng ảnh tối đa – BR-51", null, null, "5" },
                    { "Media.MaxPerEvent", "Int", "Số ảnh tối đa mỗi giải – BR-51", null, null, "200" },
                    { "Payment.HoldMinutes.Mock", "Int", "Thời gian giữ chỗ khi thanh toán Mock (phút) – BR-13", null, null, "15" },
                    { "Payment.HoldMinutes.VnPay", "Int", "Thời gian giữ chỗ khi thanh toán VNPay (phút) – BR-13", null, null, "15" },
                    { "Payment.MockEnabled", "Bool", "Bật thanh toán giả lập", null, null, "true" },
                    { "Payment.VnPayEnabled", "Bool", "Bật VNPay sandbox", null, null, "false" },
                    { "Platform.FeePercentDefault", "Decimal", "Phí nền tảng mặc định (%)", null, null, "0" },
                    { "PolicyDefault.ComplaintWindowMinutes", "Int", "Mặc định chính sách giải mới", null, null, "1440" },
                    { "PolicyDefault.GuardianConsentUnderAge", "Int", "Mặc định chính sách giải mới", null, null, "18" },
                    { "PolicyDefault.MaxDependentsPerAccount", "Int", "Mặc định chính sách giải mới", null, null, "5" },
                    { "PolicyDefault.TransferPolicy", "String", "Mặc định chính sách giải mới", null, null, "Forbidden" },
                    { "Price.Max", "Decimal", "Giá tối đa (VND) – BR-48", null, null, "20000000" },
                    { "Price.MinNonZero", "Decimal", "Giá tối thiểu nếu khác 0 (VND) – BR-48", null, null, "10000" },
                    { "Privacy.MedicalRetentionDays", "Int", "Số ngày giữ dữ liệu y tế sau khi kết quả chính thức – BR-52", null, null, "30" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgeGroups_CategoryId",
                table: "AgeGroups",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AgeGroups_EventId",
                table: "AgeGroups",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_CategoryId_Sequence",
                table: "Checkpoints",
                columns: new[] { "CategoryId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_RegistrationId",
                table: "ConsentRecords",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_UserId_ConsentType",
                table: "ConsentRecords",
                columns: new[] { "UserId", "ConsentType" });

            migrationBuilder.CreateIndex(
                name: "IX_EventMedia_EventId_Kind_DisplayOrder",
                table: "EventMedia",
                columns: new[] { "EventId", "Kind", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_OrganizationId",
                table: "Events",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Slug",
                table: "Events",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_Status_EventDate",
                table: "Events",
                columns: new[] { "Status", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueId",
                table: "Events",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_DocType_Version",
                table: "LegalDocuments",
                columns: new[] { "DocType", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_OrganizationId",
                table: "OrganizationApplications",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_ReviewedById",
                table: "OrganizationApplications",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "UX_OrgApplications_Applicant_Pending",
                table: "OrganizationApplications",
                column: "ApplicantId",
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Name",
                table: "Organizations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_TaxCode",
                table: "Organizations",
                column: "TaxCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_LinkedUserId",
                table: "Participants",
                column: "LinkedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_OwnerUserId_IdType_IdNumber",
                table: "Participants",
                columns: new[] { "OwnerUserId", "IdType", "IdNumber" },
                unique: true,
                filter: "[IdNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Participants_Owner_Self",
                table: "Participants",
                column: "OwnerUserId",
                unique: true,
                filter: "[Relationship] = 'Self' AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RegistrationId",
                table: "Payments",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TxnRef",
                table: "Payments",
                column: "TxnRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceTiers_CategoryId_ValidFrom",
                table: "PriceTiers",
                columns: new[] { "CategoryId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_RaceCategories_EventId_Name",
                table: "RaceCategories",
                columns: new[] { "EventId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaceKitPickups_RegistrationId",
                table: "RaceKitPickups",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaceKitPickups_StaffId",
                table: "RaceKitPickups",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_CategoryId_Status",
                table: "Registrations",
                columns: new[] { "CategoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_ParticipantId",
                table: "Registrations",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_QrToken",
                table: "Registrations",
                column: "QrToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_RegisteredByUserId",
                table: "Registrations",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_Status_ExpiresAt",
                table: "Registrations",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Registrations_Event_Bib",
                table: "Registrations",
                columns: new[] { "EventId", "BibNumber" },
                unique: true,
                filter: "[BibNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Registrations_Event_IdNumber_Active",
                table: "Registrations",
                columns: new[] { "EventId", "IdType", "IdNumber" },
                unique: true,
                filter: "[IdNumber] IS NOT NULL AND [Status] IN ('PendingPayment','Confirmed')");

            migrationBuilder.CreateIndex(
                name: "UX_Registrations_Event_Participant_Active",
                table: "Registrations",
                columns: new[] { "EventId", "ParticipantId" },
                unique: true,
                filter: "[Status] IN ('PendingPayment','Confirmed')");

            migrationBuilder.CreateIndex(
                name: "IX_ResultPenalties_ReasonCode",
                table: "ResultPenalties",
                column: "ReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_ResultPenalties_ResultId",
                table: "ResultPenalties",
                column: "ResultId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_AgeGroupId",
                table: "Results",
                column: "AgeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_DnfCheckpointId",
                table: "Results",
                column: "DnfCheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_RegistrationId",
                table: "Results",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimingRecords_CheckpointId",
                table: "TimingRecords",
                column: "CheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_TimingRecords_RegistrationId_CheckpointId",
                table: "TimingRecords",
                columns: new[] { "RegistrationId", "CheckpointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Venues_Name_City",
                table: "Venues",
                columns: new[] { "Name", "City" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DropTable(
                name: "EventMedia");

            migrationBuilder.DropTable(
                name: "EventPolicies");

            migrationBuilder.DropTable(
                name: "LegalDocuments");

            migrationBuilder.DropTable(
                name: "OrganizationApplications");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PriceTiers");

            migrationBuilder.DropTable(
                name: "RaceKitPickups");

            migrationBuilder.DropTable(
                name: "ResultPenalties");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TimingRecords");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "PenaltyReasons");

            migrationBuilder.DropTable(
                name: "Results");

            migrationBuilder.DropTable(
                name: "AgeGroups");

            migrationBuilder.DropTable(
                name: "Checkpoints");

            migrationBuilder.DropTable(
                name: "Registrations");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "RaceCategories");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "Venues");
        }
    }
}
