
using Microsoft.Extensions.DependencyInjection;

namespace Domain.Shared;

    public interface IQueryDispatcher
    {
        Task<TQueryResult> DispatchAsync<TQueryParameters, TQueryResult>(TQueryParameters parameters, CancellationToken cancellation);
    }

    public class QueryDispatcher : IQueryDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public QueryDispatcher(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        public Task<TQueryResult> DispatchAsync<TQueryParameters, TQueryResult>(TQueryParameters parameters, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredService<IQuery<TQueryParameters, TQueryResult>>();
            return handler.ExecuteAsync(parameters, cancellationToken);
        }
    }

