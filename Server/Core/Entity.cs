using System.ComponentModel.DataAnnotations;

namespace Server.Core;

public abstract class Entity : IAggregateRoot
{
    [Key]
    public uint Id { get; init; }
}