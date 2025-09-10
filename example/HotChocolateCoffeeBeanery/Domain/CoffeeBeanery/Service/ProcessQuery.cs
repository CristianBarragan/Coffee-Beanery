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
    private readonly IModelTreeMap<D, S> _modelTreeMap;
    private List<M> _models;

    public ProcessQuery(ILoggerFactory loggerFactory, NpgsqlConnection dbConnection, IModelTreeMap<D, S> modelTreeMap)
    {
        _logger = loggerFactory.CreateLogger<ProcessQuery<M, D, S>>();
        _dbConnection = dbConnection;
        _modelTreeMap = modelTreeMap;
        _models = new List<M>();
    }

    public async Task<(List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
        ExecuteAsync(SqlStructure parameters, CancellationToken cancellationToken)
    {
        var types = new List<Type>()
        { };

        if (parameters != null)
        {
            types.Add(typeof(TotalPageRecords));
            types.Add(typeof(TotalRecordCount));
            if (parameters.HasTotalCount && parameters.HasPagination)
            {
                parameters.SplitOnDapper.Insert(0, "RowNumber");
            }
        }
        
        types.AddRange(_modelTreeMap.EntityTypes.Select(t => t.GetType()));
        var typesToMap = new List<Type>();

        if (parameters == null)
        {
            return ([], 0, 0, 0, 0);
        }

        foreach (var splitOnPart in parameters.SplitOnDapper)
        {
            typesToMap.Add(types.First(t =>
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
            
            await dbTransaction.CommitAsync(cancellationToken);

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