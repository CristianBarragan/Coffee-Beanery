using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class ContactPointQueryMapping
{
    public static void MapFromCustomer(List<Customer> models, object mappedObject, IMapper mapper)
    {
        if (mappedObject is DatabaseEntity.ContactPoint)
        {
            var contactPointModel = mappedObject as DatabaseEntity.ContactPoint;

            var index = models.FindIndex(x => x.CustomerKey == contactPointModel.CustomerKey);

            if (index >= 0)
            {
                models[index].ContactPoint = models[index].ContactPoint ?? [];
                var indexCbs = models[index].ContactPoint
                    .FindIndex(x => x.ContactPointKey == contactPointModel.ContactPointKey);

                if (indexCbs >= 0)
                {
                    models[index].ContactPoint[indexCbs] = mapper.Map(contactPointModel,
                        models[index].ContactPoint[indexCbs]);
                }
                else
                {
                    var contactPoint = new ContactPoint();
                    contactPoint = mapper.Map(contactPointModel,
                        contactPoint);
                    models[index].ContactPoint.Add(contactPoint);
                }
            }
            else
            {
                var customer = new Customer();
                customer.ContactPoint = new List<ContactPoint>();
                var contactPoint = new ContactPoint();
                contactPoint = mapper.Map(contactPointModel,
                    contactPoint);
                customer.ContactPoint.Add(contactPoint);
                models.Add(customer);
            }
        }
    }
}