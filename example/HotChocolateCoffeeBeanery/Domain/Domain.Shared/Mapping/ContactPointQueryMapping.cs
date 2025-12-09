using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class ContactPointQueryMapping
{
    public static Customer MapFromCustomer(object mappedObject, IMapper mapper, Customer? existingCustomer)
    {
        if (mappedObject is DatabaseEntity.ContactPoint)
        {
            var contactPointEntity = mappedObject as DatabaseEntity.ContactPoint;

            if (existingCustomer?.ContactPoint != null)
            {
                existingCustomer.ContactPoint ??= [];
                
                var existingContactPoint = existingCustomer.ContactPoint.FirstOrDefault(a => a.ContactPointKey == contactPointEntity!.ContactPointKey);
                
                if (existingContactPoint?.CustomerKey != null)
                {
                    mapper.Map(contactPointEntity, existingContactPoint);
                }
                else
                {
                    var contactPoint = new ContactPoint();
                    contactPoint = mapper.Map<ContactPoint>(contactPoint);
                    existingCustomer.ContactPoint.Add(contactPoint);
                }
            }
            else
            {
                existingCustomer = new Customer();
                existingCustomer.ContactPoint = [];
                var contactPoint = new ContactPoint();
                contactPoint = mapper.Map(contactPointEntity,
                    contactPoint);
                existingCustomer.ContactPoint.Add(contactPoint);
            }
        }

        return existingCustomer;
    }
}