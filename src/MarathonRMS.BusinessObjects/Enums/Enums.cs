namespace MarathonRMS.BusinessObjects.Enums;

// Tất cả enum được lưu dạng chuỗi trong DB (HasConversion<string>) để CHECK constraint đọc được.

public enum Gender { M, F }

public enum TShirtSize { XS, S, M, L, XL, XXL }

public enum IdType { CCCD, Passport, BirthCertificate }

public enum ParticipantRelationship { Self, Child, Relative }

public enum ApplicationStatus { Pending, Approved, Rejected }

public enum EventStatus { Draft, Published, RegistrationOpen, RegistrationClosed, Postponed, Completed, Cancelled }

public enum ResultsStatus { Draft, Provisional, Official }

public enum Discipline { Road, Trail }

public enum CategoryType { Standard, Kids, Elite }

public enum CheckpointType { Start, Split, Finish }

public enum StationType { None, Water, Full, Medical }

public enum AgeCalculationMode { RaceDate, CalendarYear }

public enum TransferPolicy { Forbidden, Free, Paid }

public enum RankingBasis { GunTime, ChipTime }

public enum TieBreakRule { Shared, ChipTime }

public enum AwardStackingPolicy { AllowAll, HighestOnly }

public enum ForceMajeurePolicy { NoRefund, CarryOver, Refund }

public enum EntryType { Public, Partner, Complimentary, Pacer, Charity }

public enum RegistrationStatus { PendingPayment, Confirmed, Cancelled, Expired, Transferred, RefundPending, Refunded }

public enum PaymentPurpose { Registration, CategoryChange, TransferFee, ComplaintFee, AddOn }

public enum PaymentProvider { Mock, VnPay }

public enum PaymentStatus { Pending, Success, Failed, Expired }

public enum TimingSource { Manual, Import }

public enum ResultStatus { Finished, DNF, DNS, DSQ }

public enum DnfReason { CutOff, Voluntary, Medical, MissedCheckpoint }

public enum PenaltyAction { Warning, TimePenalty, DSQ }

public enum ConsentType { Terms, Privacy, Waiver, HealthData, PhotoMarketing, PublicListing, Guardian }

public enum LegalDocType { Terms, Privacy }

public enum SettingDataType { Int, Decimal, Bool, String }

public enum MediaKind { Inline, Gallery }

// ---------- Giai đoạn 2 ----------

public enum DiscountType { Percent, Amount }

public enum TransferStatus { Pending, AwaitingFee, Accepted, Rejected, Cancelled, Expired }

public enum ComplaintStatus { Open, Accepted, Rejected }

public enum RefundStatus { Pending, Completed, Rejected }

public enum RefundReason { EventCancelled, Other }

public enum AwardType { Overall, AgeGroup, Nationality }

public enum RouteSource { Drawn, Gpx }

public enum PoiType { Water, Medical, Toilet, Baggage, Other }

public enum SectionType { Intro, Schedule, Faq, Custom }
