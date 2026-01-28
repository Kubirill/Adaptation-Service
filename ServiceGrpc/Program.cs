using System;
using System.Net;
using AdaptationCore;
using ServiceGrpc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

var settings = ServiceGrpcSettings.Load(builder.Configuration);
var config = ConfigLoader.LoadByVersion(settings.ConfigRoot, settings.ConfigVersion);
var auditWriter = new ServiceAuditWriter(settings.AuditRoot);

builder.Services.AddGrpc();
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton(auditWriter);

builder.WebHost.ConfigureKestrel(options =>
{
    ConfigureEndpoint(options, settings.GrpcUrl, HttpProtocols.Http1AndHttp2);
    ConfigureEndpoint(options, settings.HealthUrl, HttpProtocols.Http1);
});

var app = builder.Build();

app.UseGrpcWeb();
app.MapGrpcService<ServiceGrpc.AdaptationGrpcService>().EnableGrpcWeb();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

Console.WriteLine($"ServiceGrpc starting.");
Console.WriteLine($"Config root: {settings.ConfigRoot}");
Console.WriteLine($"Config version: {settings.ConfigVersion}");
Console.WriteLine($"Audit root: {settings.AuditRoot}");
Console.WriteLine($"gRPC URL: {settings.GrpcUrl}");
Console.WriteLine($"Health URL: {settings.HealthUrl}");

app.Run();

static void ConfigureEndpoint(KestrelServerOptions options, string url, HttpProtocols protocols)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException($"Invalid URL: {url}");
    }

    var host = uri.Host;
    var port = uri.Port;

    if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1")
    {
        options.ListenLocalhost(port, listenOptions => listenOptions.Protocols = protocols);
        return;
    }

    if (host == "0.0.0.0" || host == "*" || host == "+")
    {
        options.ListenAnyIP(port, listenOptions => listenOptions.Protocols = protocols);
        return;
    }

    if (!IPAddress.TryParse(host, out var ipAddress))
    {
        ipAddress = IPAddress.Any;
    }

    options.Listen(ipAddress, port, listenOptions => listenOptions.Protocols = protocols);
}
