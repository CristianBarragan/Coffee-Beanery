using Microsoft.Extensions.DependencyInjection;

namespace CoffeeBeanery.CQRS;

public interface ICommandDispatcher
{
    Task<TCommandResult> DispatchAsync<TCommandParameters, TCommandResult>(TCommandParameters parameters,
        CancellationToken cancellation);
}

public class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public CommandDispatcher(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TCommandResult> DispatchAsync<TCommandParameters, TCommandResult>(TCommandParameters parameters,
        CancellationToken cancellationToken)
    {
        var handler = _serviceProvider.GetRequiredService<ICommand<TCommandParameters, TCommandResult>>();
        return handler.ExecuteAsync(parameters, cancellationToken);
    }
}