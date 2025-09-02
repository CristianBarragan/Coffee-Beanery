using Npgsql;

namespace CoffeeBeanery.CQRS;

public interface IUnitOfWorkContext
{
    IUnitOfWork CreateUnitOfWork();

    NpgsqlConnection GetConnection();
}

public class UnitOfWorkContext : IUnitOfWorkContext
{
    private readonly NpgsqlConnection _npgsqlConnection;

    private IUnitOfWork _unitOfWork;

    public UnitOfWorkContext(NpgsqlConnection npgsqlConnection)
    {
        _npgsqlConnection = npgsqlConnection;
    }

    public IUnitOfWork CreateUnitOfWork()
    {
        if (_npgsqlConnection.State == System.Data.ConnectionState.Open)
        {
            return _unitOfWork;
        }

        _unitOfWork = new UnitOfWork(_npgsqlConnection);

        return _unitOfWork;
    }

    public NpgsqlConnection GetConnection()
    {
        return _unitOfWork!.NpgsqlConnection;
    }
}