using System.Runtime.CompilerServices;
using Consumer.Components.Pages;
using PinguApps.BlazorSitemap;

namespace Consumer;

public sealed class BlogEntries(BlogStore store) : ISitemapEntryProvider<BlogPost>
{
    public async IAsyncEnumerable<SitemapEntry> GetEntriesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Replace with an ordered, paged database query that returns only published translations.
        foreach (Article article in store.Published())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new SitemapEntry(new Dictionary<string, string?> { ["locale"] = article.Locale, ["slug"] = article.Slug })
            {
                AlternateGroup = article.Id,
                LastModified = new SitemapLastModified(article.UpdatedAtUtc)
            };
        }

        await Task.CompletedTask;
    }
}
