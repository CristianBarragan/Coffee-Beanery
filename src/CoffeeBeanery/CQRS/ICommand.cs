namespace CoffeeBeanery.CQRS;

public interface ICommand<in TCommandParameters, TCommandResult>
{
    Task<TCommandResult> ExecuteAsync(TCommandParameters parameters, CancellationToken cancellationToken);
}