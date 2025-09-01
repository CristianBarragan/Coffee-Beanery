using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class CustomerQueryMapping
{
    public static void MapCustomer(List<Customer> customers, object mappedObject, IMapper mapper)
    {
        Customer customer = null!;
        if (mappedObject is DatabaseEntity.Customer)
        {
            var customerDb = mappedObject as DatabaseEntity.Customer;

            var index = customers.FindIndex(x => x.Id == customerDb.Id);

            if (index >= 0)
            {
                customer = customers[index];
                customer = mapper.Map(customerDb, customer);
                customers[index] = customer;
            }
            else
            {
                customer = mapper.Map(customerDb, customer);
                customers.Add(customer);
            }
        }
    }
}