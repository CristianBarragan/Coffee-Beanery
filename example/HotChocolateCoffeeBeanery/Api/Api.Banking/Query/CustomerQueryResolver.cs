using CoffeeBeanery.GraphQL.Helper;
using CoffeeBeanery.GraphQL.Model;
using CoffeeBeanery.Service;
using Domain.Model;
using HotChocolate.Resolvers;
using HotChocolate.Types.Pagination;

namespace Api.Banking.Query;

[ExtendObjectType("CustomerQuery")]
public class CustomerQueryResolver : IOutputType
{
    private readonly ILogger<CustomerQueryResolver> _logger;

    public CustomerQueryResolver(ILogger<CustomerQueryResolver> logger)
    {
        _logger = logger;
    }

    [UsePaging]
    [IsProjected]
    [UseFiltering]
    [UseSorting]
    public async Task<Connection<Customer>> GetCustomer(
        [Service] IProcessService<Customer, dynamic, dynamic> service,
        [SchemaService] IResolverContext resolverContext, CancellationToken cancellationToken)
    {
        try
        {
            //might be sent by client
            var cacheKey = string.Empty;

            var set = await service.QueryProcessAsync<Customer>(cacheKey, resolverContext.Selection, nameof(Wrapper),
                CancellationToken.None);
            var recordCount = set.totalCount ?? 0;
            var pageRecords = set.totalPageRecords ?? 0;

            var connection = ContextResolverHelper.GenerateConnection(
                set.list.Select(a => new EntityNode<Customer>(a, a.Id.ToString())),
                new Pagination()
                {
                    TotalRecordCount = new TotalRecordCount()
                    {
                        RecordCount = recordCount
                    },
                    TotalPageRecords = new TotalPageRecords()
                    {
                        PageRecords = pageRecords
                    },
                    StartCursor = set.startCursor,
                    After = set.endCursor?.ToString(),
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