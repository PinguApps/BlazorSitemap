namespace Consumer;

public sealed class BlogStore
{
    private readonly object _gate = new();
    private readonly List<Article> _articles =
    [
        new("welcome", "en-gb", "welcome", "Welcome to Swansea", "A coastal city and a new beginning.", DateTimeOffset.Parse("2026-09-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture), true),
        new("welcome", "cy-gb", "croeso", "Croeso i Abertawe", "Dinas arfordirol a dechrau newydd.", DateTimeOffset.Parse("2026-09-02T11:00:00Z", System.Globalization.CultureInfo.InvariantCulture), true),
        new("coast", "en-gb", "the-coast", "The coast", "Walking beside Swansea Bay.", DateTimeOffset.Parse("2026-09-03T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture), true),
        new("draft", "en-gb", "unpublished", "Unpublished draft", "This must not appear in a sitemap.", DateTimeOffset.Parse("2026-09-04T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture), false)
    ];

    // Persist this independently of the remaining articles. A deletion is also a collection publication event.
    public DateTimeOffset CollectionPublishedAtUtc { get; private set; } = DateTimeOffset.Parse("2026-09-04T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    public Article[] Published()
    {
        lock (_gate)
        {
            return _articles.Where(x => x.Published).ToArray();
        }
    }

    public void Change(string operation)
    {
        lock (_gate)
        {
            DateTimeOffset publicationTime = DateTimeOffset.UtcNow; // Actual demo publication event, not sitemap generation time.
            switch (operation)
            {
                case "edit":
                    int index = _articles.FindIndex(x => x.Id == "welcome" && x.Locale == "en-gb");
                    if (_articles[index].Title == "Welcome to Swansea — edited") { return; }
                    _articles[index] = _articles[index] with { Title = "Welcome to Swansea — edited", UpdatedAtUtc = publicationTime };
                    break;
                case "add":
                    if (_articles.Any(x => x.Id == "new")) { return; }
                    _articles.Add(new("new", "en-gb", "new-article", "A new article", "Published without rebuilding.", publicationTime, true));
                    break;
                case "unpublish":
                    if (!_articles.Any(x => x.Id == "coast" && x.Published)) { return; }
                    for (int i = 0; i < _articles.Count; i++)
                    {
                        if (_articles[i].Id == "coast")
                        {
                            _articles[i] = _articles[i] with { Published = false };
                        }
                    }
                    break;
                case "delete":
                    if (_articles.RemoveAll(x => x.Id == "new") == 0) { return; }
                    break;
                default:
                    throw new ArgumentException("Use edit, add, unpublish or delete.", nameof(operation));
            }

            CollectionPublishedAtUtc = publicationTime;
        }
    }
}
