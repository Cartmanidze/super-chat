namespace SuperChat.Infrastructure.Shared.Persistence;

internal interface IEfSpecification<TEntity> where TEntity : class
{
    IQueryable<TEntity> Apply(IQueryable<TEntity> query);
}
