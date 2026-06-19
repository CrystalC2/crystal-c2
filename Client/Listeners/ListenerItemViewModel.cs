using System;
using CrystalC2.Listeners;
using ReactiveUI;

namespace Client.Listeners;

internal sealed class ListenerItemViewModel(ListenerInfo info) : ReactiveObject
{
    public uint Id { get; } = info.Id;
    public string Name { get; } = info.Name;
    public uint Port { get; } = info.BindPort;
    public string PublicKey { get; } = Convert.ToBase64String(info.PublicKey.ToByteArray());
    public string PortDisplay => $"0.0.0.0:{Port}";
}