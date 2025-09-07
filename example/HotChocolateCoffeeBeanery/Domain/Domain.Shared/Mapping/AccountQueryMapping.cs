using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class AccountQueryMapping
{
    public static void MapFromCustomer(List<Customer> models, object mappedObject, IMapper mapper)
    {
        if (mappedObject is DatabaseEntity.Account)
        {
            var accountEntity = mappedObject as DatabaseEntity.Account;

            var index = models.Where(c => c.Product != null).ToList().FindIndex(c =>
                c.Product.Any(cbr => cbr.AccountKey == accountEntity.AccountKey));
            
            if (index >= 0)
            {
                models[index].Product = models[index].Product ?? [];
                var indexAccount = models[index].Product
                    .FindIndex(x => x.AccountKey == accountEntity.AccountKey);

                if (indexAccount >= 0)
                {
                    mapper.Map(accountEntity, models[indexAccount]);
                }
                else
                {
                    var product = new Product();
                    product = mapper.Map(accountEntity,
                        product);
                    models[index].Product.Add(product);
                }
            }
            else
            {
                var customer = new Customer();
                customer.Product = new List<Product>();
                var product = new Product();
                product = mapper.Map(accountEntity,
                    product);
                customer.Product.Add(product);
                models.Add(customer);
            }
        }
    }

    public static void MapProduct(List<Product> models, object mappedObject, IMapper mapper)
    {
        Product model = null!;
        if (mappedObject is DatabaseEntity.Account)
        {
            var accountModel = mappedObject as DatabaseEntity.Account;

            var index = models.FindIndex(x => x.AccountKey == accountModel.AccountKey);

            if (index >= 0)
            {
                if (index >= 0)
                {
                    models[index] = mapper.Map(accountModel,
                        models[index]);
                }
                else
                {
                    var product = new Product();
                    product = mapper.Map(accountModel,
                        product);
                    models[index] = product;
                }
            }
        }
    }
}