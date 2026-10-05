using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PinguApps.BlazorSitemap;

/// <summary>Registers sitemap services for server-hosted Blazor.</summary>
public static class SitemapServiceCollectionExtensions
{
    /// <summary>Adds fixed published URLs independently of component routes. Repeat for additional batches.</summary>
    public static IServiceCollection AddSitemapUrls(this IServiceCollection services, params SitemapUrlEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(entries);
        SitemapOptions options = services.SingleOrDefault(x => x.ServiceType == typeof(SitemapOptions))?.ImplementationInstance as SitemapOptions
            ?? throw new InvalidOperationException("Call AddBlazorSitemap before AddSitemapUrls.");
        Uri baseUrl = options.Validate();
        foreach (SitemapUrlEntry entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            _ = entry.Resolve(baseUrl);
        }

        services.AddSingleton(new SitemapUrlRegistration(entries.ToArray()));
        return services;
    }

    /// <summary>Adds a scoped standalone URL provider. Repeat for different providers; registering the same provider again has no effect.</summary>
    public static IServiceCollection AddSitemapUrls<TProvider>(this IServiceCollection services)
        where TProvider : class, ISitemapUrlProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!services.Any(x => x.ServiceType == typeof(SitemapOptions)))
        {
            throw new InvalidOperationException("Call AddBlazorSitemap before AddSitemapUrls.");
        }

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISitemapUrlProvider, TProvider>());
        return services;
    }

    /// <summary>Discovers included pages in TApp's assembly and configures canonical URL generation.</summary>
    /// <typeparam name="TApp">An application component or assembly marker.</typeparam>
    public static IServiceCollection AddBlazorSitemap<TApp>(this IServiceCollection services, Action<SitemapOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(x => x.ServiceType == typeof(SitemapOptions)))
        {
            throw new InvalidOperationException("AddBlazorSitemap may be called only once. Use options.AddAssembly for additional assemblies.");
        }

        SitemapOptions options = new();
        options.AddAssembly(typeof(TApp).Assembly);
        configure(options);
        _ = options.Validate();
        services.AddSingleton(options);
        services.AddRouting();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<RouteCatalog>();
        services.AddSingleton(provider => new SitemapCache(
            provider.GetRequiredService<SitemapOptions>(),
            provider.GetRequiredService<RouteCatalog>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SitemapCache>>()));
        return services;
    }

    /// <summary>Registers a scoped provider that replaces automatic instances of the selected page.</summary>
    public static IServiceCollection AddSitemapEntries<TPage, TProvider>(this IServiceCollection services)
        where TPage : IComponent
        where TProvider : class, ISitemapEntryProvider<TPage>
    {
        ArgumentNullException.ThrowIfNull(services);
        SitemapOptions options = services.SingleOrDefault(x => x.ServiceType == typeof(SitemapOptions))?.ImplementationInstance as SitemapOptions
            ?? throw new InvalidOperationException("Call AddBlazorSitemap before AddSitemapEntries.");
        options.AddAssembly(typeof(TPage).Assembly);
        services.TryAddScoped<TProvider>();
        services.AddSingleton(new SourceRegistration(typeof(TPage), (provider, token) => provider.GetRequiredService<TProvider>().GetEntriesAsync(token)));
        return services;
    }

    /// <summary>Registers a scoped freshness provider for one page. Repeat for other page types.</summary>
    public static IServiceCollection AddSitemapLastModified<TPage, TProvider>(this IServiceCollection services)
        where TPage : IComponent
        where TProvider : class, ISitemapLastModifiedProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        SitemapOptions options = services.SingleOrDefault(x => x.ServiceType == typeof(SitemapOptions))?.ImplementationInstance as SitemapOptions
            ?? throw new InvalidOperationException("Call AddBlazorSitemap before AddSitemapLastModified.");
        if (services.Any(x => x.ImplementationInstance is FreshnessRegistration registration && registration.PageType == typeof(TPage)))
        {
            throw new InvalidOperationException($"A last-modified provider is already registered for {typeof(TPage).Name}.");
        }

        options.AddAssembly(typeof(TPage).Assembly);
        services.TryAddScoped<TProvider>();
        services.AddSingleton(new FreshnessRegistration(typeof(TPage), provider => provider.GetRequiredService<TProvider>()));
        return services;
    }
}
