namespace MarathonRMS.WebApp.Infrastructure;

public record RoleHomeModel(string Title, string Icon, string? OrganizationName, IReadOnlyList<string> Upcoming);
