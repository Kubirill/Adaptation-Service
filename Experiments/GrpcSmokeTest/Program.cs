using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using AdaptationGrpc;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;

var address = GetArgValue(args, "--address") ?? "http://127.0.0.1:6002";
var timeoutMs = int.TryParse(GetArgValue(args, "--timeoutMs"), out var parsed) ? parsed : 3000;
var configVersion = GetArgValue(args, "--configVersion") ?? "v1";

var correlationId = Guid.NewGuid().ToString("N");

var handler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler())
{
    HttpVersion = System.Net.HttpVersion.Version11
};
var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = handler });
var client = new Adaptation.AdaptationClient(channel);

var request = new SessionResult
{
    SessionId = "smoke_session",
    SceneId = "SampleScene",
    ResultZ = 0.42f,
    TimeT = 1.23f,
    Attempts = 1,
    Seed = 1234,
    ConfigVersion = configVersion,
    CorrelationId = correlationId
};

var headers = new Metadata { { "x-correlation-id", correlationId } };
var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(100, timeoutMs));

try
{
    var sw = Stopwatch.StartNew();
    var call = client.ComputeNextAsync(request, new CallOptions(headers: headers, deadline: deadline));
    var response = await call.ResponseAsync;
    sw.Stop();

    var trailers = call.GetTrailers();
    Console.WriteLine($"ComputeNext OK in {sw.Elapsed.TotalMilliseconds:0.000} ms");
    Console.WriteLine($"NextSceneId: {response.NextSceneId}");
    Console.WriteLine($"Seed: {response.Seed}");
    Console.WriteLine($"ConfigVersion: {response.ConfigVersion}");
    Console.WriteLine($"CorrelationId: {response.CorrelationId}");
    Console.WriteLine($"NpcParams count: {response.NpcParams.Count}");
    Console.WriteLine($"Explanation count: {response.Explanation.Count}");

    var serverMs = GetTrailer(trailers, "x-server-compute-ms");
    if (!string.IsNullOrWhiteSpace(serverMs))
    {
        Console.WriteLine($"ServerComputeMs: {serverMs}");
    }
}
catch (RpcException ex)
{
    Console.Error.WriteLine($"gRPC error: {ex.StatusCode} {ex.Status.Detail}");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}

static string GetTrailer(Metadata trailers, string key)
{
    if (trailers == null)
    {
        return string.Empty;
    }

    foreach (var entry in trailers)
    {
        if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
        {
            return entry.Value ?? string.Empty;
        }
    }
    return string.Empty;
}

static string GetArgValue(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return null;
}
