using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class CustomerCustomerRelationshipQueryMapping
{
    public static CustomerCustomerEdge MapCustomerCustomerRelationship(List<CustomerCustomerEdge> customerCustomerEdges, object mappedObject, IMapper mapper)
    {
        Customer existingCustomer = null;
        if (mappedObject is DatabaseEntity.Customer)
        {
            var customerDb = mappedObject as DatabaseEntity.Customer;

            existingCustomer = customerCustomerEdges.First(x => x.OuterCustomerKey == customerDb?.CustomerKey).InnerCustomer;

            if (existingCustomer?.CustomerKey != null)
            {
                existingCustomer = mapper.Map(customerDb, existingCustomer);
            }
            else
            {
                var customer = mapper.Map<Customer>(customerDb);
                customerCustomerEdges.First().InnerCustomer = customer;
                existingCustomer = customer;
            }
        }

        return default;
        // return existingCustomer;
    }
}