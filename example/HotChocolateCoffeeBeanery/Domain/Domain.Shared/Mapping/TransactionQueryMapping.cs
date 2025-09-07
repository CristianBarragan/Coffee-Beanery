using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class TransactionQueryMapping
{
    public static void MapFromCustomer(List<Customer> models, object mappedObject, IMapper mapper)
    {
        if (mappedObject is DatabaseEntity.Transaction)
        {
            // var transactionEntity = mappedObject as DatabaseEntity.Transaction;
            //
            // var index = models.Where(c => c.Product != null).ToList().FindIndex(c =>
            //     c.Product.Any(cbr => cbr.TransactionKey == transactionEntity.TransactionKey));
            //
            // if (index >= 0)
            // {
            //     models[index].Product = models[index].Product ?? [];
            //     var indexTransaction = models[index].Product
            //         .FindIndex(x => x.TransactionKey == transactionEntity.TransactionKey);
            //
            //     if (indexTransaction >= 0)
            //     {
            //         mapper.Map(transactionEntity, models[indexTransaction]);
            //     }
            //     else
            //     {
            //         var product = new Product();
            //         product = mapper.Map(transactionEntity,
            //             product);
            //         models[index].Product.Add(product);
            //     }
            // }
            // else
            // {
            //     var customer = new Customer();
            //     customer.Product = new List<Product>();
            //     var product = new Product();
            //     product = mapper.Map(transactionEntity,
            //         product);
            //     customer.Product.Add(product);
            //     models.Add(customer);
            // }
        }
    }

    public static void MapProduct(List<Product> models, object mappedObject, IMapper mapper)
    {
        Product model = null!;
        if (mappedObject is DatabaseEntity.Transaction)
        {
            // var transactionEntity = mappedObject as DatabaseEntity.Transaction;
            //
            // var index = models.FindIndex(x => x.TransactionKey == transactionEntity.TransactionKey);
            //
            // if (index >= 0)
            // {
            //     if (index >= 0)
            //     {
            //         models[index] = mapper.Map(transactionEntity,
            //             models[index]);
            //     }
            //     else
            //     {
            //         var product = new Product();
            //         product = mapper.Map(transactionEntity,
            //             product);
            //         models[index] = product;
            //     }
            // }
        }
    }
}