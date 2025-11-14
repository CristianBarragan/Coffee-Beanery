using System.Data;
using CoffeeBeanery.CQRS;
using Dapper;
using CoffeeBeanery.GraphQL.Model;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CoffeeBeanery.Service;

public class ProcessQuery<M> : IQuery<SqlStructure,
    (List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
    where M : class
{
    private readonly ILogger<ProcessQuery<M>> _logger;
    private readonly NpgsqlConnection _dbConnection;
    private List<M> _models;

    public ProcessQuery(ILoggerFactory loggerFactory, NpgsqlConnection dbConnection)
    {
        _logger = loggerFactory.CreateLogger<ProcessQuery<M>>();
        _dbConnection = dbConnection;
        _models = new List<M>();
    }

    public async Task<(List<M> list, int? startCursor, int? endCursor, int? totalCount, 
            int? totalPageRecords)>
        ExecuteAsync(SqlStructure parameters, CancellationToken cancellationToken)
    {
        var splitOnTypes = parameters.SplitOnDapper.Values.Distinct().ToList();
        var splitOn = parameters.SplitOnDapper
            .Select(a => a.Key).ToList();
        
        if (parameters != null && parameters.HasTotalCount && parameters.HasPagination)
        {
            splitOnTypes.Add(typeof(TotalPageRecords));
            splitOnTypes.Add(typeof(TotalRecordCount));
            splitOn.Insert(0, "RowNumber");
        }

        var query = parameters.SqlUpsert + " ; " + parameters.SqlQuery;

        await using var connection = _dbConnection;
        await connection.OpenAsync(cancellationToken);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var result =
                await connection.QueryAsync<(int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>(
                    query, splitOnTypes.ToArray(), map =>
                    {
                        var set = MappingConfiguration(_models, parameters, map);
                        _models = set.models;
                        return (set.startCursor, set.endCursor, set.totalCount, set.totalPageRecords);
                    }, splitOn: string.Join(",", splitOn), commandType: CommandType.Text);

            if (result == null || result.Count() == 0)
            {
                return ([], 0, 0, 0, 0);
            }
            
            await dbTransaction.CommitAsync(cancellationToken);

            return (_models,
                parameters.Pagination.StartCursor > 0
                    ? parameters.Pagination.StartCursor
                    : result.Select(s => s.startCursor).FirstOrDefault(),
                parameters.Pagination.EndCursor > 0
                    ? parameters.Pagination.EndCursor
                    : result.Select(s => s.endCursor).FirstOrDefault(),
                result.Select(s => s.totalCount).FirstOrDefault(),
                result.Select(s => s.totalPageRecords)
                    .FirstOrDefault());
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error upserting Process");
        }

        return ([], 0, 0, 0, 0);
    }

    public virtual (List<M> models, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)
        MappingConfiguration(List<M> models, SqlStructure sqlStructure, object[] map)
    {
        throw new NotImplementedException();
    }
}