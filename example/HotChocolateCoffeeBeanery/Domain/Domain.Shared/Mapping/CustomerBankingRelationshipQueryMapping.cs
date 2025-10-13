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
            var customerBankingRelationshipEntity = mappedObject as DatabaseEntity.CustomerBankingRelationship;

            var index = models.Where(c => c.Product != null).ToList().FindIndex(c =>
                c.Product.Any(cbr => cbr.CustomerBankingRelationshipKey == customerBankingRelationshipEntity.CustomerBankingRelationshipKey));
            
            var product = new Product();
            product = mapper.Map(customerBankingRelationshipEntity,
                product);
            
            if (index >= 0)
            {
                models[index].Product = models[index].Product ?? [];
                var indexCustomerBankingRelationship = models[index].Product
                    .FindIndex(x => x.CustomerBankingRelationshipKey == customerBankingRelationshipEntity.CustomerBankingRelationshipKey);

                if (indexCustomerBankingRelationship >= 0)
                {
                    models[index].Product[indexCustomerBankingRelationship] = product;
                }
                else
                {
                    models[index].Product.Add(product);
                }
            }
            else
            {
                ProductQueryMapping.MapFromCustomer(models, product, mapper);
            }
        }
    }
}