using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class CustomerBankingRelationshipQueryMapping
{
    public static void MapFromCustomer(List<Customer> models, object mappedObject, IMapper mapper)
    {
        if (mappedObject is DatabaseEntity.CustomerBankingRelationship)
        {
            var customerBankingRelationshipModel = mappedObject as DatabaseEntity.CustomerBankingRelationship;

            var index = models.FindIndex(x => x.Id == customerBankingRelationshipModel.CustomerId);

            if (index >= 0)
            {
                models[index].CustomerBankingRelationship = models[index].CustomerBankingRelationship ?? [];
                var indexCbs = models[index].CustomerBankingRelationship
                    .FindIndex(x => x.Id == customerBankingRelationshipModel.Id);

                if (indexCbs >= 0)
                {
                    models[index].CustomerBankingRelationship[indexCbs] = mapper.Map(customerBankingRelationshipModel,
                        models[index].CustomerBankingRelationship[indexCbs]);
                }
                else
                {
                    var customerBankingRelationship = new CustomerBankingRelationship();
                    customerBankingRelationship = mapper.Map(customerBankingRelationshipModel,
                        customerBankingRelationship);
                    models[index].CustomerBankingRelationship.Add(customerBankingRelationship);
                }
            }
            else
            {
                var customer = new Customer();
                customer.CustomerBankingRelationship = new List<CustomerBankingRelationship>();
                var customerBankingRelationship = new CustomerBankingRelationship();
                customerBankingRelationship = mapper.Map(customerBankingRelationshipModel,
                    customerBankingRelationship);
                customer.CustomerBankingRelationship.Add(customerBankingRelationship);
                models.Add(customer);
            }
        }
    }

    public static void MapCustomerBankingRelationship(List<CustomerBankingRelationship> models, object mappedObject,
        IMapper mapper)
    {
        CustomerBankingRelationship model = null!;
        if (mappedObject is DatabaseEntity.CustomerBankingRelationship)
        {
            var customerBankingRelationshipModel = mappedObject as DatabaseEntity.CustomerBankingRelationship;

            var index = models.FindIndex(x => x.Id == customerBankingRelationshipModel.Id);

            if (index >= 0)
            {
                if (index >= 0)
                {
                    models[index] = mapper.Map(customerBankingRelationshipModel,
                        models[index]);
                }
                else
                {
                    var customerBankingRelationship = new CustomerBankingRelationship();
                    customerBankingRelationship = mapper.Map(customerBankingRelationshipModel,
                        customerBankingRelationship);
                    models[index] = customerBankingRelationship;
                }
            }
        }
    }
}