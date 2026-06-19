using CrystalC2.Payloads;

namespace Client.Payloads;

internal sealed class PayloadItemViewModel(PayloadInfo info)
{
    public uint Id { get; } = info.Id;
    public string Name { get; } = info.Name;
    public string User { get; } = info.User;
    public string? Note { get; } = info.HasNote ? info.Note : null;
    public bool HasYara { get; } = info.HasYara;
    public string? Yara { get; set; } = info.Yara;
    public byte[] Bytes { get; } = info.Payload.ToByteArray();
}