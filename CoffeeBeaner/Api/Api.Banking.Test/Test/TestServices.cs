using Api.Banking.Mutation;
using Api.Banking.Query;
using HotChocolate.Execution;
using HotChocolate.Types.Pagination;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Banking.Test.Test;

public class TestServices
{
    private readonly IServiceProvider _serviceProvider;
    
    public TestServices(IServiceProvider serviceProvider)
    {
        Executor = serviceProvider.GetRequiredService<RequestExecutorProxy>();
        _serviceProvider = serviceProvider;
    }

    public IServiceProvider Services { get; }

    public RequestExecutorProxy Executor { get; }

    public async Task<IExecutionResult> ExecuteRequestAsync(
        Action<IQueryRequestBuilder> configureRequest,
        CancellationToken cancellationToken = default)
    {
        var scope = _serviceProvider.CreateAsyncScope();

        var requestBuilder = new QueryRequestBuilder();
        requestBuilder.SetServices(scope.ServiceProvider);
        configureRequest(requestBuilder);
        var request = requestBuilder.Create();

        var result = await Executor.ExecuteAsync(request, cancellationToken);
        result.RegisterForCleanup(scope.DisposeAsync);
        return result;
    }
}