using AutoMapper;
using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Model;
using CoffeeBeanery.Service;
using Domain.Model;
using Domain.Shared.Mapping;
using Microsoft.Extensions.Logging;
using Npgsql;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Query;

public class CustomerQueryHandler<M> : ProcessQuery<M>, IQuery<SqlStructure,
    (List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
    where M : class
{
    private readonly IMapper _mapper;

    public CustomerQueryHandler(ILoggerFactory loggerFactory, NpgsqlConnection dbConnection,
        IMapper mapper) : base(loggerFactory, dbConnection)
    {
        _mapper = mapper;
    }

    public override (List<M> models, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)
        mappingConfiguration(List<M> models, SqlStructure sqlStructure, object[] map, List<Type> typesToMap)
    {
        var customers = models.OfType<Customer>().ToList();
        var rowNumber = 0;
        var totalCount = 0;
        var pageRecords = 0;

        for (int i = 0; i < map.Length; i++)
        {
            if (map[i] is TotalPageRecords)
            {
                pageRecords = (map[i] as TotalPageRecords).PageRecords;
            }
            else if (map[i] is TotalRecordCount)
            {
                totalCount = (map[i] as TotalRecordCount).RecordCount;
            }
            else if (map[i] is DatabaseEntity.Customer)
            {
                CustomerQueryMapping.MapCustomer(customers, map[i], _mapper);
            }
            else if (map[i] is DatabaseEntity.ContactPoint)
            {
                ContactPointQueryMapping.MapFromCustomer(customers, map[i], _mapper);
            }
            else if (map[i] is DatabaseEntity.Transaction)
            {
                TransactionQueryMapping.MapFromCustomer(customers, map[i], _mapper);
            }
            else {
                ProductQueryMapping.MapFromCustomer(customers, map[i], _mapper);
            }
        }

        dynamic list = customers;
        return (list, sqlStructure.Pagination?.StartCursor, sqlStructure.Pagination?.EndCursor, totalCount,
            pageRecords);
    }
}