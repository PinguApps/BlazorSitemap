using Consumer.Components;
using Consumer.Components.Pages;
using Consumer.Library;
using PinguApps.BlazorSitemap;

namespace Consumer;

public static class DemoHost
{
    public static WebApplication Create(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = typeof(DemoHost).Assembly.GetName().Name });
        configure?.Invoke(builder);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents();
        builder.Services.AddSingleton<BlogStore>();
        builder.Services.AddBlazorSitemap<App>(options =>
        {
            options.PublicBaseUrl = builder.Configuration["Sitemap:PublicBaseUrl"]!;
            options.UseSourceLastModified = builder.Configuration.GetValue("Sitemap:UseSourceLastModified", true);
            options.Locales(SupportedLocales.All, xDefaultLocale: "en-gb");
            options.AddAssembly(typeof(LibraryPage).Assembly);
            options.AddAssembly(typeof(Consumer.Client.AutoPage).Assembly);
        });
        builder.Services.AddSitemapEntries<BlogPost, BlogEntries>();
        builder.Services.AddSitemapLastModified<BlogIndex, BlogIndexFreshness>();
        const string guidePath = "/api/guide";
        builder.Services.AddSitemapUrls(new SitemapUrlEntry(guidePath, new SitemapLastModified(new DateOnly(2026, 9, 1))));
        builder.Services.AddSitemapUrls<ApiArticleUrls>();

        WebApplication app = builder.Build();
        string pathBase = new Uri(builder.Configuration["Sitemap:PublicBaseUrl"]!).AbsolutePath.TrimEnd('/');
        if (pathBase.Length != 0)
        {
            app.UsePathBase(pathBase);
        }

        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapBlazorSitemap();
        app.MapGet(guidePath, () => Results.Content("<!DOCTYPE html><html><head><title>Guide</title></head><body><h1>API-served guide</h1></body></html>", "text/html"));
        app.MapGet("/api/articles/{slug}", (string slug, BlogStore store) =>
        {
            Article? article = store.Published().FirstOrDefault(x => x.Locale == "en-gb" && x.Slug == slug);
            return article is null ? Results.NotFound() : Results.Content(
                $"<!DOCTYPE html><html><head><title>{System.Net.WebUtility.HtmlEncode(article.Title)}</title></head><body><h1>{System.Net.WebUtility.HtmlEncode(article.Title)}</h1><p>{System.Net.WebUtility.HtmlEncode(article.Body)}</p></body></html>", "text/html");
        });
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(LibraryPage).Assembly, typeof(Consumer.Client.AutoPage).Assembly);

        // Local demonstration controls only. Real CMS writes belong behind authentication and authorization.
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/demo/{operation}", (string operation, BlogStore store, SitemapCache cache) =>
            {
                store.Change(operation);
                cache.Invalidate(); // After the publication transaction commits.
                return Results.Redirect(DemoNavigation.To(typeof(BlogIndex), builder.Configuration["Sitemap:PublicBaseUrl"]!).AbsoluteUri);
            });
        }

        return app;
    }
}
