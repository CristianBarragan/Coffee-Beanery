
namespace Domain.Shared;

    public interface IQuery<in TQueryParameters, TQueryResult>
    {
        Task<TQueryResult> ExecuteAsync(TQueryParameters parameters, CancellationToken cancellationToken);
    }

