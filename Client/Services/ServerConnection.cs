using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CrystalC2.Auth;
using Grpc.Core;
using Grpc.Net.Client;

namespace Client.Services;

internal sealed class ServerConnection : IDisposable
{
    private GrpcChannel? _channel;
    private string? _token;

    public bool IsConnected => _channel is not null && _token is not null;
    public string ServerAddress { get; private set; } = string.Empty;

    public async Task ConnectAsync(string address, string username, string password)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = handler
        });

        var authClient = new AuthProtoService.AuthProtoServiceClient(channel);
        var response = await authClient.LoginAsync(new LoginRequest
        {
            Username = username,
            Password = password
        });

        _channel?.Dispose();
        _channel = channel;
        _token = response.Token;
        
        ServerAddress = address;
    }

    public CallOptions GetCallOptions(CancellationToken ct = default)
    {
        var metadata = new Metadata { { "Authorization", $"Bearer {_token}" } };
        return new CallOptions(metadata, cancellationToken: ct);
    }

    public GrpcChannel GetChannel()
    {
        return _channel ?? throw new InvalidOperationException("Not connected to server.");
    }

    public void Disconnect()
    {
        _channel?.Dispose();
        _channel = null;
        _token = null;
        
        ServerAddress = string.Empty;
    }

    public void Dispose() => _channel?.Dispose();
}