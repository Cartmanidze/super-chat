namespace SuperChat.Domain.Shared;

public interface ISpecification<in T>
{
    bool IsSatisfiedBy(T entity);
}
