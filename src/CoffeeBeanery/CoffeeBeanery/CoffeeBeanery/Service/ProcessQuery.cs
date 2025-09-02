using System.Data;
using CoffeeBeanery.CQRS;
using Dapper;
using CoffeeBeanery.GraphQL.Extension;
using CoffeeBeanery.GraphQL.Model;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CoffeeBeanery.Service;

public class ProcessQuery<M, D, S> : IQuery<SqlStructure,
    (List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
    where M : class, new() where D : class where S : class
{
    private readonly ILogger<ProcessQuery<M, D, S>> _logger;
    private readonly NpgsqlConnection _dbConnection;
    private readonly ITreeMap<D, S> _treeMap;
    private List<M> _models;

    public ProcessQuery(ILoggerFactory loggerFactory, NpgsqlConnection dbConnection, ITreeMap<D, S> treeMap)
    {
        _logger = loggerFactory.CreateLogger<ProcessQuery<M, D, S>>();
        _dbConnection = dbConnection;
        _treeMap = treeMap;
        _models = new List<M>();
    }

    public async Task<(List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
        ExecuteAsync(SqlStructure parameters, CancellationToken cancellationToken)
    {
        var types = new List<Type>()
        {
            typeof(TotalPageRecords)
        };
        types.AddRange(_treeMap.EntityTypes.Select(t => t.GetType()));

        var typesToMap = new List<Type>
        {
            typeof(TotalRecordCount)
        };

        if (parameters.HasTotalCount && parameters.HasPagination)
        {
            parameters.SplitOnDapper.Insert(0, "RowNumber");
        }

        foreach (var splitOnPart in parameters.SplitOnDapper)
        {
            typesToMap.Add(types.FirstOrDefault(t =>
                t.Name.Matches(splitOnPart.Split('_')[0])));
        }

        var splitOn = string.Join(',', parameters.SplitOnDapper);
        var query = parameters.SqlUpsert + " ; " + parameters.SqlQuery;

        await using var connection = _dbConnection;
        await connection.OpenAsync(cancellationToken);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var result =
                await connection.QueryAsync<(int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>(
                    query, typesToMap.ToArray(), map =>
                    {
                        var set = mappingConfiguration(_models, parameters, map, typesToMap);
                        _models = set.models;
                        return (set.startCursor, set.endCursor, set.totalCount, set.totalPageRecords);
                    }, splitOn: splitOn, commandType: CommandType.Text);

            if (result == null || result.Count() == 0)
            {
                return ([], 0, 0, 0, 0);
            }

            return (_models,
                parameters.Pagination.StartCursor > 0
                    ? parameters.Pagination.StartCursor
                    : result.Select(s => s.startCursor).FirstOrDefault(),
                parameters.Pagination.EndCursor > 0
                    ? parameters.Pagination.EndCursor
                    : result.Select(s => s.endCursor).FirstOrDefault(),
                result.Select(s => s.totalCount).FirstOrDefault(),
                result.Select(s => s.totalPageRecords).FirstOrDefault());
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error upserting Process");
        }

        return ([], 0, 0, 0, 0);
    }

    public virtual (List<M> models, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)
        mappingConfiguration(List<M> models, SqlStructure sqlStructure, object[] map, List<Type> typesToMap)
    {
        throw new NotImplementedException();
    }
}