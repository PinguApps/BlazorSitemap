using Consumer;

WebApplication app = DemoHost.Create(args);
try
{
    await app.RunAsync();
}
finally { await app.DisposeAsync(); }
