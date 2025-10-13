using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class ContractQueryMapping
{
    public static void MapFromCustomer(List<Customer> models, object mappedObject, IMapper mapper)
    {
        if (mappedObject is DatabaseEntity.Contract)
        {
            var contractEntity = mappedObject as DatabaseEntity.Contract;

            var index = models.Where(c => c.Product != null).ToList().FindIndex(c =>
                c.Product.Any(cbr => cbr.ContractKey == contractEntity.ContractKey));
            
            if (index >= 0)
            {
                models[index].Product = models[index].Product ?? [];
                var indexContract = models[index].Product
                    .FindIndex(x => x.ContractKey == contractEntity.ContractKey);

                if (indexContract >= 0)
                {
                    mapper.Map(contractEntity, models[indexContract]);
                }
                else
                {
                    var product = new Product();
                    product = mapper.Map(contractEntity,
                        product);
                    models[index].Product.Add(product);
                }
            }
            else
            {
                var product = new Product();
                product = mapper.Map(contractEntity,
                    product);
                ProductQueryMapping.MapFromCustomer(models, product, mapper);
            }
        }
    }

    public static void MapProduct(List<Product> models, object mappedObject, IMapper mapper)
    {
        Product model = null!;
        if (mappedObject is DatabaseEntity.Contract)
        {
            var contractModel = mappedObject as DatabaseEntity.Contract;

            var index = models.FindIndex(x => x.ContractKey == contractModel.ContractKey);

            if (index >= 0)
            {
                if (index >= 0)
                {
                    models[index] = mapper.Map(contractModel,
                        models[index]);
                }
                else
                {
                    var product = new Product();
                    product = mapper.Map(contractModel,
                        product);
                    models[index] = product;
                }
            }
        }
    }
}