using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class ContractQueryMapping
{
    public static (Customer existingCustomer, Product existingProduct) MapFromCustomer(
        object mappedObject, IMapper mapper, Customer? existingCustomer, Product? existingProduct)
    {
        if (mappedObject is DatabaseEntity.Contract)
        {
            var contractEntity = mappedObject as DatabaseEntity.Contract;
            
            if (existingCustomer?.CustomerKey != null)
            {
                existingCustomer.Product ??= [];
                
                if (existingProduct?.CustomerKey != null)
                {
                    mapper.Map(contractEntity, existingProduct);
                    var productIndex = existingCustomer.Product.FindIndex(a => a.CustomerKey == existingProduct?.CustomerKey);
                    existingProduct.CustomerKey = existingCustomer.CustomerKey;
                    existingCustomer.Product[productIndex] = existingProduct;
                }
                else
                {
                    existingProduct = mapper.Map<Product>(contractEntity);
                    existingProduct.CustomerKey = existingCustomer.CustomerKey;
                    existingCustomer.Product.Add(existingProduct);
                }
            }
            else
            {
                existingCustomer = new Customer();
                existingCustomer.Product = [];
                
                existingProduct = mapper.Map<Product>(contractEntity);
                existingProduct.CustomerKey = existingCustomer.CustomerKey;
                existingCustomer.Product.Add(existingProduct);
            }
        }

        return (existingCustomer, existingProduct);
    }
}