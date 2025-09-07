using CoffeeBeanery.GraphQL.Helper;
using CoffeeBeanery.GraphQL.Model;
using CoffeeBeanery.Service;
using Domain.Model;
using HotChocolate.Resolvers;
using HotChocolate.Types.Pagination;

namespace Api.Banking.Mutation;

[ExtendObjectType("CustomerMutation")]
public class CustomerMutationResolver : IInputType, IOutputType
{
    private readonly ILogger<CustomerMutationResolver> _logger;

    public CustomerMutationResolver(ILogger<CustomerMutationResolver> logger)
    {
        _logger = logger;
    }

    [UsePaging]
    // [UseFiltering]
    [UseSorting]
    public async Task<Connection<Customer>> UpsertCustomer(
        [Service] IProcessService<Customer, dynamic, dynamic> service,
        [SchemaService] IResolverContext resolverContext, Wrapper wrapper)
    {
        try
        {
            //might be sent by client
            var cacheKey = wrapper.CacheKey;

            var set = await service.UpsertProcessAsync<Customer>(cacheKey, resolverContext.Selection, nameof(Wrapper),
                CancellationToken.None);

            var connection = ContextResolverHelper.GenerateConnection(
                set.list.Select(a => new EntityNode<Customer>(a, a.CustomerKey.ToString())),
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