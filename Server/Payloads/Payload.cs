using System.ComponentModel.DataAnnotations;
using System.Text;
using Server.Core;

namespace Server.Payloads;

internal sealed class Payload : Entity
{
    [MaxLength(25)]
    public required string Name { get; init; }
    
    [MaxLength(25)]
    public required string User { get; init; }
    
    public required byte[] Bytes { get; init; }

    public byte[]? Yara { get; init; }
    
    [MaxLength(25)]
    public string? Note { get; init; }

    public static Payload Create(string name, string user, byte[] bytes, string? yara, string? note)
    {
        return new Payload
        {
            Name = name,
            User = user,
            Bytes = bytes,
            Yara = string.IsNullOrWhiteSpace(yara) ? null : Encoding.UTF8.GetBytes(yara),
            Note = note
        };
    }
}