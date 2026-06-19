using Ardalis.Specification.EntityFrameworkCore;

namespace Server.Core;

internal sealed class CrystalRepository<T>(CrystalDb db)
    : RepositoryBase<T>(db), IRepository<T> where T
    : class, IAggregateRoot;