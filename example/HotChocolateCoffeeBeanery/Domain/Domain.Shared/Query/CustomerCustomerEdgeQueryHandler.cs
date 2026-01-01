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

public class CustomerCustomerEdgeQueryHandler<M> : ProcessQuery<M>, IQuery<SqlStructure,
    (List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
    where M : class
{
    private readonly IMapper _mapper;

    public CustomerCustomerEdgeQueryHandler(ILoggerFactory loggerFactory, NpgsqlConnection dbConnection,
        IMapper mapper) : base(loggerFactory, dbConnection)
    {
        _mapper = mapper;
    }

    public override (List<M> models, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)
        MappingConfiguration(List<M> models, SqlStructure sqlStructure, object[] map)
    {
        var customerCustomerEdges = models.OfType<CustomerCustomerEdge>().ToList();
        var rowNumber = 0;
        var totalCount = 0;
        var pageRecords = 0;
        var customerCustomerEdge = new CustomerCustomerEdge();
        var product = new Product();
        
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
            else if (map[i] is DatabaseEntity.CustomerCustomerRelationship)
            {
                customerCustomerEdge = CustomerCustomerRelationshipQueryMapping
                    .MapCustomerCustomerRelationship(customerCustomerEdges, map[i], _mapper);
            }
            else if (map[i] is DatabaseEntity.Customer)
            {
                customerCustomerEdge = CustomerQueryMapping.MapCustomer(customerCustomerEdges, map[i], _mapper);
            }
            else if (map[i] is DatabaseEntity.ContactPoint)
            {
                customerCustomerEdge = ContactPointQueryMapping.MapFromCustomer(map[i], _mapper, customerCustomerEdge);
            }
            else if (map[i] is DatabaseEntity.CustomerBankingRelationship)
            {
                var result = CustomerBankingRelationshipQueryMapping
                    .MapFromCustomer(map[i], _mapper, customerCustomerEdge, product);
                customerCustomerEdge = result.existingCustomerCustomerEdge;
                product = result.existingProduct;
            }
            else if (map[i] is DatabaseEntity.Contract)
            {
                var result = ContractQueryMapping.MapFromCustomer(map[i], _mapper, 
                    customerCustomerEdge, product);
                customerCustomerEdge = result.existingCustomerCustomerEdge;
                product = result.existingProduct;
            }
            else if (map[i] is DatabaseEntity.Account)
            {
                var result = AccountQueryMapping.MapFromCustomer(map[i], _mapper, 
                    customerCustomerEdge, product);
                customerCustomerEdge = result.existingCustomerCustomerEdge;
                product = result.existingProduct;
            }
            else if (map[i] is DatabaseEntity.Transaction)
            {
                var result = TransactionQueryMapping.MapFromCustomer(map[i], _mapper, 
                    customerCustomerEdge, product);
                customerCustomerEdge = result.existingCustomerCustomerEdge;
                product = result.existingProduct;
            }
        }  
        
        var existingCustomerIndex = customerCustomerEdges.FindIndex(c => 
            c.InnerCustomer.CustomerKey == customerCustomerEdge.InnerCustomer?.CustomerKey);
        if (existingCustomerIndex >= 0)
        {
            customerCustomerEdges[existingCustomerIndex]  = customerCustomerEdge;
        }
        else
        {
            customerCustomerEdges.Add(customerCustomerEdge);
        }
        
        dynamic list = customerCustomerEdges;
        return (list, sqlStructure.Pagination?.StartCursor, sqlStructure.Pagination?.EndCursor, 
            totalCount, pageRecords);
    }
}