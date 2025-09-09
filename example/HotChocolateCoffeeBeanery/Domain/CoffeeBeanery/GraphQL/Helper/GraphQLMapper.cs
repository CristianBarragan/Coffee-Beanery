using System.Reflection;
using AutoMapper;
using CoffeeBeanery.GraphQL.Configuration;
using CoffeeBeanery.GraphQL.Extension;
using CoffeeBeanery.GraphQL.Model;

namespace CoffeeBeanery.GraphQL.Helper;

using AutoMapper.Internal;

public static class GraphQLMapper
{
    /// <summary>
    /// Method to get all mappings registered by automapper 
    /// </summary>
    /// <param name="mapper"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
    public static List<FieldMap> GetMappings<E,M>(MapperConfiguration mapper, M model, E entity)
     where E : class where M : class
    {
        var configurationProvider = mapper.Internal().GetAllTypeMaps();
        var mappingFields = new List<FieldMap>();
        var mappings = configurationProvider
            .Where(configuration => configuration.SourceType.Name.Matches(model.GetType().Name) ||
                                    configuration.SourceType.Name.Matches(entity.GetType().Name));

        foreach (var mapMapping in mappings.Select(configuration =>
                 configuration.MemberMaps))
        {
            foreach (var mapping in mapMapping)
            {
                var isDestinationEntity = !mapping.TypeMap.DestinationType.Assembly.GetName()
                    .Name.Matches(model.GetType().Assembly.GetName().Name);

                if (string.IsNullOrEmpty(mapping.GetSourceMemberName()) ||
                    (mappingFields.Any(a => a.FieldDestinationName.Matches(mapping.DestinationName))) ||
                    (mappingFields.Any(a => a.FieldSourceName.Matches(mapping.GetSourceMemberName()))))
                {
                    continue;
                }

                var processingFieldMap = new FieldMap()
                {
                    FieldSourceName = isDestinationEntity ? mapping.GetSourceMemberName() : mapping.DestinationName,
                    FieldSourceType = isDestinationEntity ? mapping.TypeMap.SourceType : mapping.TypeMap.DestinationType,
                    SourceModel = isDestinationEntity ? mapping.TypeMap.SourceType.Name : mapping.TypeMap.DestinationType.Name,
                    FieldDestinationName = !isDestinationEntity ? mapping.GetSourceMemberName() : mapping.DestinationName,
                    FieldDestinationType = !isDestinationEntity ? mapping.TypeMap.SourceType : mapping.TypeMap.DestinationType,
                    DestinationEntity = !isDestinationEntity ? mapping.TypeMap.SourceType.Name : mapping.TypeMap.DestinationType.Name
                };
                
                var propertyModelAttributeType = model.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));
                
                var entityNodeType = Type.GetType($"{entity.GetType().Namespace}.{processingFieldMap.DestinationEntity},{entity.GetType().Assembly}");
                var entityVariable = (E)Activator.CreateInstance(entityNodeType);
                
                var propertyEntityAttributeType = entityVariable.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldDestinationName));
                
                if (propertyModelAttributeType == null || propertyEntityAttributeType == null)
                {
                    continue;
                }

                var nonNullableEntityType = Nullable.GetUnderlyingType(propertyEntityAttributeType.PropertyType) ?? propertyEntityAttributeType.PropertyType;
                
                if (nonNullableEntityType != null && nonNullableEntityType.IsEnum || propertyEntityAttributeType.PropertyType.IsEnum)
                {
                    var enumDictionary = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    var k = 0;
                    foreach (var value in Enum.GetValues(nonNullableEntityType))
                    {
                        enumDictionary.Add(value.ToString()!, k.ToString());
                        k++;
                    }
                    processingFieldMap.IsEnum = true;
                    processingFieldMap.DestinationEnumerationValues = enumDictionary;
                }
                
                var upsertAttribute = propertyModelAttributeType.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(UpsertKeyAttribute));
                processingFieldMap.IsUpsertKey = upsertAttribute != null;
                processingFieldMap.FieldDestinationSchema = upsertAttribute != null ? upsertAttribute.ConstructorArguments[1].Value.ToString() : string.Empty;
                processingFieldMap.IsJoinKey =  propertyModelAttributeType.CustomAttributes.Any(a => a.AttributeType == typeof(JoinKeyAttribute));
                mappingFields!.Add(processingFieldMap);
            }
        }

        return mappingFields;
    }
}