using Ardalis.Specification;

namespace Server.Core;

public interface IRepository<T>
    : IRepositoryBase<T> where T
    : class, IAggregateRoot;