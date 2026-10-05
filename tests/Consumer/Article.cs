namespace Consumer;

public sealed record Article(string Id, string Locale, string Slug, string Title, string Body, DateTimeOffset UpdatedAtUtc, bool Published);
