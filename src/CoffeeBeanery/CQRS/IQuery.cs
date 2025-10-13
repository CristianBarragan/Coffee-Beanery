namespace CoffeeBeanery.CQRS;

public interface IQuery<in TQueryParameters, TQueryResult>
{
    Task<TQueryResult> ExecuteAsync(TQueryParameters parameters, CancellationToken cancellationToken);
}