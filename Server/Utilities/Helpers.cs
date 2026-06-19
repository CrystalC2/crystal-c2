using System.Buffers.Binary;
using System.Text;

namespace Server.Utilities;

public static class Helpers
{
    public static uint GenerateId()
    {
        return BitConverter.ToUInt32(Guid.NewGuid().ToByteArray());
    }

    public static string ReadBigEndianLengthPrefixedString(byte[] data, ref int offset)
    {
        var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, sizeof(int)));
        offset += sizeof(int);

        if (length <= 0)
            return string.Empty;
        
        var result = Encoding.Default.GetString(data, offset, length).TrimEnd();
        offset += length;
        
        return result;
    }
}