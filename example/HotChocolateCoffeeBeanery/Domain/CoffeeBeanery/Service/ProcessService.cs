using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Helper;
using CoffeeBeanery.GraphQL.Model;
using Microsoft.Extensions.Logging;
using Npgsql;
using FASTER.core;
using HotChocolate.Execution.Processing;

namespace CoffeeBeanery.Service;

public interface IProcessService<M, N, S>
    where M : class where N : class where S : class
{
    Task<(List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)> QueryProcessAsync<M>(
        string cacheKey, ISelection graphQlSelection, string rootName, CancellationToken cancellationToken)
        where M : class;

    Task<(List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
        UpsertProcessAsync<M>(
            string cacheKey, ISelection graphQlSelection, string rootName, CancellationToken cancellationToken)
        where M : class;
}

public class ProcessService<M, D, S>
    : IProcessService<M, D, S>
    where M : class where D : class where S : class
{
    private readonly ILogger<ProcessService<M, D, S>> _logger;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly NpgsqlConnection _connection;
    private readonly ITreeMap<D, S> _treeMap;
    private IFasterKV<string, string> _cache;

    public ProcessService(ILogger<ProcessService<M, D, S>> logger, IQueryDispatcher queryDispatcher,
        NpgsqlConnection connection, ITreeMap<D, S> treeMap, IFasterKV<string, string> cache)
    {
        _logger = logger;
        _queryDispatcher = queryDispatcher;
        _connection = connection;
        _treeMap = treeMap;
        _cache = cache;
    }

    public virtual async Task<(List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
        QueryProcessAsync<M>(
            string cacheKey, ISelection graphQlSelection, string rootName, CancellationToken cancellationToken)
        where M : class
    {
        return await ExecuteStatementAsync<M>(cacheKey, graphQlSelection, rootName, cancellationToken);
    }

    private async Task<(List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
        ExecuteStatementAsync<M>(string cacheKey, ISelection graphQlSelection, string rootName,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(graphQlSelection.ToString()))
        {
            return default;
        }

        var sqlStructure = new SqlStructure();
        sqlStructure = SqlNodeResolverHelper.HandleGraphQL(graphQlSelection, _treeMap.DictionaryTree, rootName,
            _treeMap.EntityNames, _cache);
        //Permissions )

        return await _queryDispatcher
            .DispatchAsync<SqlStructure, (List<M> Process, int? startCursor, int? endCursor, int?
                totalCount, int? totalPageRecords)>(sqlStructure, cancellationToken);
    }

    public virtual async Task<(List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
        UpsertProcessAsync<M>(string cacheKey, ISelection graphQlSelection, string rootName,
            CancellationToken cancellationToken)
        where M : class
    {
        return await ExecuteStatementAsync<M>(cacheKey, graphQlSelection, rootName, cancellationToken);
    }

    public async Task<ProcessQueryParameters> HandleQuery<M>(SqlStructure sqlStructure,
        CancellationToken cancellationToken)
        where M : class
    {
        throw new NotImplementedException();
    }
}