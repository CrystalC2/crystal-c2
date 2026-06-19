using System;
using System.Threading;
using System.Threading.Tasks;
using CrystalC2.Tasks;
using Grpc.Core;

namespace Client.Services;

internal sealed class TaskGrpcService(ServerConnection connection) : IAsyncDisposable
{
    private AsyncDuplexStreamingCall<TaskRequest, TaskResponse>? _call;

    public async Task StartAsync(Action<TaskResponse> onResponse, CancellationToken ct)
    {
        var client = new TaskProtoService.TaskProtoServiceClient(connection.GetChannel());
        _call = client.StreamTasks(connection.GetCallOptions(ct));
        _ = ReadLoopAsync(onResponse, ct);
    }

    public async Task SendAsync(TaskRequest request)
    {
        if (_call is null) return;
        await _call.RequestStream.WriteAsync(request);
    }

    private async Task ReadLoopAsync(Action<TaskResponse> onResponse, CancellationToken ct)
    {
        try
        {
            while (await _call!.ResponseStream.MoveNext(ct))
            {
                onResponse(_call.ResponseStream.Current);
            }
        }
        catch (OperationCanceledException) { }
        catch (RpcException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_call is null) return;
        try { await _call.RequestStream.CompleteAsync(); } catch { }
        _call.Dispose();
        _call = null;
    }
}