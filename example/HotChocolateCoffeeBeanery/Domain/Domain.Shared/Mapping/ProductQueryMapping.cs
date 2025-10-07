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
            
            if (index >= 0)
            {
                models[index].Product = models[index].Product ?? [];
                var indexCustomerBankingRelationship = models[index].Product
                    .FindIndex(x => x.CustomerBankingRelationshipKey == customerBankingRelationshipEntity.CustomerBankingRelationshipKey);

                if (indexCustomerBankingRelationship >= 0)
                {
                    mapper.Map(customerBankingRelationshipEntity, models[indexCustomerBankingRelationship]);
                }
                else
                {
                    var product = new Product();
                    product = mapper.Map(customerBankingRelationshipEntity,
                        product);
                    models[index].Product.Add(product);
                }
            }
            else
            {
                var customer = models.FirstOrDefault(c => c.CustomerKey.Value == customerBankingRelationshipEntity.CustomerKey);

                if (customer != null)
                {
                    var product = new Product();
                    customer.Product ??= new List<Product>();
                    product = mapper.Map(customerBankingRelationshipEntity,
                        product);
                    customer.Product.Add(product);
                }
                else
                {
                    customer = new Customer();
                    customer.Product ??= new List<Product>();
                    var product = new Product();
                    product = mapper.Map(customerBankingRelationshipEntity,
                        product);
                    customer.Product.Add(product);
                    models.Add(customer);
                }
            }
        }
    }

    public static void MapProduct(List<Product> models, object mappedObject, IMapper mapper)
    {
        Product model = null!;
        if (mappedObject is DatabaseEntity.CustomerBankingRelationship)
        {
            var customerBankingRelationshipModel = mappedObject as DatabaseEntity.CustomerBankingRelationship;

            var index = models.FindIndex(x => x.CustomerBankingRelationshipKey == customerBankingRelationshipModel.CustomerBankingRelationshipKey);

            if (index >= 0)
            {
                if (index >= 0)
                {
                    models[index] = mapper.Map(customerBankingRelationshipModel,
                        models[index]);
                }
                else
                {
                    var product = new Product();
                    product = mapper.Map(customerBankingRelationshipModel,
                        product);
                    models[index] = product;
                }
            }
        }
    }
}