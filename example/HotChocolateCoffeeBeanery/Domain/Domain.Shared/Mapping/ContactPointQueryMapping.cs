using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class ContactPointQueryMapping
{
    public static CustomerCustomerEdge MapFromCustomer(object mappedObject, IMapper mapper, CustomerCustomerEdge? existingCustomerCustomerEdge)
    {
        if (mappedObject is DatabaseEntity.ContactPoint)
        {
            var contactPointEntity = mappedObject as DatabaseEntity.ContactPoint;

            if (existingCustomerCustomerEdge.InnerCustomer?.ContactPoint != null)
            {
                existingCustomerCustomerEdge.InnerCustomer.ContactPoint ??= [];
                
                var existingContactPoint = existingCustomerCustomerEdge.InnerCustomer.ContactPoint.FirstOrDefault(a => a.ContactPointKey == contactPointEntity!.ContactPointKey);
                
                if (existingContactPoint?.CustomerKey != null)
                {
                    mapper.Map(contactPointEntity, existingContactPoint);
                }
                else
                {
                    var contactPoint = new ContactPoint();
                    contactPoint = mapper.Map<ContactPoint>(contactPoint);
                    existingCustomerCustomerEdge.InnerCustomer.ContactPoint.Add(contactPoint);
                }
            }
            else
            {
                existingCustomerCustomerEdge.InnerCustomer = new Customer();
                existingCustomerCustomerEdge.InnerCustomer.ContactPoint = [];
                var contactPoint = new ContactPoint();
                contactPoint = mapper.Map(contactPointEntity,
                    contactPoint);
                existingCustomerCustomerEdge.InnerCustomer.ContactPoint.Add(contactPoint);
            }
        }

        return existingCustomerCustomerEdge;
    }
}