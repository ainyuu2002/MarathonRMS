using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarathonRMS.BusinessObjects.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThemeColor",
                table: "Events",
                type: "varchar(7)",
                unicode: false,
                maxLength: 7,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BibTransfers",
                columns: table => new
                {
                    TransferId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationId = table.Column<int>(type: "int", nullable: false),
                    FromUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ToParticipantId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    NewRegistrationId = table.Column<int>(type: "int", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibTransfers", x => x.TransferId);
                    table.CheckConstraint("CK_BibTransfers_Fee", "[FeeAmount] >= 0");
                    table.CheckConstraint("CK_BibTransfers_Users", "[FromUserId] <> [ToUserId]");
                    table.ForeignKey(
                        name: "FK_BibTransfers_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "RegistrationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    CouponId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    EventId = table.Column<int>(type: "int", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    OwnerMemberId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DiscountType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: true),
                    MaxUses = table.Column<int>(type: "int", nullable: false),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    PartnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.CouponId);
                    table.CheckConstraint("CK_Coupons_CategoryNeedsEvent", "[CategoryId] IS NULL OR [EventId] IS NOT NULL");
                    table.CheckConstraint("CK_Coupons_MaxDiscount", "[MaxDiscountAmount] IS NULL OR ([MaxDiscountAmount] > 0 AND [MaxDiscountAmount] % 1000 = 0)");
                    table.CheckConstraint("CK_Coupons_Range", "[ValidFrom] < [ValidTo]");
                    table.CheckConstraint("CK_Coupons_Uses", "[MaxUses] >= 1 AND [UsedCount] >= 0 AND [UsedCount] <= [MaxUses]");
                    table.CheckConstraint("CK_Coupons_Value", "([DiscountType] = 'Percent' AND [DiscountValue] BETWEEN 1 AND 100) OR ([DiscountType] = 'Amount' AND [DiscountValue] > 0 AND [DiscountValue] % 1000 = 0)");
                    table.ForeignKey(
                        name: "FK_Coupons_AspNetUsers_OwnerMemberId",
                        column: x => x.OwnerMemberId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Coupons_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId");
                    table.ForeignKey(
                        name: "FK_Coupons_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "OrganizationId");
                    table.ForeignKey(
                        name: "FK_Coupons_RaceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "RaceCategories",
                        principalColumn: "CategoryId");
                });

            migrationBuilder.CreateTable(
                name: "EventContentSections",
                columns: table => new
                {
                    SectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    SectionType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventContentSections", x => x.SectionId);
                    table.CheckConstraint("CK_EventContentSections_Order", "[DisplayOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_EventContentSections_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventScheduleItems",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventScheduleItems", x => x.ItemId);
                    table.CheckConstraint("CK_EventScheduleItems_Range", "[EndsAt] IS NULL OR [EndsAt] > [StartsAt]");
                    table.ForeignKey(
                        name: "FK_EventScheduleItems_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prizes",
                columns: table => new
                {
                    PrizeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    AwardType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Gender = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    AgeGroupId = table.Column<int>(type: "int", nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Reward = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CashAmount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prizes", x => x.PrizeId);
                    table.CheckConstraint("CK_Prizes_AgeGroup", "([AwardType] = 'AgeGroup' AND [AgeGroupId] IS NOT NULL) OR ([AwardType] <> 'AgeGroup' AND [AgeGroupId] IS NULL)");
                    table.CheckConstraint("CK_Prizes_Cash", "[CashAmount] IS NULL OR [CashAmount] >= 0");
                    table.CheckConstraint("CK_Prizes_Rank", "[Rank] >= 1");
                    table.ForeignKey(
                        name: "FK_Prizes_AgeGroups_AgeGroupId",
                        column: x => x.AgeGroupId,
                        principalTable: "AgeGroups",
                        principalColumn: "AgeGroupId");
                    table.ForeignKey(
                        name: "FK_Prizes_RaceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "RaceCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaceRoutes",
                columns: table => new
                {
                    RouteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    RouteGeoJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalculatedDistanceKm = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: false),
                    Source = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceRoutes", x => x.RouteId);
                    table.CheckConstraint("CK_RaceRoutes_Distance", "[CalculatedDistanceKm] > 0");
                    table.CheckConstraint("CK_RaceRoutes_Json", "ISJSON([RouteGeoJson]) = 1");
                    table.ForeignKey(
                        name: "FK_RaceRoutes_RaceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "RaceCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    RefundId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    Reason = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    BankInfo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ProcessedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    TransactionRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.RefundId);
                    table.CheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Refunds_Completed", "[Status] <> 'Completed' OR [TransactionRef] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Refunds_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "RegistrationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultComplaints",
                columns: table => new
                {
                    ComplaintId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, scale: 0, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Response = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HandledById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    HandledAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultComplaints", x => x.ComplaintId);
                    table.CheckConstraint("CK_ResultComplaints_Fee", "[FeeAmount] >= 0");
                    table.CheckConstraint("CK_ResultComplaints_Response", "[Status] = 'Open' OR [Response] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_ResultComplaints_Results_ResultId",
                        column: x => x.ResultId,
                        principalTable: "Results",
                        principalColumn: "ResultId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoutePois",
                columns: table => new
                {
                    PoiId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    DistanceFromStartKm = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutePois", x => x.PoiId);
                    table.CheckConstraint("CK_RoutePois_Coordinates", "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "FK_RoutePois_RaceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "RaceCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_CouponId",
                table: "Registrations",
                column: "CouponId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_ThemeColor",
                table: "Events",
                sql: "[ThemeColor] IS NULL OR [ThemeColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");

            migrationBuilder.CreateIndex(
                name: "IX_BibTransfers_ToUserId_Status",
                table: "BibTransfers",
                columns: new[] { "ToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_BibTransfers_Registration_Open",
                table: "BibTransfers",
                column: "RegistrationId",
                unique: true,
                filter: "[Status] IN ('Pending','AwaitingFee')");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_CategoryId",
                table: "Coupons",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code",
                table: "Coupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_EventId",
                table: "Coupons",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_OrganizationId_IsActive",
                table: "Coupons",
                columns: new[] { "OrganizationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_OwnerMemberId",
                table: "Coupons",
                column: "OwnerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_EventContentSections_EventId_DisplayOrder",
                table: "EventContentSections",
                columns: new[] { "EventId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_EventScheduleItems_EventId_StartsAt",
                table: "EventScheduleItems",
                columns: new[] { "EventId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Prizes_AgeGroupId",
                table: "Prizes",
                column: "AgeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Prizes_CategoryId_AwardType_Gender_AgeGroupId_Rank",
                table: "Prizes",
                columns: new[] { "CategoryId", "AwardType", "Gender", "AgeGroupId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaceRoutes_CategoryId",
                table: "RaceRoutes",
                column: "CategoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RegistrationId",
                table: "Refunds",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultComplaints_ResultId_Status",
                table: "ResultComplaints",
                columns: new[] { "ResultId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutePois_CategoryId",
                table: "RoutePois",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Registrations_Coupons_CouponId",
                table: "Registrations",
                column: "CouponId",
                principalTable: "Coupons",
                principalColumn: "CouponId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Registrations_Coupons_CouponId",
                table: "Registrations");

            migrationBuilder.DropTable(
                name: "BibTransfers");

            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropTable(
                name: "EventContentSections");

            migrationBuilder.DropTable(
                name: "EventScheduleItems");

            migrationBuilder.DropTable(
                name: "Prizes");

            migrationBuilder.DropTable(
                name: "RaceRoutes");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "ResultComplaints");

            migrationBuilder.DropTable(
                name: "RoutePois");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_CouponId",
                table: "Registrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_ThemeColor",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ThemeColor",
                table: "Events");
        }
    }
}
