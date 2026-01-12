using CoffeeBeanery.GraphQL.Helper;
using CoffeeBeanery.GraphQL.Model;
using CoffeeBeanery.Service;
using Domain.Model;
using HotChocolate.Resolvers;
using HotChocolate.Types.Pagination;

namespace Api.Banking.Mutation;

[ExtendObjectType("CustomerCustomerEdgeMutation")]
public class CustomerCustomerEdgeMutationResolver : IInputType, IOutputType
{
    private readonly ILogger<CustomerCustomerEdgeMutationResolver> _logger;

    public CustomerCustomerEdgeMutationResolver(ILogger<CustomerCustomerEdgeMutationResolver> logger)
    {
        _logger = logger;
    }
    
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public async Task<Connection<CustomerCustomerEdge>> UpsertCustomerCustomerEdge(
        [Service] IProcessService<CustomerCustomerEdge, dynamic, dynamic> service,
        [SchemaService] IResolverContext resolverContext, Wrapper wrapper)
    {
        try
        {
            var set = await service.UpsertProcessAsync<CustomerCustomerEdge>(wrapper.CacheKey, resolverContext.Selection, 
                wrapper.Model.ToString(), nameof(Wrapper), CancellationToken.None);

            var connection = ContextResolverHelper.GenerateConnection(
                set.list.Select(a => new EntityNode<CustomerCustomerEdge>(a, wrapper.CacheKey.ToString())),
                new Pagination()
                {
                    TotalRecordCount = new TotalRecordCount()
                    {
                        RecordCount = set.totalCount ?? 0
                    },
                    TotalPageRecords = new TotalPageRecords()
                    {
                        PageRecords = set.totalPageRecords ?? 0
                    },
                    StartCursor = set.startCursor,
                    After = set.endCursor.ToString(),
                });
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception: {ex.Message} with inner exception {ex.InnerException}");
        }

        return default;
    }

    public TypeKind Kind { get; }
    public Type RuntimeType { get; }
}