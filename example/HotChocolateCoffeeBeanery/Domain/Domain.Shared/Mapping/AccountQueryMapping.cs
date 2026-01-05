using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class AccountQueryMapping
{
    public static (CustomerCustomerEdge existingCustomerCustomerEdge, Product existingProduct) MapFromCustomer(
        object mappedObject, IMapper mapper, CustomerCustomerEdge? existingCustomerCustomerEdge, Product? existingProduct)
    {
        if (mappedObject is DatabaseEntity.Account)
        {
            var accountEntity = mappedObject as DatabaseEntity.Account;
            
            if (existingCustomerCustomerEdge?.InnerCustomer.CustomerKey != null)
            {
                if (existingProduct?.CustomerKey != null)
                {
                    mapper.Map(accountEntity, existingProduct);
                    var productIndex = existingCustomerCustomerEdge?.InnerCustomer.Product?.FindIndex(a => a.CustomerKey == existingProduct?.CustomerKey);
                    existingProduct.CustomerKey = existingCustomerCustomerEdge?.InnerCustomer.CustomerKey;
                    existingCustomerCustomerEdge.InnerCustomer.Product ??= [];
                    mapper.Map(accountEntity, existingProduct);
                    existingCustomerCustomerEdge.InnerCustomer.Product[productIndex!.Value] = existingProduct;
                }
                else
                {
                    existingProduct = mapper.Map<Product>(accountEntity);
                    existingCustomerCustomerEdge.InnerCustomer.Product ??= [];
                    existingProduct.CustomerKey = existingCustomerCustomerEdge.InnerCustomer.CustomerKey;
                    mapper.Map(accountEntity, existingProduct);
                    existingCustomerCustomerEdge.InnerCustomer.Product.Add(existingProduct);
                }
            }
            else
            {
                existingCustomerCustomerEdge.InnerCustomer ??= new Customer();

                if (existingProduct == null)
                {
                    existingProduct = mapper.Map<Product>(accountEntity);
                    existingProduct.CustomerKey = existingCustomerCustomerEdge.InnerCustomer.CustomerKey;
                    existingCustomerCustomerEdge.InnerCustomer.Product ??= [];
                    existingCustomerCustomerEdge.InnerCustomer.Product.Add(existingProduct);
                }
                else
                {
                    mapper.Map(accountEntity, existingProduct);
                }
            }
        }
        return (default, existingProduct);
    }
}