using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class AccountQueryMapping
{
    public static (Customer existingCustomer, Product existingProduct) MapFromCustomer(
        object mappedObject, IMapper mapper, Customer? existingCustomer, Product? existingProduct)
    {
        if (mappedObject is DatabaseEntity.Account)
        {
            var accountEntity = mappedObject as DatabaseEntity.Account;
            
            if (existingCustomer?.CustomerKey != null)
            {
                if (existingProduct?.CustomerKey != null)
                {
                    mapper.Map(accountEntity, existingProduct);
                    var productIndex = existingCustomer.Product?.FindIndex(a => a.CustomerKey == existingProduct?.CustomerKey);
                    existingProduct.CustomerKey = existingCustomer.CustomerKey;
                    existingCustomer.Product ??= [];
                    mapper.Map(accountEntity, existingProduct);
                    existingCustomer.Product[productIndex!.Value] = existingProduct;
                }
                else
                {
                    existingProduct = mapper.Map<Product>(accountEntity);
                    existingCustomer.Product ??= [];
                    existingProduct.CustomerKey = existingCustomer.CustomerKey;
                    mapper.Map(accountEntity, existingProduct);
                    existingCustomer.Product.Add(existingProduct);
                }
            }
            else
            {
                existingCustomer ??= new Customer();

                if (existingProduct == null)
                {
                    existingProduct = mapper.Map<Product>(accountEntity);
                    existingProduct.CustomerKey = existingCustomer.CustomerKey;
                    existingCustomer.Product ??= [];
                    existingCustomer.Product.Add(existingProduct);
                }
                else
                {
                    mapper.Map(accountEntity, existingProduct);
                }
            }
        }
        return (existingCustomer, existingProduct);
    }
}