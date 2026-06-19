using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Client.Services;

internal sealed class LinkerApiClient
{
    private const string BaseUrl = "http://localhost:60060";

    private readonly HttpClient _http = new() { BaseAddress = new Uri(BaseUrl) };

    internal async Task<LinkerResult> GenerateAsync(LinkerRequest request)
    {
        var response = await _http.PostAsJsonAsync("/link", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LinkerResponse>()
            ?? throw new InvalidOperationException("Empty response from linker API");

        if (!result.Success)
        {
            var prefix = result.Context is not null ? $"{result.Context}: " : string.Empty;
            throw new InvalidOperationException($"{prefix}{result.Message ?? "Unknown error"}");
        }

        var bytes = Convert.FromBase64String(result.OutputB64!);
        var yara  = string.IsNullOrWhiteSpace(result.Yara) ? null : result.Yara;
        return new LinkerResult(bytes, yara);
    }
}

internal sealed record LinkerResult(byte[] Bytes, string? Yara);

internal sealed record LinkerResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("output_b64")]
    public string? OutputB64 { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("context")]
    public string? Context { get; init; }

    [JsonPropertyName("yara")]
    public string? Yara { get; init; }
}

internal sealed record LinkerRequest
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = "piclink";

    [JsonPropertyName("params")]
    public LinkerParams Params { get; init; } = new();

    [JsonPropertyName("config")]
    public string[] Config { get; init; } = [];

    [JsonPropertyName("env")]
    public LinkerEnv Env { get; init; } = new();

    [JsonPropertyName("yara")]
    public bool Yara { get; init; }
}

internal sealed record LinkerParams
{
    [JsonPropertyName("spec")]
    public string Spec { get; init; } = string.Empty;

    [JsonPropertyName("arch")]
    public string Arch { get; init; } = "x64";
    
    [JsonPropertyName("file")]
    public string File { get; init; } = string.Empty;
}

internal sealed record LinkerEnv
{
    [JsonPropertyName("%SLEEP")]
    public string Sleep { get; init; } = "060";

    [JsonPropertyName("%JITTER")]
    public string Jitter { get; init; } = "020";

    [JsonPropertyName("%UDC2")]
    public string Udc2 { get; init; } = string.Empty;

    [JsonPropertyName("%PUBKEY")]
    public string PubKey { get; init; } = string.Empty;

    [JsonPropertyName("%EXITFUNC")]
    public string ExitFunc { get; init; } = "1";
}