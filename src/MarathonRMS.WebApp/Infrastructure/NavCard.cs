namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>Thẻ điều hướng trên trang chủ khu vực role. Page = null ⇒ chức năng chưa có (hiện mờ).</summary>
public record NavCard(string Title, string Description, string Icon, string? Page);
