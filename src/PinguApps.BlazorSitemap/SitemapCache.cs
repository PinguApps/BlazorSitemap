using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PinguApps.BlazorSitemap;

/// <summary>Caches consistent sitemap snapshots. Invalidate after committing a publication change.</summary>
public sealed partial class SitemapCache : IDisposable
{
    private readonly SitemapOptions _options;
    private readonly RouteCatalog _catalog;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<SitemapCache> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _version;
    private long _snapshotVersion = -1;
    private long _created;
    private SitemapSnapshot? _snapshot;

    internal SitemapCache(SitemapOptions options, RouteCatalog catalog, IServiceScopeFactory scopes, TimeProvider clock, ILogger<SitemapCache> logger)
    {
        _options = options;
        _catalog = catalog;
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Makes the next request regenerate. An invalidation during generation is preserved for the next request.</summary>
    public void Invalidate() => Interlocked.Increment(ref _version);

    /// <summary>Releases synchronization resources when the application shuts down.</summary>
    public void Dispose() => _gate.Dispose();

    internal async Task<SitemapSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            long version = Interlocked.Read(ref _version);
            if (_snapshot is not null && _snapshotVersion == version && _options.CacheLifetime > TimeSpan.Zero &&
                _clock.GetElapsedTime(_created) < _options.CacheLifetime)
            {
                return _snapshot;
            }

            SitemapSnapshot generated = await GenerateAsync(cancellationToken);
            _snapshot = generated;
            _snapshotVersion = version;
            _created = _clock.GetTimestamp();
            return generated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SitemapSnapshot> GenerateAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
        Dictionary<Type, FreshnessRegistration> freshnessSources = scope.ServiceProvider.GetServices<FreshnessRegistration>().ToDictionary(x => x.PageType);
        Dictionary<string, CanonicalEntry> entries = new(StringComparer.Ordinal);
        int missingFreshness = 0;
        foreach (PageRoute page in _catalog.Pages)
        {
            ISitemapLastModifiedProvider? freshness = freshnessSources.TryGetValue(page.PageType, out FreshnessRegistration? registration)
                ? registration.Resolve(scope.ServiceProvider) : null;
            bool dynamic = page.Source is not null || page.Attribute.DynamicContent;
            SitemapLastModified? sourceDate = !dynamic && _options.UseSourceLastModified ? SourceFreshness.Read(page.PageType, page.Attribute) : null;
            IAsyncEnumerable<SitemapEntry> source = page.Source is null ? FixedEntry() : page.Source.Read(scope.ServiceProvider, cancellationToken);
            await foreach (SitemapEntry entry in source.WithCancellation(cancellationToken))
            {
                foreach (Dictionary<string, string?> values in _catalog.Expand(page, entry))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Uri url = _catalog.Bind(page, values);
                    SitemapLastModified? lastModified = entry.LastModified;
                    if (lastModified is null && freshness is not null)
                    {
                        lastModified = await freshness.GetLastModifiedAsync(new SitemapPageContext(page.PageType, url, values), cancellationToken);
                    }

                    if (lastModified is null)
                    {
                        lastModified = sourceDate;

                        if (lastModified is null && (dynamic || _options.RequireLastModified))
                        {
                            throw new InvalidOperationException($"Missing trustworthy lastmod for {url.AbsoluteUri} ({page.PageType.Name}). Dynamic content requires a supplied timestamp; static content needs an embedded source-change stamp or a page-specific freshness provider. UseSourceLastModified and RequireLastModified control the static policy.");
                        }

                        if (lastModified is null) { missingFreshness++; }
                    }

                    string? group = null;
                    SitemapLocale? locale = null;
                    if (page.Attribute.Hreflang)
                    {
                        if (!values.TryGetValue(_options.LocaleParameter, out string? localeValue) || localeValue is null || !_options.LocaleValues.TryGetValue(localeValue, out locale))
                        {
                            throw new InvalidOperationException($"{page.PageType.Name}: missing or unregistered locale for hreflang.");
                        }

                        string identity = page.Source is null
                            ? JsonSerializer.Serialize(values.Where(x => !x.Key.Equals(_options.LocaleParameter, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                            : entry.AlternateGroup ?? throw new InvalidOperationException($"{page.PageType.Name}: localized provider entries require AlternateGroup to identify equivalent translations.");
                        if (string.IsNullOrWhiteSpace(identity))
                        {
                            throw new InvalidOperationException($"{page.PageType.Name}: AlternateGroup must be nonempty.");
                        }

                        group = JsonSerializer.Serialize(new[] { page.PageType.AssemblyQualifiedName, identity });
                    }

                    CanonicalEntry canonical = new(page.PageType, url, lastModified, group, locale);
                    AddEntry(canonical);
                }
            }
        }

        foreach (SitemapUrlRegistration registration in scope.ServiceProvider.GetServices<SitemapUrlRegistration>())
        {
            foreach (SitemapUrlEntry entry in registration.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddEntry(new CanonicalEntry(null, entry.Resolve(_catalog.BaseUrl), entry.LastModified, null, null));
            }
        }

        foreach (ISitemapUrlProvider provider in scope.ServiceProvider.GetServices<ISitemapUrlProvider>())
        {
            await foreach (SitemapUrlEntry entry in provider.GetEntriesAsync(cancellationToken).WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddEntry(new CanonicalEntry(null, entry.Resolve(_catalog.BaseUrl), entry.LastModified, null, null));
            }
        }

        if (missingFreshness > 0)
        {
            MissingFreshness(_logger, missingFreshness);
        }

        Dictionary<string, CanonicalEntry[]> groups = entries.Values.Where(x => x.Group is not null)
            .GroupBy(x => x.Group!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Locale!.Language, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        foreach (CanonicalEntry[] siblings in groups.Values)
        {
            if (siblings.Select(x => x.Locale!.Language).Distinct(StringComparer.OrdinalIgnoreCase).Count() != siblings.Length)
            {
                throw new InvalidOperationException($"Hreflang group for {siblings[0].Page!.Name} has multiple URLs for the same language. Supply distinct group identities.");
            }
        }

        List<SerializedEntry> serialized = [];
        foreach (CanonicalEntry entry in entries.Values.OrderBy(x => x.Url.AbsoluteUri, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CanonicalEntry[] siblings = entry.Group is null ? [] : groups[entry.Group];
            byte[] xml = SerializeEntry(entry, siblings);
            string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(entry.Url.AbsoluteUri)));
            serialized.Add(new SerializedEntry(entry.Url.AbsoluteUri, hash, xml));
        }

        return Partition(serialized, cancellationToken);

        void AddEntry(CanonicalEntry entry)
        {
            if (entries.TryGetValue(entry.Url.AbsoluteUri, out CanonicalEntry? existing))
            {
                if (existing != entry)
                {
                    throw new InvalidOperationException($"Conflicting sitemap ownership, freshness or alternate metadata for {entry.Url.AbsoluteUri}.");
                }
            }
            else
            {
                entries.Add(entry.Url.AbsoluteUri, entry);
            }
        }
    }

    private static async IAsyncEnumerable<SitemapEntry> FixedEntry()
    {
        yield return new SitemapEntry(new Dictionary<string, string?>());
        await Task.CompletedTask;
    }

    private byte[] SerializeEntry(CanonicalEntry entry, CanonicalEntry[] siblings)
    {
        using MemoryStream stream = new();
        using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
        {
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", entry.Url.AbsoluteUri);
            if (entry.LastModified is not null)
            {
                writer.WriteElementString("lastmod", entry.LastModified.Value);
            }

            foreach (CanonicalEntry sibling in siblings)
            {
                WriteAlternate(writer, sibling.Locale!.Language, sibling.Url.AbsoluteUri);
            }

            CanonicalEntry? fallback = siblings.FirstOrDefault(x => x.Locale!.RouteValue == _options.XDefaultLocale);
            if (fallback is not null)
            {
                WriteAlternate(writer, "x-default", fallback.Url.AbsoluteUri);
            }

            writer.WriteEndElement();
        }

        return stream.ToArray();
    }

    private static void WriteAlternate(XmlWriter writer, string language, string url)
    {
        writer.WriteStartElement("xhtml", "link", "http://www.w3.org/1999/xhtml");
        writer.WriteAttributeString("rel", "alternate");
        writer.WriteAttributeString("hreflang", language);
        writer.WriteAttributeString("href", url);
        writer.WriteEndElement();
    }

    private SitemapSnapshot Partition(List<SerializedEntry> entries, CancellationToken cancellationToken)
    {
        byte[] declaration = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        byte[] header = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">");
        byte[] footer = Encoding.UTF8.GetBytes("</urlset>");
        long overhead = header.Length + footer.Length;
        SortedDictionary<string, byte[]> children = new(StringComparer.Ordinal);
        if (Fits(entries))
        {
            return new SitemapSnapshot(Document(entries), children);
        }

        foreach (SerializedEntry entry in entries)
        {
            if (entry.Xml.LongLength + overhead > _options.MaxBytesPerSitemap)
            {
                throw new InvalidOperationException($"A single sitemap entry exceeds the byte limit: {entry.Url}. Reduce its alternate set or increase the limit within the protocol maximum.");
            }
        }

        Split(entries, "");
        if (children.Count > 50_000)
        {
            throw new InvalidOperationException("Sitemap index exceeds 50,000 child documents. Divide the site into separately managed sitemaps.");
        }

        using MemoryStream index = new();
        index.Write(declaration);
        using (XmlWriter writer = XmlWriter.Create(index, new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true }))
        {
            writer.WriteStartElement("sitemapindex", "http://www.sitemaps.org/schemas/sitemap/0.9");
            foreach (string prefix in children.Keys)
            {
                string childUrl = new Uri(_catalog.BaseUrl, $"sitemap-{prefix}.xml").AbsoluteUri;
                if (childUrl.Length >= 2048)
                {
                    throw new InvalidOperationException("Sitemap child URL must be shorter than 2048 characters. Shorten PublicBaseUrl's path base.");
                }

                writer.WriteStartElement("sitemap");
                writer.WriteElementString("loc", childUrl);
                // Page freshness cannot establish the modification time of a child sitemap document.
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        if (index.Length > 52_428_800)
        {
            throw new InvalidOperationException("Sitemap index exceeds 52,428,800 uncompressed bytes. Divide the site into separately managed sitemaps.");
        }

        return new SitemapSnapshot(index.ToArray(), children);

        bool Fits(List<SerializedEntry> items) => items.Count <= _options.MaxUrlsPerSitemap && overhead + items.Sum(x => x.Xml.LongLength) <= _options.MaxBytesPerSitemap;

        byte[] Document(List<SerializedEntry> items)
        {
            using MemoryStream output = new();
            output.Write(header);
            foreach (SerializedEntry entry in items)
            {
                output.Write(entry.Xml);
            }

            output.Write(footer);
            return output.ToArray();
        }

        void Split(List<SerializedEntry> items, string prefix)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Fits(items))
            {
                children.Add(prefix, Document(items));
                if (children.Count > 50_000)
                {
                    throw new InvalidOperationException("Sitemap index exceeds 50,000 child documents. Divide the site into separately managed sitemaps.");
                }

                return;
            }

            if (prefix.Length == 64)
            {
                throw new InvalidOperationException("Sitemap hash bucket cannot be partitioned within the configured limits.");
            }

            foreach (IGrouping<char, SerializedEntry> bucket in items.GroupBy(x => x.Hash[prefix.Length]).OrderBy(x => x.Key))
            {
                Split(bucket.ToList(), prefix + bucket.Key);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sitemap omitted lastmod for {Count} static URL instances because freshness was unavailable. Enable source-change stamps or register a page-specific ISitemapLastModifiedProvider; enable RequireLastModified to enforce completeness.")]
    private static partial void MissingFreshness(ILogger logger, int count);
}
