using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class CustomerBankingRelationshipQueryMapping
{
    public static (CustomerCustomerEdge existingCustomerCustomerEdge, Product existingProduct) 
        MapFromCustomer(object mappedObject, IMapper mapper, 
        CustomerCustomerEdge? existingCustomerCustomerEdge, Product? existingProduct)
    {
        if (mappedObject is DatabaseEntity.CustomerBankingRelationship)
        {
            var customerBankingRelationshipEntity = mappedObject as DatabaseEntity.CustomerBankingRelationship;
            
            if (existingCustomerCustomerEdge.InnerCustomer?.CustomerKey != null)
            {
                if (existingProduct?.CustomerKey != null)
                {
                    existingProduct.CustomerBankingRelationship ??= new CustomerBankingRelationship();
                    var existingCustomerBankingRelationship = existingProduct.CustomerBankingRelationship
                        .CustomerBankingRelationshipKey;

                    if (existingCustomerBankingRelationship != null)
                    {
                        mapper.Map(existingCustomerBankingRelationship, existingProduct);
                    }
                    else
                    {
                        existingProduct = mapper.Map<Product>(customerBankingRelationshipEntity);
                    }
                    
                    existingCustomerCustomerEdge.InnerCustomer.Product ??= [];
                    existingCustomerCustomerEdge.InnerCustomer.Product!.Add(existingProduct);
                }
                else
                {
                    existingProduct = mapper.Map<Product>(customerBankingRelationshipEntity);
                    existingCustomerCustomerEdge.InnerCustomer.Product ??= [];
                    existingCustomerCustomerEdge.InnerCustomer.Product.Add(existingProduct);
                }
            }
            else
            {
                existingCustomerCustomerEdge.InnerCustomer = new Customer();              
                if (existingProduct?.CustomerKey != null)
                {
                    existingProduct.CustomerBankingRelationship ??= new CustomerBankingRelationship();
                    var existingCustomerBankingRelationship = existingProduct.CustomerBankingRelationship
                        .CustomerBankingRelationshipKey;

                    if (existingCustomerBankingRelationship != null)
                    {
                        mapper.Map(existingCustomerBankingRelationship, existingProduct);
                    }
                    else
                    {
                        existingProduct = mapper.Map<Product>(customerBankingRelationshipEntity);
                    }
                    
                    existingCustomerCustomerEdge.InnerCustomer.Product ??= [];
                    existingCustomerCustomerEdge.InnerCustomer.Product!.Add(existingProduct);
                }
                else
                {
                    existingProduct = mapper.Map<Product>(customerBankingRelationshipEntity);
                    existingCustomerCustomerEdge.InnerCustomer.Product ??= [];
                    existingCustomerCustomerEdge.InnerCustomer.Product.Add(existingProduct);
                }
            }
        }
        
        return (existingCustomerCustomerEdge, existingProduct);
    }
}