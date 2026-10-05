using System.Runtime.CompilerServices;
using PinguApps.BlazorSitemap;

namespace Consumer;

public sealed class ApiArticleUrls(BlogStore store) : ISitemapUrlProvider
{
    public async IAsyncEnumerable<SitemapUrlEntry> GetEntriesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (Article article in store.Published().Where(x => x.Locale == "en-gb"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new SitemapUrlEntry("/api/articles/" + Uri.EscapeDataString(article.Slug), new SitemapLastModified(article.UpdatedAtUtc));
        }
        await Task.CompletedTask;
    }
}
