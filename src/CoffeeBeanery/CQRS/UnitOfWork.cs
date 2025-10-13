using Npgsql;
using System.Data;

namespace CoffeeBeanery.CQRS;

public interface IUnitOfWork
{
    void BeginTransaction();

    void RollbackTranscation();

    void CommitTransaction();

    void DisposeConnection();

    bool IsDisposed { get; }

    int NumberOfNestedTransactions { get; }

    NpgsqlConnection NpgsqlConnection { get; }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _npgsqlConnection;

    private IDbTransaction _dbTransaction;

    public NpgsqlConnection NpgsqlConnection => _npgsqlConnection;

    public bool IsDisposed { get; private set; } = false;

    public int NumberOfNestedTransactions { get; private set; } = 0;

    public bool InTransaction { get; private set; } = false;

    public UnitOfWork(NpgsqlConnection npgsqlConnection)
    {
        _npgsqlConnection = npgsqlConnection;
        _npgsqlConnection.Open();
    }

    public void BeginTransaction()
    {
        if (NumberOfNestedTransactions == 0)
        {
            _dbTransaction = _npgsqlConnection.BeginTransaction();
            InTransaction = true;
        }

        NumberOfNestedTransactions++;
    }

    public void CommitTransaction()
    {
        if (NumberOfNestedTransactions == 1 && InTransaction)
        {
            _dbTransaction.Commit();
            InTransaction = false;
        }

        NumberOfNestedTransactions--;
    }

    public void RollbackTranscation()
    {
        if (NumberOfNestedTransactions == 1 && InTransaction)
        {
            _dbTransaction.Rollback();
            InTransaction = false;
        }
    }

    public void DisposeConnection()
    {
        if (NumberOfNestedTransactions == 0 && InTransaction)
        {
            _dbTransaction.Dispose();
            _npgsqlConnection?.Dispose();
            _npgsqlConnection?.Close();
            InTransaction = false;
        }
    }
}