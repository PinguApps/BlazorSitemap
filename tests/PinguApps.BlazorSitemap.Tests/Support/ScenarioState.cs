using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using Consumer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PinguApps.BlazorSitemap.Tests.Support;

public sealed class ScenarioState
{
    public List<PageSpec> Pages { get; } = [];
    public List<Action<SitemapOptions>> Configure { get; } = [];
    public List<SitemapUrlEntry> StaticUrls { get; } = [];
    public bool UseUrlProvider { get; set; }
    public bool UseOtherUrlProvider { get; set; }
    public bool DuplicateUrlProvider { get; set; }
    public TestContent Content { get; } = new();
    public ManualClock Clock { get; } = new();
    public WebApplication? App { get; private set; }
    public HttpClient? Client { get; private set; }
    public string BaseUrl { get; set; } = "https://example.com";
    public string PathBase { get; set; } = "";
    public int UrlLimit { get; set; } = 50_000;
    public int ByteLimit { get; set; } = 52_428_800;
    public int LifetimeSeconds { get; set; } = 300;
    public bool RequireFreshness { get; set; }
    public bool IsConsumer { get; set; }
    public bool DuplicateFreshness { get; set; }
    public bool UseConsumerSourceEstimates { get; set; } = true;
    public Exception? Error { get; set; }
    public byte[] Xml { get; set; } = [];
    public byte[] PreviousXml { get; set; } = [];
    public XDocument Document => XDocument.Parse(System.Text.Encoding.UTF8.GetString(Xml));
    public HttpResponseMessage? Response { get; set; }
    public Dictionary<string, byte[]> Children { get; } = new(StringComparer.Ordinal);
    public static XNamespace SitemapNamespace => "http://www.sitemaps.org/schemas/sitemap/0.9";
    public static XNamespace XhtmlNamespace => "http://www.w3.org/1999/xhtml";

    public async Task StartAsync()
    {
        try
        {
            if (IsConsumer)
            {
                string root = RepositoryRoot();
                App = DemoHost.Create(["--contentRoot", Path.Combine(root, "tests", "Consumer"), "--environment", "Development", "--Sitemap:PublicBaseUrl", BaseUrl, "--Sitemap:UseSourceLastModified", UseConsumerSourceEstimates.ToString()], builder =>
                {
                    builder.WebHost.UseTestServer();
                    builder.Logging.ClearProviders();
                });
            }
            else
            {
                AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("SitemapScenario" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
                ModuleBuilder module = assembly.DefineDynamicModule("Pages");
                for (int i = 0; i < Pages.Count; i++)
                {
                    PageSpec page = Pages[i];
                    TypeBuilder type = module.DefineType("Page" + i, TypeAttributes.Public | TypeAttributes.Sealed, typeof(ComponentBase));
                    foreach (string route in page.Routes)
                    {
                        type.SetCustomAttribute(new CustomAttributeBuilder(typeof(RouteAttribute).GetConstructor([typeof(string)])!, [route]));
                    }

                    if (page.Included)
                    {
                        type.SetCustomAttribute(new CustomAttributeBuilder(typeof(SitemapPageAttribute).GetConstructor([typeof(string)])!, [""],
                            [typeof(SitemapPageAttribute).GetProperty(nameof(SitemapPageAttribute.CanonicalRouteIndex))!, typeof(SitemapPageAttribute).GetProperty(nameof(SitemapPageAttribute.Hreflang))!, typeof(SitemapPageAttribute).GetProperty(nameof(SitemapPageAttribute.DynamicContent))!], [page.Canonical, page.Hreflang, page.DynamicContent]));
                    }

                    if (page.Authorized)
                    {
                        type.SetCustomAttribute(new CustomAttributeBuilder(typeof(AuthorizeAttribute).GetConstructor(Type.EmptyTypes)!, []));
                    }

                    page.Type = type.CreateType()!;
                }

                WebApplicationBuilder builder = WebApplication.CreateBuilder(["--environment", "Development"]);
                builder.WebHost.UseTestServer();
                builder.Logging.ClearProviders();
                builder.Services.AddSingleton(Content);
                builder.Services.AddSingleton<TimeProvider>(Clock);
                builder.Services.AddBlazorSitemap<ScenarioState>(options =>
                {
                    options.PublicBaseUrl = BaseUrl;
                    options.CacheLifetime = TimeSpan.FromSeconds(LifetimeSeconds);
                    options.RequireLastModified = RequireFreshness;
                    options.MaxUrlsPerSitemap = UrlLimit;
                    options.MaxBytesPerSitemap = ByteLimit;
                    options.AddAssembly(assembly);
                    foreach (Action<SitemapOptions> configure in Configure)
                    {
                        configure(options);
                    }
                });
                foreach (PageSpec page in Pages.Where(x => x.Dynamic))
                {
                    RegisterProvider(builder.Services, page.Type!);
                }

                foreach (PageSpec page in Pages.Where(x => x.Included))
                {
                    RegisterFreshness(builder.Services, page.Type!);
                }
                if (DuplicateFreshness) { RegisterFreshness(builder.Services, Pages[0].Type!); }
                foreach (SitemapUrlEntry entry in StaticUrls) { builder.Services.AddSitemapUrls(entry); }
                if (UseUrlProvider) { builder.Services.AddSitemapUrls<MemoryUrlProvider>(); }
                if (UseOtherUrlProvider) { builder.Services.AddSitemapUrls<OtherUrlProvider>(); }
                if (DuplicateUrlProvider) { builder.Services.AddSitemapUrls<MemoryUrlProvider>(); }
                App = builder.Build();
                if (PathBase.Length != 0)
                {
                    App.UsePathBase(PathBase);
                }

                App.MapBlazorSitemap();
            }

            await App.StartAsync();
            Client = App.GetTestClient();
        }
        catch (Exception exception)
        {
            Error = exception;
        }
    }

    public async Task FetchAsync()
    {
        if (Error is not null)
        {
            return;
        }

        try
        {
            PreviousXml = Xml;
            Response?.Dispose();
            Response = await Client!.GetAsync(PathBase + "/sitemap.xml");
            Xml = await Response.Content.ReadAsByteArrayAsync();
            if ((int)Response.StatusCode >= 500)
            {
                Error = new InvalidOperationException(System.Text.Encoding.UTF8.GetString(Xml));
            }
        }
        catch (Exception exception)
        {
            Error = exception;
        }
    }

    public async Task StopAsync()
    {
        Client?.Dispose();
        Response?.Dispose();
        if (App is not null)
        {
            await App.DisposeAsync();
        }

        App = null;
        Client = null;
    }

    public static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BlazorSitemap.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Cannot locate consumer fixture root.");
    }

    private static void RegisterProvider(IServiceCollection services, Type page)
    {
        Type provider = typeof(MemoryProvider<>).MakeGenericType(page);
        typeof(SitemapServiceCollectionExtensions).GetMethod(nameof(SitemapServiceCollectionExtensions.AddSitemapEntries))!
            .MakeGenericMethod(page, provider).Invoke(null, [services]);
    }

    private static void RegisterFreshness(IServiceCollection services, Type page)
        => typeof(SitemapServiceCollectionExtensions).GetMethod(nameof(SitemapServiceCollectionExtensions.AddSitemapLastModified))!
            .MakeGenericMethod(page, typeof(TestFreshness<>).MakeGenericType(page)).Invoke(null, [services]);
}
