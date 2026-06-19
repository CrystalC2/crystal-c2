using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Server.Core;
using Server.Utilities;

namespace Server.Listeners;

public sealed class Listener : Entity
{
    [MaxLength(25)]
    public required string Name { get; init; }
    public required ushort BindPort { get; init; }
    public required byte[] X86Coff { get; init; }
    public required byte[] X64Coff { get; init; }
    public required byte[] PublicKey { get; init; }
    public required byte[] PrivateKey { get; init; }

    public static Listener Create(string name, ushort bindPort, byte[] x86Coff, byte[] x64Coff)
    {
        using var rsa = RSA.Create(1024);

        return new Listener
        {
            Id = Helpers.GenerateId(),
            Name = name,
            BindPort = bindPort,
            X86Coff = x86Coff,
            X64Coff = x64Coff,
            PublicKey = rsa.ExportSubjectPublicKeyInfo(),
            PrivateKey = rsa.ExportRSAPrivateKey()
        };
    }
    
    // instance implementation
    
    private IServiceScopeFactory? _factory;
    private bool _running;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _accept;

    public void Start(IServiceScopeFactory factory)
    {
        if (_running)
        {
            return;
        }
        
        _factory = factory;
        
        _cts = new CancellationTokenSource();
        
        _listener = new TcpListener(IPAddress.Any, BindPort);
        _listener.Start();
        
        _running = true;
        
        _accept = AcceptClient(_cts.Token);
    }
    
    private async Task AcceptClient(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                await HandleClient(client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }
    
    private async Task HandleClient(TcpClient client, CancellationToken ct)
    {
        await using var stream = client.GetStream();

        while (client.Connected && !ct.IsCancellationRequested)
        {
            // read length
            var lenBuf = new byte[4];
            var lenRead = await ReadExactly(stream, lenBuf, ct);

            if (lenRead < 4)
            {
                break;
            }

            var msgLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuf.AsSpan());

            // read message
            var msg = new byte[msgLen];
            _ = await ReadExactly(stream, msg, ct);

            await using var scope = _factory!.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<C2Bridge>();
            var outbound = await manager.ProcessBeaconMessage(Id, msg, ct);
            
            // write response length
            BinaryPrimitives.WriteInt32LittleEndian(lenBuf, outbound.Length);
            await stream.WriteAsync(lenBuf.AsMemory(), ct);
            
            // write response
            await stream.WriteAsync(outbound.AsMemory(), ct);
        }
    }
    
    private static async Task<int> ReadExactly(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            
            if (read == 0)
            {
                break;
            }
            
            totalRead += read;
        }
        
        return totalRead;
    }

    public async Task Stop()
    {
        if (!_running)
        {
            return;
        }

        if (_cts is not null)
        {
            await _cts.CancelAsync();
            
            _cts.Dispose();
            _cts = null;
        }
        
        _listener?.Stop();
        _listener?.Dispose();
        _listener = null;

        if (_accept is not null)
        {
            try
            {
                await _accept;
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        }
        
        _running = false;
    }
}