using System.Buffers.Binary;
using Server.Utilities;

namespace Server.Beacons;

/// <summary>
/// This is the metadata sent by a Beacon
/// </summary>
public sealed class BeaconMetadata
{
    public uint Id { get; set; }
    public byte[] SessionKey { get; set; }
    public string User { get; set; }
    public string Computer { get; set; }
    public string Process { get; set; }
    public int ProcessId { get; set; }
    public uint MajorVersion { get; set; }
    public uint MinorVersion { get; set; }
    public uint BuildVersion { get; set; }
    public int CharSet { get; set; }
    public byte[] InternalAddress { get; set; }
    public BeaconFlags Flags { get; set; }

    public static BeaconMetadata Parse(byte[] data)
    {
        var metadata = new BeaconMetadata();
        var offset = 0;
        
        metadata.Id = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);
        
        metadata.SessionKey = data.AsSpan(offset, 16).ToArray();
        offset += 16;

        metadata.User = Helpers.ReadBigEndianLengthPrefixedString(data, ref offset);
        metadata.Computer = Helpers.ReadBigEndianLengthPrefixedString(data, ref offset);
        metadata.Process = Helpers.ReadBigEndianLengthPrefixedString(data, ref offset);
        
        metadata.ProcessId = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, sizeof(int)));
        offset += sizeof(int);

        metadata.MajorVersion = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);

        metadata.MinorVersion = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);
        
        metadata.BuildVersion = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);
        
        metadata.CharSet = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, sizeof(int)));
        offset += sizeof(int);
        
        var ip = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, sizeof(uint)));
        metadata.InternalAddress = BitConverter.GetBytes(ip);
        offset += sizeof(uint);
        
        metadata.Flags = (BeaconFlags)data[offset];

        return metadata;
    }
}