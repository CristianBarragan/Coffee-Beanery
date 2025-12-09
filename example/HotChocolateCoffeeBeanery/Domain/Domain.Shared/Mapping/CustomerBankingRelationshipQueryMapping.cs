using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class CustomerBankingRelationshipQueryMapping
{
    public static (Customer existingCustomer, Product existingProduct) MapFromCustomer(object mappedObject, IMapper mapper, Customer? existingCustomer, Product? existingProduct)
    {
        if (mappedObject is DatabaseEntity.CustomerBankingRelationship)
        {
            var customerBankingRelationshipEntity = mappedObject as DatabaseEntity.CustomerBankingRelationship;
            
            if (existingCustomer?.CustomerKey != null)
            {
                if (existingProduct?.CustomerKey != null)
                {
                    existingProduct.CustomerBankingRelationship ??= [];
                    var existingCustomerBankingRelationship = existingProduct.CustomerBankingRelationship
                        .FirstOrDefault(x => x.CustomerBankingRelationshipKey == customerBankingRelationshipEntity?.CustomerBankingRelationshipKey);

                    if (existingCustomerBankingRelationship != null)
                    {
                        mapper.Map(existingCustomerBankingRelationship, existingProduct);
                    }
                    else
                    {
                        existingProduct = mapper.Map<Product>(customerBankingRelationshipEntity);
                    }
                    
                    existingCustomer.Product ??= [];
                    existingCustomer.Product!.Add(existingProduct);
                }
                else
                {
                    existingProduct = mapper.Map<Product>(customerBankingRelationshipEntity);
                    existingCustomer.Product ??= [];
                    existingCustomer.Product.Add(existingProduct);
                }
            }
            else
            {
                existingCustomer = new Customer();
                existingCustomer.Product = new List<Product>();
                
                if (existingProduct?.CustomerKey != null)
                {
                    existingProduct.CustomerBankingRelationship ??= [];
                    var existingCustomerBankingRelationship = existingProduct.CustomerBankingRelationship
                        .FirstOrDefault(x => x.CustomerBankingRelationshipKey == customerBankingRelationshipEntity?.CustomerBankingRelationshipKey);

                    if (existingCustomerBankingRelationship != null)
                    {
                        mapper.Map(existingCustomerBankingRelationship, existingProduct);
                    }
                    else
                    {
                        existingProduct = mapper.Map<Product>(customerBankingRelationshipEntity);
                    }
                    
                    existingCustomer.Product ??= [];
                    existingCustomer.Product!.Add(existingProduct);
                }
                else
                {
                    existingProduct = mapper.Map<Product>(customerBankingRelationshipEntity);
                    existingCustomer.Product ??= [];
                    existingCustomer.Product.Add(existingProduct);
                }
            }
        }
        
        return (existingCustomer, existingProduct);
    }
}