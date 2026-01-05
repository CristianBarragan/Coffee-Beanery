using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class ContractQueryMapping
{
    public static (CustomerCustomerEdge existingCustomerCustomerEdge, Product existingProduct) MapFromCustomer(
        object mappedObject, IMapper mapper, CustomerCustomerEdge? existingCustomerCustomerEdge, 
        Product? existingProduct)
    {
        if (mappedObject is DatabaseEntity.Contract)
        {
            var contractEntity = mappedObject as DatabaseEntity.Contract;
            
            if (existingCustomerCustomerEdge.InnerCustomer?.CustomerKey != null)
            {
                existingCustomerCustomerEdge.InnerCustomer.Product ??= [];
                
                if (existingProduct?.CustomerKey != null)
                {
                    mapper.Map(contractEntity, existingProduct);
                    var productIndex = existingCustomerCustomerEdge.InnerCustomer.Product.FindIndex(a => a.CustomerKey == existingProduct?.CustomerKey);
                    existingProduct.CustomerKey = existingCustomerCustomerEdge.InnerCustomer.CustomerKey;
                    mapper.Map(contractEntity, existingProduct);
                    existingCustomerCustomerEdge.InnerCustomer.Product[productIndex] = existingProduct;
                }
                else
                {
                    existingProduct = mapper.Map<Product>(contractEntity);
                    existingProduct.CustomerKey = existingCustomerCustomerEdge.InnerCustomer.CustomerKey;
                    mapper.Map(contractEntity, existingProduct);
                    existingCustomerCustomerEdge.InnerCustomer.Product.Add(existingProduct);
                }
            }
            else
            {
                existingCustomerCustomerEdge.InnerCustomer = new Customer();
                existingCustomerCustomerEdge.InnerCustomer.Product = [];
                
                existingProduct = mapper.Map<Product>(contractEntity);
                existingProduct.CustomerKey = existingCustomerCustomerEdge.InnerCustomer.CustomerKey;
                mapper.Map(contractEntity, existingProduct);
                existingCustomerCustomerEdge.InnerCustomer.Product.Add(existingProduct);
            }
        }

        return (existingCustomerCustomerEdge, existingProduct);
    }
}