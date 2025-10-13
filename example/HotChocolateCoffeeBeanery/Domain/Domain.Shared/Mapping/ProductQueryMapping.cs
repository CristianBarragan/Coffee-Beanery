using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class ProductQueryMapping
{
    public static void MapFromCustomer(List<Customer> models, object mappedObject, IMapper mapper)
    {
        if (mappedObject is DatabaseEntity.CustomerBankingRelationship)
        {
            HandleCustomerBankingRelationship(models, mappedObject, mapper);
        }
        
        if (mappedObject is DatabaseEntity.Contract)
        {
            HandleContract(models, mappedObject, mapper);
        }
        
        if (mappedObject is DatabaseEntity.Account)
        {
            HandleAccount(models, mappedObject, mapper);
        }
    }

    private static void HandleCustomerBankingRelationship(List<Customer> models, object mappedObject, IMapper mapper)
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
            var customer = models.FirstOrDefault(c => c.CustomerKey.Value == customerBankingRelationshipEntity.CustomerKey);

            if (customer != null)
            {
                customer.Product.Add(product);
            }
            else
            {
                models.Add(new Customer()
                {
                    Product = new List<Product>()
                    {
                        product
                    }
                });
            }
        }
    }
    
    private static void HandleContract(List<Customer> models, object mappedObject, IMapper mapper)
    {
        var contractEntity = mappedObject as DatabaseEntity.Contract;

        var index = models.Where(c => c.Product != null).ToList().FindIndex(c =>
            c.Product.Any(cbr => cbr.CustomerBankingRelationshipKey == contractEntity.CustomerBankingRelationshipKey));
            
        var product = new Product();
        product = mapper.Map(contractEntity,
            product);
        
        if (index >= 0)
        {
            models[index].Product = models[index].Product ?? [];
            var indexContract = models[index].Product
                .FindIndex(x => x.CustomerBankingRelationshipKey == contractEntity.CustomerBankingRelationshipKey);
            
            if (indexContract >= 0)
            {
                models[index].Product[indexContract] = product;
            }
            else
            {
                models[index].Product.Add(product);
            }
        }
        else
        {
            var customerModel = models.FirstOrDefault(cb => cb.Product.Any(p => p.CustomerBankingRelationshipKey == contractEntity.CustomerBankingRelationshipKey));

            if (customerModel != null)
            {
                var productIndex = customerModel
                    .Product.FindIndex(x => x.ContractKey == contractEntity.ContractKey);

                if (productIndex >= 0)
                {
                    product = models.FirstOrDefault(c => c.CustomerKey.Value == product.CustomerKey)
                        .Product[productIndex];
                    product = mapper.Map(contractEntity,
                        product);
                    models.FirstOrDefault(c => c.CustomerKey.Value == product.CustomerKey)
                        .Product[productIndex] = product;
                }
                else
                {
                    customerModel.Product.Add(product);
                }
            }
            else
            {
                models.Add(new Customer()
                {
                    Product = new List<Product>()
                    {
                        product
                    }
                });
            }
        }
    }
    
    private static void HandleAccount(List<Customer> models, object mappedObject, IMapper mapper)
    {
        var accountEntity = mappedObject as DatabaseEntity.Account;

        var index = models.Where(c => c.Product != null).ToList().FindIndex(c =>
            c.Product.Any(cbr => cbr.AccountKey == accountEntity.AccountKey));
            
        var product = new Product();
        product = mapper.Map(accountEntity,
            product);
        
        if (index >= 0)
        {
            models[index].Product = models[index].Product ?? [];
            var indexAccount = models[index].Product
                .FindIndex(x => x.AccountKey == accountEntity.AccountKey);
            
            if (indexAccount >= 0)
            {
                product = models[index].Product[indexAccount];
                product = mapper.Map(accountEntity,
                    product);
                models[index].Product[indexAccount] = product;
            }
            else
            {
                models[index].Product.Add(product);
            }
        }
        else
        {
            var customerModel = models.FirstOrDefault(cb => cb.Product.Any(p => p.AccountKey == accountEntity.AccountKey));

            if (customerModel != null)
            {
                var productIndex = customerModel
                    .Product.FindIndex(x => x.AccountKey == accountEntity.AccountKey);

                if (productIndex >= 0)
                {
                    product = models.FirstOrDefault(c => c.CustomerKey.Value == product.CustomerKey)
                        .Product[productIndex];
                    product = mapper.Map(accountEntity,
                        product);
                    models.FirstOrDefault(c => c.CustomerKey.Value == product.CustomerKey)
                        .Product[productIndex] = product;
                }
                else
                {
                    customerModel.Product.Add(product);
                }
            }
            else
            {
                models.Add(new Customer()
                {
                    Product = new List<Product>()
                    {
                        product
                    }
                });
            }
        }
    }
}