
IF DB_ID(N'MarathonRMS') IS NULL
    CREATE DATABASE [MarathonRMS];
GO

USE [MarathonRMS];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @upToDate bit = 0;
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NOT NULL
    EXEC sp_executesql
        N'SELECT @d = 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N''20261026120052_Phase2Schema'';',
        N'@d bit OUTPUT', @d = @upToDate OUTPUT;
IF @upToDate = 1
BEGIN
    PRINT N'MarathonRMS already has the latest schema - nothing to do.';
    SET NOEXEC ON;
END;
GO
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AuditLogs] (
    [AuditLogId] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(450) NULL,
    [UserRole] nvarchar(100) NULL,
    [Action] varchar(100) NOT NULL,
    [EntityType] varchar(100) NOT NULL,
    [EntityId] nvarchar(100) NULL,
    [OldValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [IpAddress] varchar(45) NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([AuditLogId])
);
GO

CREATE TABLE [LegalDocuments] (
    [DocumentId] int NOT NULL IDENTITY,
    [DocType] varchar(30) NOT NULL,
    [Version] int NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [PublishedAt] datetime2(3) NOT NULL,
    [PublishedById] nvarchar(450) NULL,
    CONSTRAINT [PK_LegalDocuments] PRIMARY KEY ([DocumentId]),
    CONSTRAINT [CK_LegalDocuments_Version] CHECK ([Version] >= 1)
);
GO

CREATE TABLE [Organizations] (
    [OrganizationId] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Slug] varchar(200) NOT NULL,
    [TaxCode] varchar(20) NOT NULL,
    [LogoUrl] nvarchar(500) NULL,
    [Description] nvarchar(2000) NULL,
    [ContactEmail] nvarchar(256) NOT NULL,
    [ContactPhone] varchar(20) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    CONSTRAINT [PK_Organizations] PRIMARY KEY ([OrganizationId])
);
GO

CREATE TABLE [PenaltyReasons] (
    [Code] varchar(40) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [DefaultAction] varchar(30) NOT NULL,
    [DefaultMinutes] int NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_PenaltyReasons] PRIMARY KEY ([Code]),
    CONSTRAINT [CK_PenaltyReasons_Minutes] CHECK ([DefaultAction] <> 'TimePenalty' OR [DefaultMinutes] > 0)
);
GO

CREATE TABLE [SystemSettings] (
    [Key] varchar(100) NOT NULL,
    [Value] nvarchar(1000) NOT NULL,
    [DataType] varchar(30) NOT NULL,
    [Description] nvarchar(500) NULL,
    [UpdatedById] nvarchar(450) NULL,
    [UpdatedAt] datetime2(3) NULL,
    CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Key])
);
GO

CREATE TABLE [Venues] (
    [VenueId] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Address] nvarchar(300) NOT NULL,
    [City] nvarchar(100) NOT NULL,
    [Latitude] decimal(9,6) NULL,
    [Longitude] decimal(9,6) NULL,
    CONSTRAINT [PK_Venues] PRIMARY KEY ([VenueId]),
    CONSTRAINT [CK_Venues_Latitude] CHECK ([Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90),
    CONSTRAINT [CK_Venues_Longitude] CHECK ([Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180)
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [OrganizationId] int NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUsers_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([OrganizationId])
);
GO

CREATE TABLE [Events] (
    [EventId] int NOT NULL IDENTITY,
    [OrganizationId] int NOT NULL,
    [VenueId] int NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Slug] varchar(200) NOT NULL,
    [DescriptionHtml] nvarchar(max) NULL,
    [BannerUrl] nvarchar(500) NULL,
    [WaiverText] nvarchar(max) NOT NULL,
    [WaiverVersion] int NOT NULL,
    [EventDate] date NOT NULL,
    [EndDate] date NOT NULL,
    [RegistrationOpenAt] datetime2(3) NOT NULL,
    [RegistrationCloseAt] datetime2(3) NOT NULL,
    [Status] varchar(30) NOT NULL,
    [PostponedFromDate] date NULL,
    [CancelReason] nvarchar(1000) NULL,
    [ResultsStatus] varchar(30) NOT NULL,
    [ResultsProvisionalAt] datetime2(3) NULL,
    [ResultsOfficialAt] datetime2(3) NULL,
    [EmergencyPhone] varchar(20) NULL,
    [InsuranceNote] nvarchar(1000) NULL,
    [CreatedById] nvarchar(450) NOT NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    CONSTRAINT [PK_Events] PRIMARY KEY ([EventId]),
    CONSTRAINT [CK_Events_EndDate] CHECK ([EndDate] >= [EventDate]),
    CONSTRAINT [CK_Events_RegistrationWindow] CHECK ([RegistrationOpenAt] < [RegistrationCloseAt] AND CAST([RegistrationCloseAt] AS date) <= [EventDate]),
    CONSTRAINT [CK_Events_WaiverVersion] CHECK ([WaiverVersion] >= 1),
    CONSTRAINT [FK_Events_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([OrganizationId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Events_Venues_VenueId] FOREIGN KEY ([VenueId]) REFERENCES [Venues] ([VenueId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [OrganizationApplications] (
    [ApplicationId] int NOT NULL IDENTITY,
    [ApplicantId] nvarchar(450) NOT NULL,
    [OrganizationName] nvarchar(200) NOT NULL,
    [TaxCode] varchar(20) NOT NULL,
    [RepresentativeName] nvarchar(100) NOT NULL,
    [ContactEmail] nvarchar(256) NOT NULL,
    [ContactPhone] varchar(20) NOT NULL,
    [Address] nvarchar(300) NULL,
    [Description] nvarchar(2000) NULL,
    [DocumentUrl] nvarchar(500) NULL,
    [Status] varchar(30) NOT NULL,
    [RejectReason] nvarchar(500) NULL,
    [ReviewedById] nvarchar(450) NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    [ReviewedAt] datetime2(3) NULL,
    [OrganizationId] int NULL,
    CONSTRAINT [PK_OrganizationApplications] PRIMARY KEY ([ApplicationId]),
    CONSTRAINT [CK_OrgApplications_RejectReason] CHECK ([Status] <> 'Rejected' OR [RejectReason] IS NOT NULL),
    CONSTRAINT [FK_OrganizationApplications_AspNetUsers_ApplicantId] FOREIGN KEY ([ApplicantId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrganizationApplications_AspNetUsers_ReviewedById] FOREIGN KEY ([ReviewedById]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_OrganizationApplications_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([OrganizationId])
);
GO

CREATE TABLE [Participants] (
    [ParticipantId] int NOT NULL IDENTITY,
    [OwnerUserId] nvarchar(450) NOT NULL,
    [LinkedUserId] nvarchar(450) NULL,
    [Relationship] varchar(30) NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [DateOfBirth] date NULL,
    [Gender] varchar(30) NULL,
    [Nationality] nvarchar(60) NULL,
    [IdType] varchar(30) NULL,
    [IdNumber] varchar(20) NULL,
    [Phone] varchar(20) NULL,
    [TShirtSize] varchar(30) NULL,
    [EmergencyContactName] nvarchar(100) NULL,
    [EmergencyContactPhone] varchar(20) NULL,
    [BloodType] varchar(5) NULL,
    [MedicalNotes] nvarchar(500) NULL,
    [ClubName] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    CONSTRAINT [PK_Participants] PRIMARY KEY ([ParticipantId]),
    CONSTRAINT [CK_Participants_IdNumber_Type] CHECK ([IdNumber] IS NULL OR [IdType] IS NOT NULL),
    CONSTRAINT [CK_Participants_Self_Linked] CHECK ([Relationship] = 'Self' OR [LinkedUserId] IS NULL),
    CONSTRAINT [FK_Participants_AspNetUsers_LinkedUserId] FOREIGN KEY ([LinkedUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Participants_AspNetUsers_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EventMedia] (
    [MediaId] int NOT NULL IDENTITY,
    [EventId] int NOT NULL,
    [FileUrl] nvarchar(500) NOT NULL,
    [Caption] nvarchar(300) NULL,
    [Kind] varchar(30) NOT NULL,
    [DisplayOrder] int NOT NULL,
    [IsVisible] bit NOT NULL,
    [UploadedById] nvarchar(450) NOT NULL,
    [UploadedAt] datetime2(3) NOT NULL,
    CONSTRAINT [PK_EventMedia] PRIMARY KEY ([MediaId]),
    CONSTRAINT [FK_EventMedia_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId]) ON DELETE CASCADE
);
GO

CREATE TABLE [EventPolicies] (
    [EventId] int NOT NULL,
    [AgeCalculationMode] varchar(30) NOT NULL,
    [GuardianConsentUnderAge] int NOT NULL,
    [MaxDependentsPerAccount] int NOT NULL,
    [InfoEditDeadline] datetime2(3) NULL,
    [TransferPolicy] varchar(30) NOT NULL,
    [TransferFee] decimal(12,0) NOT NULL,
    [MaxTransfers] int NOT NULL,
    [TransferDeadline] datetime2(3) NULL,
    [CategoryChangeFee] decimal(12,0) NOT NULL,
    [MaxCategoryChanges] int NOT NULL,
    [CategoryChangeDeadline] datetime2(3) NULL,
    [LockSpecialEntries] bit NOT NULL,
    [OverallRankingBasis] varchar(30) NULL,
    [AgeGroupRankingBasis] varchar(30) NULL,
    [TieBreakRule] varchar(30) NOT NULL,
    [LateStartToleranceMinutes] int NULL,
    [ComplaintWindowMinutes] int NOT NULL,
    [ComplaintFee] decimal(12,0) NOT NULL,
    [AwardStackingPolicy] varchar(30) NOT NULL,
    [ForceMajeurePolicy] varchar(30) NOT NULL,
    CONSTRAINT [PK_EventPolicies] PRIMARY KEY ([EventId]),
    CONSTRAINT [CK_EventPolicies_ComplaintWindow] CHECK ([ComplaintWindowMinutes] BETWEEN 10 AND 20160),
    CONSTRAINT [CK_EventPolicies_Fees] CHECK ([TransferFee] >= 0 AND [CategoryChangeFee] >= 0 AND [ComplaintFee] >= 0),
    CONSTRAINT [CK_EventPolicies_GuardianAge] CHECK ([GuardianConsentUnderAge] BETWEEN 0 AND 21),
    CONSTRAINT [CK_EventPolicies_LateStart] CHECK ([LateStartToleranceMinutes] IS NULL OR [LateStartToleranceMinutes] > 0),
    CONSTRAINT [CK_EventPolicies_MaxChanges] CHECK ([MaxTransfers] >= 0 AND [MaxCategoryChanges] >= 0),
    CONSTRAINT [CK_EventPolicies_MaxDependents] CHECK ([MaxDependentsPerAccount] BETWEEN 0 AND 10),
    CONSTRAINT [CK_EventPolicies_PaidTransfer] CHECK ([TransferPolicy] <> 'Paid' OR [TransferFee] > 0),
    CONSTRAINT [FK_EventPolicies_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId]) ON DELETE CASCADE
);
GO

CREATE TABLE [RaceCategories] (
    [CategoryId] int NOT NULL IDENTITY,
    [EventId] int NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Discipline] varchar(30) NOT NULL,
    [CategoryType] varchar(30) NOT NULL,
    [DistanceKm] decimal(6,2) NOT NULL,
    [ActualDistanceKm] decimal(6,2) NULL,
    [ElevationGainM] int NULL,
    [ItraPoints] tinyint NULL,
    [Capacity] int NOT NULL,
    [StartAt] datetime2(3) NOT NULL,
    [CutOffMinutes] int NULL,
    [MinAge] int NOT NULL,
    [MaxAge] int NULL,
    [EscortRequiredUnderAge] int NULL,
    [IsTimed] bit NOT NULL,
    [IsRanked] bit NOT NULL,
    [BibFrom] int NOT NULL,
    [BibTo] int NOT NULL,
    [BibColor] nvarchar(20) NULL,
    [MedalImageUrl] nvarchar(500) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_RaceCategories] PRIMARY KEY ([CategoryId]),
    CONSTRAINT [CK_RaceCategories_Age] CHECK ([MinAge] BETWEEN 0 AND 100 AND ([MaxAge] IS NULL OR [MaxAge] >= [MinAge])),
    CONSTRAINT [CK_RaceCategories_Bib] CHECK ([BibFrom] > 0 AND [BibFrom] < [BibTo]),
    CONSTRAINT [CK_RaceCategories_Capacity] CHECK ([Capacity] > 0),
    CONSTRAINT [CK_RaceCategories_CutOff] CHECK ([IsTimed] = 0 OR [CutOffMinutes] > 0),
    CONSTRAINT [CK_RaceCategories_Distance] CHECK ([DistanceKm] > 0 AND ([ActualDistanceKm] IS NULL OR [ActualDistanceKm] > 0)),
    CONSTRAINT [CK_RaceCategories_Elevation] CHECK ([ElevationGainM] IS NULL OR [ElevationGainM] >= 0),
    CONSTRAINT [CK_RaceCategories_Itra] CHECK ([ItraPoints] IS NULL OR [ItraPoints] BETWEEN 0 AND 6),
    CONSTRAINT [CK_RaceCategories_RankedNeedsTimed] CHECK ([IsRanked] = 0 OR [IsTimed] = 1),
    CONSTRAINT [FK_RaceCategories_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId]) ON DELETE CASCADE
);
GO

CREATE TABLE [AgeGroups] (
    [AgeGroupId] int NOT NULL IDENTITY,
    [EventId] int NOT NULL,
    [CategoryId] int NULL,
    [Name] nvarchar(50) NOT NULL,
    [MinAge] int NOT NULL,
    [MaxAge] int NULL,
    CONSTRAINT [PK_AgeGroups] PRIMARY KEY ([AgeGroupId]),
    CONSTRAINT [CK_AgeGroups_Range] CHECK ([MinAge] >= 0 AND ([MaxAge] IS NULL OR [MaxAge] >= [MinAge])),
    CONSTRAINT [FK_AgeGroups_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AgeGroups_RaceCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RaceCategories] ([CategoryId])
);
GO

CREATE TABLE [Checkpoints] (
    [CheckpointId] int NOT NULL IDENTITY,
    [CategoryId] int NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [DistanceKm] decimal(6,2) NOT NULL,
    [Sequence] int NOT NULL,
    [Type] varchar(30) NOT NULL,
    [IsMandatory] bit NOT NULL,
    [CutOffAt] datetime2(3) NULL,
    [StationType] varchar(30) NOT NULL,
    [Latitude] decimal(9,6) NULL,
    [Longitude] decimal(9,6) NULL,
    CONSTRAINT [PK_Checkpoints] PRIMARY KEY ([CheckpointId]),
    CONSTRAINT [CK_Checkpoints_Coordinates] CHECK (([Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90) AND ([Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180)),
    CONSTRAINT [CK_Checkpoints_Distance] CHECK ([DistanceKm] >= 0),
    CONSTRAINT [CK_Checkpoints_Sequence] CHECK ([Sequence] >= 1),
    CONSTRAINT [FK_Checkpoints_RaceCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RaceCategories] ([CategoryId]) ON DELETE CASCADE
);
GO

CREATE TABLE [PriceTiers] (
    [PriceTierId] int NOT NULL IDENTITY,
    [CategoryId] int NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [Price] decimal(12,0) NOT NULL,
    [ValidFrom] datetime2(3) NOT NULL,
    [ValidTo] datetime2(3) NOT NULL,
    CONSTRAINT [PK_PriceTiers] PRIMARY KEY ([PriceTierId]),
    CONSTRAINT [CK_PriceTiers_Price] CHECK ([Price] >= 0 AND [Price] % 1000 = 0),
    CONSTRAINT [CK_PriceTiers_Range] CHECK ([ValidFrom] < [ValidTo]),
    CONSTRAINT [FK_PriceTiers_RaceCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RaceCategories] ([CategoryId]) ON DELETE CASCADE
);
GO

CREATE TABLE [Registrations] (
    [RegistrationId] int NOT NULL IDENTITY,
    [EventId] int NOT NULL,
    [CategoryId] int NOT NULL,
    [ParticipantId] int NOT NULL,
    [RegisteredByUserId] nvarchar(450) NOT NULL,
    [EntryType] varchar(30) NOT NULL,
    [Status] varchar(30) NOT NULL,
    [BibNumber] int NULL,
    [OriginalPrice] decimal(12,0) NOT NULL,
    [DiscountAmount] decimal(12,0) NOT NULL,
    [CoinDiscountAmount] decimal(12,0) NOT NULL,
    [FinalPrice] decimal(12,0) NOT NULL,
    [CouponId] int NULL,
    [QrToken] varchar(64) NOT NULL,
    [RegisteredAt] datetime2(3) NOT NULL,
    [ExpiresAt] datetime2(3) NULL,
    [ConfirmedAt] datetime2(3) NULL,
    [CancelledAt] datetime2(3) NULL,
    [ChangeCount] int NOT NULL,
    [TransferCount] int NOT NULL,
    [HideFromPublicList] bit NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [BibName] nvarchar(16) NOT NULL,
    [DateOfBirth] date NOT NULL,
    [Gender] varchar(30) NOT NULL,
    [IdType] varchar(30) NULL,
    [IdNumber] varchar(20) NULL,
    [Nationality] nvarchar(60) NOT NULL,
    [Phone] varchar(20) NOT NULL,
    [Email] nvarchar(256) NULL,
    [TShirtSize] varchar(30) NOT NULL,
    [EmergencyContactName] nvarchar(100) NOT NULL,
    [EmergencyContactPhone] varchar(20) NOT NULL,
    [BloodType] varchar(5) NULL,
    [MedicalNotes] nvarchar(500) NULL,
    [ClubName] nvarchar(100) NULL,
    [GuardianName] nvarchar(100) NULL,
    [GuardianPhone] varchar(20) NULL,
    [GuardianIdNumber] varchar(20) NULL,
    [GuardianConsent] bit NOT NULL,
    [EscortName] nvarchar(100) NULL,
    [EscortPhone] varchar(20) NULL,
    [WaiverAccepted] bit NOT NULL,
    [HealthDeclarationAccepted] bit NOT NULL,
    CONSTRAINT [PK_Registrations] PRIMARY KEY ([RegistrationId]),
    CONSTRAINT [CK_Registrations_Bib] CHECK ([BibNumber] IS NULL OR [BibNumber] > 0),
    CONSTRAINT [CK_Registrations_BibOnlyConfirmed] CHECK ([BibNumber] IS NULL OR [Status] IN ('Confirmed','Transferred')),
    CONSTRAINT [CK_Registrations_Consents] CHECK ([WaiverAccepted] = 1 AND [HealthDeclarationAccepted] = 1),
    CONSTRAINT [CK_Registrations_Counts] CHECK ([ChangeCount] >= 0 AND [TransferCount] >= 0),
    CONSTRAINT [CK_Registrations_IdNumber_Type] CHECK ([IdNumber] IS NULL OR [IdType] IS NOT NULL),
    CONSTRAINT [CK_Registrations_Prices] CHECK ([OriginalPrice] >= 0 AND [DiscountAmount] >= 0 AND [CoinDiscountAmount] >= 0 AND [FinalPrice] >= 0 AND [FinalPrice] = [OriginalPrice] - [DiscountAmount] - [CoinDiscountAmount]),
    CONSTRAINT [FK_Registrations_AspNetUsers_RegisteredByUserId] FOREIGN KEY ([RegisteredByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Registrations_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Registrations_Participants_ParticipantId] FOREIGN KEY ([ParticipantId]) REFERENCES [Participants] ([ParticipantId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Registrations_RaceCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RaceCategories] ([CategoryId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ConsentRecords] (
    [ConsentId] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [RegistrationId] int NULL,
    [ConsentType] varchar(30) NOT NULL,
    [DocumentVersion] int NOT NULL,
    [Granted] bit NOT NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    [WithdrawnAt] datetime2(3) NULL,
    CONSTRAINT [PK_ConsentRecords] PRIMARY KEY ([ConsentId]),
    CONSTRAINT [CK_ConsentRecords_Version] CHECK ([DocumentVersion] >= 1),
    CONSTRAINT [FK_ConsentRecords_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ConsentRecords_Registrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [Registrations] ([RegistrationId])
);
GO

CREATE TABLE [Payments] (
    [PaymentId] int NOT NULL IDENTITY,
    [RegistrationId] int NOT NULL,
    [Purpose] varchar(30) NOT NULL,
    [Provider] varchar(30) NOT NULL,
    [Amount] decimal(12,0) NOT NULL,
    [Status] varchar(30) NOT NULL,
    [TxnRef] varchar(50) NOT NULL,
    [ProviderTransactionNo] varchar(50) NULL,
    [ResponseCode] varchar(10) NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    [PaidAt] datetime2(3) NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
    CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [FK_Payments_Registrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [Registrations] ([RegistrationId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [RaceKitPickups] (
    [PickupId] int NOT NULL IDENTITY,
    [RegistrationId] int NOT NULL,
    [PickedUpAt] datetime2(3) NOT NULL,
    [StaffId] nvarchar(450) NOT NULL,
    [IdChecked] bit NOT NULL,
    [IsProxy] bit NOT NULL,
    [ProxyName] nvarchar(100) NULL,
    [ProxyIdNumber] varchar(20) NULL,
    [Note] nvarchar(500) NULL,
    CONSTRAINT [PK_RaceKitPickups] PRIMARY KEY ([PickupId]),
    CONSTRAINT [CK_RaceKitPickups_Proxy] CHECK ([IsProxy] = 0 OR ([ProxyName] IS NOT NULL AND [ProxyIdNumber] IS NOT NULL)),
    CONSTRAINT [FK_RaceKitPickups_AspNetUsers_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RaceKitPickups_Registrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [Registrations] ([RegistrationId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Results] (
    [ResultId] int NOT NULL IDENTITY,
    [RegistrationId] int NOT NULL,
    [GunTimeMs] bigint NULL,
    [ChipTimeMs] bigint NULL,
    [PenaltySeconds] int NOT NULL,
    [FinalTimeMs] bigint NULL,
    [Status] varchar(30) NOT NULL,
    [DnfReason] varchar(30) NULL,
    [DnfCheckpointId] int NULL,
    [OverallRank] int NULL,
    [GenderRank] int NULL,
    [AgeGroupId] int NULL,
    [AgeGroupRank] int NULL,
    [UpdatedAt] datetime2(3) NOT NULL,
    CONSTRAINT [PK_Results] PRIMARY KEY ([ResultId]),
    CONSTRAINT [CK_Results_ChipLeGun] CHECK ([ChipTimeMs] IS NULL OR [GunTimeMs] IS NULL OR [ChipTimeMs] <= [GunTimeMs]),
    CONSTRAINT [CK_Results_DnfReason] CHECK ([Status] <> 'DNF' OR [DnfReason] IS NOT NULL),
    CONSTRAINT [CK_Results_Penalty] CHECK ([PenaltySeconds] >= 0),
    CONSTRAINT [CK_Results_Ranks] CHECK (([OverallRank] IS NULL OR [OverallRank] >= 1) AND ([GenderRank] IS NULL OR [GenderRank] >= 1) AND ([AgeGroupRank] IS NULL OR [AgeGroupRank] >= 1)),
    CONSTRAINT [FK_Results_AgeGroups_AgeGroupId] FOREIGN KEY ([AgeGroupId]) REFERENCES [AgeGroups] ([AgeGroupId]),
    CONSTRAINT [FK_Results_Checkpoints_DnfCheckpointId] FOREIGN KEY ([DnfCheckpointId]) REFERENCES [Checkpoints] ([CheckpointId]),
    CONSTRAINT [FK_Results_Registrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [Registrations] ([RegistrationId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TimingRecords] (
    [TimingRecordId] int NOT NULL IDENTITY,
    [RegistrationId] int NOT NULL,
    [CheckpointId] int NOT NULL,
    [RecordedAt] datetime2(3) NOT NULL,
    [Source] varchar(30) NOT NULL,
    [RecordedById] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_TimingRecords] PRIMARY KEY ([TimingRecordId]),
    CONSTRAINT [FK_TimingRecords_Checkpoints_CheckpointId] FOREIGN KEY ([CheckpointId]) REFERENCES [Checkpoints] ([CheckpointId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TimingRecords_Registrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [Registrations] ([RegistrationId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ResultPenalties] (
    [PenaltyId] int NOT NULL IDENTITY,
    [ResultId] int NOT NULL,
    [ReasonCode] varchar(40) NOT NULL,
    [Action] varchar(30) NOT NULL,
    [Minutes] int NULL,
    [CheckpointId] int NULL,
    [Note] nvarchar(500) NULL,
    [IssuedById] nvarchar(450) NOT NULL,
    [IssuedAt] datetime2(3) NOT NULL,
    [RevokedAt] datetime2(3) NULL,
    CONSTRAINT [PK_ResultPenalties] PRIMARY KEY ([PenaltyId]),
    CONSTRAINT [CK_ResultPenalties_Minutes] CHECK ([Action] <> 'TimePenalty' OR [Minutes] > 0),
    CONSTRAINT [FK_ResultPenalties_PenaltyReasons_ReasonCode] FOREIGN KEY ([ReasonCode]) REFERENCES [PenaltyReasons] ([Code]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ResultPenalties_Results_ResultId] FOREIGN KEY ([ResultId]) REFERENCES [Results] ([ResultId]) ON DELETE CASCADE
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
    SET IDENTITY_INSERT [AspNetRoles] ON;
INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
VALUES (N'0b8f0e6c-1a11-4c6e-9d01-000000000001', N'0b8f0e6c-1a11-4c6e-9d01-000000000001', N'SuperAdmin', N'SUPERADMIN'),
(N'0b8f0e6c-1a11-4c6e-9d01-000000000002', N'0b8f0e6c-1a11-4c6e-9d01-000000000002', N'Admin', N'ADMIN'),
(N'0b8f0e6c-1a11-4c6e-9d01-000000000003', N'0b8f0e6c-1a11-4c6e-9d01-000000000003', N'Organizer', N'ORGANIZER'),
(N'0b8f0e6c-1a11-4c6e-9d01-000000000004', N'0b8f0e6c-1a11-4c6e-9d01-000000000004', N'Staff', N'STAFF'),
(N'0b8f0e6c-1a11-4c6e-9d01-000000000005', N'0b8f0e6c-1a11-4c6e-9d01-000000000005', N'Member', N'MEMBER');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
    SET IDENTITY_INSERT [AspNetRoles] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'DocumentId', N'Content', N'DocType', N'PublishedAt', N'PublishedById', N'Version') AND [object_id] = OBJECT_ID(N'[LegalDocuments]'))
    SET IDENTITY_INSERT [LegalDocuments] ON;
INSERT INTO [LegalDocuments] ([DocumentId], [Content], [DocType], [PublishedAt], [PublishedById], [Version])
VALUES (1, N'<h2>Điều khoản sử dụng</h2><p>Phiên bản 1 – nội dung mẫu. SuperAdmin cập nhật tại trang quản trị.</p>', 'Terms', '2026-10-15T00:00:00.000', NULL, 1),
(2, N'<h2>Chính sách quyền riêng tư</h2><p>Phiên bản 1 – nội dung mẫu, căn cứ Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 và Nghị định 356/2025/NĐ-CP.</p>', 'Privacy', '2026-10-15T00:00:00.000', NULL, 1);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'DocumentId', N'Content', N'DocType', N'PublishedAt', N'PublishedById', N'Version') AND [object_id] = OBJECT_ID(N'[LegalDocuments]'))
    SET IDENTITY_INSERT [LegalDocuments] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'DefaultAction', N'DefaultMinutes', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[PenaltyReasons]'))
    SET IDENTITY_INSERT [PenaltyReasons] ON;
INSERT INTO [PenaltyReasons] ([Code], [DefaultAction], [DefaultMinutes], [IsActive], [Name])
VALUES ('BIB_NOT_VISIBLE', 'Warning', NULL, CAST(1 AS bit), N'Không đeo BIB đúng quy định'),
('BIB_TRANSFER', 'DSQ', NULL, CAST(1 AS bit), N'Chuyển nhượng hoặc dùng BIB trái phép'),
('FALSE_INFO', 'DSQ', NULL, CAST(1 AS bit), N'Khai sai thông tin cá nhân (tuổi, giới tính…)'),
('LITTERING', 'TimePenalty', 15, CAST(1 AS bit), N'Xả rác trên cung đường'),
('MISCONDUCT', 'DSQ', NULL, CAST(1 AS bit), N'Hành vi phi thể thao'),
('MISSED_CHECKPOINT', 'DSQ', NULL, CAST(1 AS bit), N'Không qua checkpoint bắt buộc'),
('MISSING_GEAR', 'TimePenalty', 30, CAST(1 AS bit), N'Thiếu vật dụng bắt buộc'),
('MULTIPLE_BIB', 'DSQ', NULL, CAST(1 AS bit), N'Đeo nhiều BIB / chạy hộ'),
('OTHER', 'Warning', NULL, CAST(1 AS bit), N'Lý do khác (ghi rõ trong ghi chú)'),
('PRO_IN_AGEGROUP', 'DSQ', NULL, CAST(1 AS bit), N'VĐV chuyên nghiệp đăng ký nhóm tuổi phong trào'),
('REFUSE_MEDICAL', 'DSQ', NULL, CAST(1 AS bit), N'Không tuân thủ yêu cầu của đội y tế'),
('SHORTCUT', 'DSQ', NULL, CAST(1 AS bit), N'Đi tắt, không chạy đúng cung đường'),
('UNAUTHORISED_ASSIST', 'DSQ', NULL, CAST(1 AS bit), N'Nhận hỗ trợ trái phép (pacer, xe, người ngoài)'),
('UNDERAGE', 'DSQ', NULL, CAST(1 AS bit), N'Không đủ điều kiện tuổi'),
('WHEELED_DEVICE', 'DSQ', NULL, CAST(1 AS bit), N'Dùng phương tiện có bánh xe');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'DefaultAction', N'DefaultMinutes', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[PenaltyReasons]'))
    SET IDENTITY_INSERT [PenaltyReasons] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'DataType', N'Description', N'UpdatedAt', N'UpdatedById', N'Value') AND [object_id] = OBJECT_ID(N'[SystemSettings]'))
    SET IDENTITY_INSERT [SystemSettings] ON;
INSERT INTO [SystemSettings] ([Key], [DataType], [Description], [UpdatedAt], [UpdatedById], [Value])
VALUES ('Fee.Max', 'Decimal', N'Phí chuyển nhượng/đổi cự ly/khiếu nại tối đa (VND) – BR-48', NULL, NULL, N'1000000'),
('Media.MaxFileSizeMB', 'Int', N'Dung lượng ảnh tối đa – BR-51', NULL, NULL, N'5'),
('Media.MaxPerEvent', 'Int', N'Số ảnh tối đa mỗi giải – BR-51', NULL, NULL, N'200'),
('Payment.HoldMinutes.Mock', 'Int', N'Thời gian giữ chỗ khi thanh toán Mock (phút) – BR-13', NULL, NULL, N'15'),
('Payment.HoldMinutes.VnPay', 'Int', N'Thời gian giữ chỗ khi thanh toán VNPay (phút) – BR-13', NULL, NULL, N'15'),
('Payment.MockEnabled', 'Bool', N'Bật thanh toán giả lập', NULL, NULL, N'true'),
('Payment.VnPayEnabled', 'Bool', N'Bật VNPay sandbox', NULL, NULL, N'false'),
('Platform.FeePercentDefault', 'Decimal', N'Phí nền tảng mặc định (%)', NULL, NULL, N'0'),
('PolicyDefault.ComplaintWindowMinutes', 'Int', N'Mặc định chính sách giải mới', NULL, NULL, N'1440'),
('PolicyDefault.GuardianConsentUnderAge', 'Int', N'Mặc định chính sách giải mới', NULL, NULL, N'18'),
('PolicyDefault.MaxDependentsPerAccount', 'Int', N'Mặc định chính sách giải mới', NULL, NULL, N'5'),
('PolicyDefault.TransferPolicy', 'String', N'Mặc định chính sách giải mới', NULL, NULL, N'Forbidden'),
('Price.Max', 'Decimal', N'Giá tối đa (VND) – BR-48', NULL, NULL, N'20000000'),
('Price.MinNonZero', 'Decimal', N'Giá tối thiểu nếu khác 0 (VND) – BR-48', NULL, NULL, N'10000'),
('Privacy.MedicalRetentionDays', 'Int', N'Số ngày giữ dữ liệu y tế sau khi kết quả chính thức – BR-52', NULL, NULL, N'30');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'DataType', N'Description', N'UpdatedAt', N'UpdatedById', N'Value') AND [object_id] = OBJECT_ID(N'[SystemSettings]'))
    SET IDENTITY_INSERT [SystemSettings] OFF;
GO

CREATE INDEX [IX_AgeGroups_CategoryId] ON [AgeGroups] ([CategoryId]);
GO

CREATE INDEX [IX_AgeGroups_EventId] ON [AgeGroups] ([EventId]);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO

CREATE UNIQUE INDEX [IX_AspNetUsers_Email] ON [AspNetUsers] ([Email]) WHERE [Email] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUsers_OrganizationId] ON [AspNetUsers] ([OrganizationId]);
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);
GO

CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [AuditLogs] ([EntityType], [EntityId]);
GO

CREATE UNIQUE INDEX [IX_Checkpoints_CategoryId_Sequence] ON [Checkpoints] ([CategoryId], [Sequence]);
GO

CREATE INDEX [IX_ConsentRecords_RegistrationId] ON [ConsentRecords] ([RegistrationId]);
GO

CREATE INDEX [IX_ConsentRecords_UserId_ConsentType] ON [ConsentRecords] ([UserId], [ConsentType]);
GO

CREATE INDEX [IX_EventMedia_EventId_Kind_DisplayOrder] ON [EventMedia] ([EventId], [Kind], [DisplayOrder]);
GO

CREATE INDEX [IX_Events_OrganizationId] ON [Events] ([OrganizationId]);
GO

CREATE UNIQUE INDEX [IX_Events_Slug] ON [Events] ([Slug]);
GO

CREATE INDEX [IX_Events_Status_EventDate] ON [Events] ([Status], [EventDate]);
GO

CREATE INDEX [IX_Events_VenueId] ON [Events] ([VenueId]);
GO

CREATE UNIQUE INDEX [IX_LegalDocuments_DocType_Version] ON [LegalDocuments] ([DocType], [Version]);
GO

CREATE INDEX [IX_OrganizationApplications_OrganizationId] ON [OrganizationApplications] ([OrganizationId]);
GO

CREATE INDEX [IX_OrganizationApplications_ReviewedById] ON [OrganizationApplications] ([ReviewedById]);
GO

CREATE UNIQUE INDEX [UX_OrgApplications_Applicant_Pending] ON [OrganizationApplications] ([ApplicantId]) WHERE [Status] = 'Pending';
GO

CREATE UNIQUE INDEX [IX_Organizations_Name] ON [Organizations] ([Name]);
GO

CREATE UNIQUE INDEX [IX_Organizations_Slug] ON [Organizations] ([Slug]);
GO

CREATE UNIQUE INDEX [IX_Organizations_TaxCode] ON [Organizations] ([TaxCode]);
GO

CREATE INDEX [IX_Participants_LinkedUserId] ON [Participants] ([LinkedUserId]);
GO

CREATE UNIQUE INDEX [IX_Participants_OwnerUserId_IdType_IdNumber] ON [Participants] ([OwnerUserId], [IdType], [IdNumber]) WHERE [IdNumber] IS NOT NULL AND [IsDeleted] = 0;
GO

CREATE UNIQUE INDEX [UX_Participants_Owner_Self] ON [Participants] ([OwnerUserId]) WHERE [Relationship] = 'Self' AND [IsDeleted] = 0;
GO

CREATE INDEX [IX_Payments_RegistrationId] ON [Payments] ([RegistrationId]);
GO

CREATE UNIQUE INDEX [IX_Payments_TxnRef] ON [Payments] ([TxnRef]);
GO

CREATE INDEX [IX_PriceTiers_CategoryId_ValidFrom] ON [PriceTiers] ([CategoryId], [ValidFrom]);
GO

CREATE UNIQUE INDEX [IX_RaceCategories_EventId_Name] ON [RaceCategories] ([EventId], [Name]);
GO

CREATE UNIQUE INDEX [IX_RaceKitPickups_RegistrationId] ON [RaceKitPickups] ([RegistrationId]);
GO

CREATE INDEX [IX_RaceKitPickups_StaffId] ON [RaceKitPickups] ([StaffId]);
GO

CREATE INDEX [IX_Registrations_CategoryId_Status] ON [Registrations] ([CategoryId], [Status]);
GO

CREATE INDEX [IX_Registrations_ParticipantId] ON [Registrations] ([ParticipantId]);
GO

CREATE UNIQUE INDEX [IX_Registrations_QrToken] ON [Registrations] ([QrToken]);
GO

CREATE INDEX [IX_Registrations_RegisteredByUserId] ON [Registrations] ([RegisteredByUserId]);
GO

CREATE INDEX [IX_Registrations_Status_ExpiresAt] ON [Registrations] ([Status], [ExpiresAt]);
GO

CREATE UNIQUE INDEX [UX_Registrations_Event_Bib] ON [Registrations] ([EventId], [BibNumber]) WHERE [BibNumber] IS NOT NULL;
GO

CREATE UNIQUE INDEX [UX_Registrations_Event_IdNumber_Active] ON [Registrations] ([EventId], [IdType], [IdNumber]) WHERE [IdNumber] IS NOT NULL AND [Status] IN ('PendingPayment','Confirmed');
GO

CREATE UNIQUE INDEX [UX_Registrations_Event_Participant_Active] ON [Registrations] ([EventId], [ParticipantId]) WHERE [Status] IN ('PendingPayment','Confirmed');
GO

CREATE INDEX [IX_ResultPenalties_ReasonCode] ON [ResultPenalties] ([ReasonCode]);
GO

CREATE INDEX [IX_ResultPenalties_ResultId] ON [ResultPenalties] ([ResultId]);
GO

CREATE INDEX [IX_Results_AgeGroupId] ON [Results] ([AgeGroupId]);
GO

CREATE INDEX [IX_Results_DnfCheckpointId] ON [Results] ([DnfCheckpointId]);
GO

CREATE UNIQUE INDEX [IX_Results_RegistrationId] ON [Results] ([RegistrationId]);
GO

CREATE INDEX [IX_TimingRecords_CheckpointId] ON [TimingRecords] ([CheckpointId]);
GO

CREATE UNIQUE INDEX [IX_TimingRecords_RegistrationId_CheckpointId] ON [TimingRecords] ([RegistrationId], [CheckpointId]);
GO

CREATE UNIQUE INDEX [IX_Venues_Name_City] ON [Venues] ([Name], [City]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261015131026_InitialCreate', N'8.0.32');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Events] ADD [ThemeColor] varchar(7) NULL;
GO

CREATE TABLE [BibTransfers] (
    [TransferId] int NOT NULL IDENTITY,
    [RegistrationId] int NOT NULL,
    [FromUserId] nvarchar(450) NOT NULL,
    [ToUserId] nvarchar(450) NOT NULL,
    [ToParticipantId] int NULL,
    [Status] varchar(30) NOT NULL,
    [FeeAmount] decimal(12,0) NOT NULL,
    [NewRegistrationId] int NULL,
    [RequestedAt] datetime2(3) NOT NULL,
    [RespondedAt] datetime2(3) NULL,
    CONSTRAINT [PK_BibTransfers] PRIMARY KEY ([TransferId]),
    CONSTRAINT [CK_BibTransfers_Fee] CHECK ([FeeAmount] >= 0),
    CONSTRAINT [CK_BibTransfers_Users] CHECK ([FromUserId] <> [ToUserId]),
    CONSTRAINT [FK_BibTransfers_Registrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [Registrations] ([RegistrationId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Coupons] (
    [CouponId] int NOT NULL IDENTITY,
    [OrganizationId] int NULL,
    [EventId] int NULL,
    [CategoryId] int NULL,
    [OwnerMemberId] nvarchar(450) NULL,
    [Code] varchar(30) NOT NULL,
    [DiscountType] varchar(30) NOT NULL,
    [DiscountValue] decimal(12,0) NOT NULL,
    [MaxDiscountAmount] decimal(12,0) NULL,
    [MaxUses] int NOT NULL,
    [UsedCount] int NOT NULL,
    [ValidFrom] datetime2(3) NOT NULL,
    [ValidTo] datetime2(3) NOT NULL,
    [PartnerName] nvarchar(200) NULL,
    [IsActive] bit NOT NULL,
    [CreatedById] nvarchar(450) NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Coupons] PRIMARY KEY ([CouponId]),
    CONSTRAINT [CK_Coupons_CategoryNeedsEvent] CHECK ([CategoryId] IS NULL OR [EventId] IS NOT NULL),
    CONSTRAINT [CK_Coupons_MaxDiscount] CHECK ([MaxDiscountAmount] IS NULL OR ([MaxDiscountAmount] > 0 AND [MaxDiscountAmount] % 1000 = 0)),
    CONSTRAINT [CK_Coupons_Range] CHECK ([ValidFrom] < [ValidTo]),
    CONSTRAINT [CK_Coupons_Uses] CHECK ([MaxUses] >= 1 AND [UsedCount] >= 0 AND [UsedCount] <= [MaxUses]),
    CONSTRAINT [CK_Coupons_Value] CHECK (([DiscountType] = 'Percent' AND [DiscountValue] BETWEEN 1 AND 100) OR ([DiscountType] = 'Amount' AND [DiscountValue] > 0 AND [DiscountValue] % 1000 = 0)),
    CONSTRAINT [FK_Coupons_AspNetUsers_OwnerMemberId] FOREIGN KEY ([OwnerMemberId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Coupons_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId]),
    CONSTRAINT [FK_Coupons_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([OrganizationId]),
    CONSTRAINT [FK_Coupons_RaceCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RaceCategories] ([CategoryId])
);
GO

CREATE TABLE [EventContentSections] (
    [SectionId] int NOT NULL IDENTITY,
    [EventId] int NOT NULL,
    [SectionType] varchar(30) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [BodyHtml] nvarchar(max) NULL,
    [DisplayOrder] int NOT NULL,
    [IsVisible] bit NOT NULL,
    CONSTRAINT [PK_EventContentSections] PRIMARY KEY ([SectionId]),
    CONSTRAINT [CK_EventContentSections_Order] CHECK ([DisplayOrder] >= 0),
    CONSTRAINT [FK_EventContentSections_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId]) ON DELETE CASCADE
);
GO

CREATE TABLE [EventScheduleItems] (
    [ItemId] int NOT NULL IDENTITY,
    [EventId] int NOT NULL,
    [StartsAt] datetime2(3) NOT NULL,
    [EndsAt] datetime2(3) NULL,
    [Title] nvarchar(200) NOT NULL,
    [Location] nvarchar(200) NULL,
    [Note] nvarchar(500) NULL,
    CONSTRAINT [PK_EventScheduleItems] PRIMARY KEY ([ItemId]),
    CONSTRAINT [CK_EventScheduleItems_Range] CHECK ([EndsAt] IS NULL OR [EndsAt] > [StartsAt]),
    CONSTRAINT [FK_EventScheduleItems_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([EventId]) ON DELETE CASCADE
);
GO

CREATE TABLE [Prizes] (
    [PrizeId] int NOT NULL IDENTITY,
    [CategoryId] int NOT NULL,
    [AwardType] varchar(30) NOT NULL,
    [Gender] varchar(30) NOT NULL,
    [AgeGroupId] int NULL,
    [Nationality] nvarchar(60) NULL,
    [Rank] int NOT NULL,
    [Reward] nvarchar(300) NOT NULL,
    [CashAmount] decimal(12,0) NULL,
    CONSTRAINT [PK_Prizes] PRIMARY KEY ([PrizeId]),
    CONSTRAINT [CK_Prizes_AgeGroup] CHECK (([AwardType] = 'AgeGroup' AND [AgeGroupId] IS NOT NULL) OR ([AwardType] <> 'AgeGroup' AND [AgeGroupId] IS NULL)),
    CONSTRAINT [CK_Prizes_Cash] CHECK ([CashAmount] IS NULL OR [CashAmount] >= 0),
    CONSTRAINT [CK_Prizes_Rank] CHECK ([Rank] >= 1),
    CONSTRAINT [FK_Prizes_AgeGroups_AgeGroupId] FOREIGN KEY ([AgeGroupId]) REFERENCES [AgeGroups] ([AgeGroupId]),
    CONSTRAINT [FK_Prizes_RaceCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RaceCategories] ([CategoryId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [RaceRoutes] (
    [RouteId] int NOT NULL IDENTITY,
    [CategoryId] int NOT NULL,
    [RouteGeoJson] nvarchar(max) NOT NULL,
    [CalculatedDistanceKm] decimal(7,3) NOT NULL,
    [Source] varchar(30) NOT NULL,
    [UpdatedById] nvarchar(450) NULL,
    [UpdatedAt] datetime2(3) NOT NULL,
    CONSTRAINT [PK_RaceRoutes] PRIMARY KEY ([RouteId]),
    CONSTRAINT [CK_RaceRoutes_Distance] CHECK ([CalculatedDistanceKm] > 0),
    CONSTRAINT [CK_RaceRoutes_Json] CHECK (ISJSON([RouteGeoJson]) = 1),
    CONSTRAINT [FK_RaceRoutes_RaceCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RaceCategories] ([CategoryId]) ON DELETE CASCADE
);
GO

CREATE TABLE [Refunds] (
    [RefundId] int NOT NULL IDENTITY,
    [RegistrationId] int NOT NULL,
    [Amount] decimal(12,0) NOT NULL,
    [Reason] varchar(30) NOT NULL,
    [Status] varchar(30) NOT NULL,
    [BankInfo] nvarchar(300) NULL,
    [ProcessedById] nvarchar(450) NULL,
    [ProcessedAt] datetime2(3) NULL,
    [TransactionRef] nvarchar(100) NULL,
    CONSTRAINT [PK_Refunds] PRIMARY KEY ([RefundId]),
    CONSTRAINT [CK_Refunds_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_Refunds_Completed] CHECK ([Status] <> 'Completed' OR [TransactionRef] IS NOT NULL),
    CONSTRAINT [FK_Refunds_Registrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [Registrations] ([RegistrationId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ResultComplaints] (
    [ComplaintId] int NOT NULL IDENTITY,
    [ResultId] int NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Content] nvarchar(2000) NOT NULL,
    [EvidenceUrl] nvarchar(500) NULL,
    [FeeAmount] decimal(12,0) NOT NULL,
    [Status] varchar(30) NOT NULL,
    [Response] nvarchar(2000) NULL,
    [HandledById] nvarchar(450) NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    [HandledAt] datetime2(3) NULL,
    CONSTRAINT [PK_ResultComplaints] PRIMARY KEY ([ComplaintId]),
    CONSTRAINT [CK_ResultComplaints_Fee] CHECK ([FeeAmount] >= 0),
    CONSTRAINT [CK_ResultComplaints_Response] CHECK ([Status] = 'Open' OR [Response] IS NOT NULL),
    CONSTRAINT [FK_ResultComplaints_Results_ResultId] FOREIGN KEY ([ResultId]) REFERENCES [Results] ([ResultId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [RoutePois] (
    [PoiId] int NOT NULL IDENTITY,
    [CategoryId] int NOT NULL,
    [Type] varchar(30) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Latitude] decimal(9,6) NOT NULL,
    [Longitude] decimal(9,6) NOT NULL,
    [DistanceFromStartKm] decimal(7,3) NULL,
    CONSTRAINT [PK_RoutePois] PRIMARY KEY ([PoiId]),
    CONSTRAINT [CK_RoutePois_Coordinates] CHECK ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180),
    CONSTRAINT [FK_RoutePois_RaceCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RaceCategories] ([CategoryId]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_Registrations_CouponId] ON [Registrations] ([CouponId]);
GO

ALTER TABLE [Events] ADD CONSTRAINT [CK_Events_ThemeColor] CHECK ([ThemeColor] IS NULL OR [ThemeColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]');
GO

CREATE INDEX [IX_BibTransfers_ToUserId_Status] ON [BibTransfers] ([ToUserId], [Status]);
GO

CREATE UNIQUE INDEX [UX_BibTransfers_Registration_Open] ON [BibTransfers] ([RegistrationId]) WHERE [Status] IN ('Pending','AwaitingFee');
GO

CREATE INDEX [IX_Coupons_CategoryId] ON [Coupons] ([CategoryId]);
GO

CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);
GO

CREATE INDEX [IX_Coupons_EventId] ON [Coupons] ([EventId]);
GO

CREATE INDEX [IX_Coupons_OrganizationId_IsActive] ON [Coupons] ([OrganizationId], [IsActive]);
GO

CREATE INDEX [IX_Coupons_OwnerMemberId] ON [Coupons] ([OwnerMemberId]);
GO

CREATE INDEX [IX_EventContentSections_EventId_DisplayOrder] ON [EventContentSections] ([EventId], [DisplayOrder]);
GO

CREATE INDEX [IX_EventScheduleItems_EventId_StartsAt] ON [EventScheduleItems] ([EventId], [StartsAt]);
GO

CREATE INDEX [IX_Prizes_AgeGroupId] ON [Prizes] ([AgeGroupId]);
GO

CREATE UNIQUE INDEX [IX_Prizes_CategoryId_AwardType_Gender_AgeGroupId_Rank] ON [Prizes] ([CategoryId], [AwardType], [Gender], [AgeGroupId], [Rank]);
GO

CREATE UNIQUE INDEX [IX_RaceRoutes_CategoryId] ON [RaceRoutes] ([CategoryId]);
GO

CREATE UNIQUE INDEX [IX_Refunds_RegistrationId] ON [Refunds] ([RegistrationId]);
GO

CREATE INDEX [IX_ResultComplaints_ResultId_Status] ON [ResultComplaints] ([ResultId], [Status]);
GO

CREATE INDEX [IX_RoutePois_CategoryId] ON [RoutePois] ([CategoryId]);
GO

ALTER TABLE [Registrations] ADD CONSTRAINT [FK_Registrations_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([CouponId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261026120052_Phase2Schema', N'8.0.32');
GO

COMMIT;
GO

SET NOEXEC OFF;
GO