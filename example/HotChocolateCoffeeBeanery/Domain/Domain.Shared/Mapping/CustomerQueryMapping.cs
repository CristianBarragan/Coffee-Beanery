using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class CustomerQueryMapping
{
    public static Customer MapCustomer(List<Customer> customers, object mappedObject, IMapper mapper)
    {
        Customer existingCustomer = null;
        if (mappedObject is DatabaseEntity.Customer)
        {
            var customerDb = mappedObject as DatabaseEntity.Customer;

            existingCustomer = customers.FirstOrDefault(x => x.CustomerKey == customerDb?.CustomerKey);

            if (existingCustomer?.CustomerKey != null)
            {
                existingCustomer = mapper.Map(customerDb, existingCustomer);
            }
            else
            {
                var customer = mapper.Map<Customer>(customerDb);
                customers.Add(customer);
                existingCustomer = customer;
            }
        }

        return existingCustomer;
    }
}