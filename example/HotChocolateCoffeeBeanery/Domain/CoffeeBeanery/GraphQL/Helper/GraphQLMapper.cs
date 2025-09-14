using System.Collections;
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
    public static List<FieldMap> GetMappings<E,M>(MapperConfiguration mapper, E from, M to, bool isModel,
        string entityNamespaceName, List<string> models, Dictionary<string, SqlNode>? linkEntityDictionaryTree, 
        List<LinkKey> linkKeys)
     where E : class where M : class
    {
        var configurationProvider = mapper.Internal().GetAllTypeMaps();
        var mappingFields = new List<FieldMap>();
        
        var mappings = configurationProvider
            .Where(configuration => configuration.SourceType.Name.Matches(to.GetType().Name));

        foreach (var mapMapping in mappings.Select(configuration =>
                 configuration.MemberMaps))
        {
            foreach (var mapping in mapMapping)
            {
                var isDestinationEntity = !mapping.TypeMap.DestinationType.Assembly.GetName()
                    .Name.Matches(to.GetType().Assembly.GetName().Name);

                if (string.IsNullOrEmpty(mapping.GetSourceMemberName()) ||
                    (mappingFields.Any(a => a.FieldDestinationName.Matches(mapping.DestinationName))) ||
                    (mappingFields.Any(a => a.FieldSourceName.Matches(mapping.GetSourceMemberName()))))
                {
                    continue;
                }

                var processingFieldMap = new FieldMap()
                {
                    FieldSourceName = isDestinationEntity && !isModel ? mapping.GetSourceMemberName() : mapping.DestinationName,
                    FieldSourceType = isDestinationEntity && !isModel ? mapping.TypeMap.SourceType : mapping.TypeMap.DestinationType,
                    SourceModel = isDestinationEntity && !isModel ? mapping.TypeMap.SourceType.Name : mapping.TypeMap.DestinationType.Name,
                    FieldDestinationName = !isDestinationEntity && isModel ? mapping.GetSourceMemberName() : mapping.DestinationName,
                    FieldDestinationType = !isDestinationEntity && isModel ? mapping.TypeMap.SourceType : mapping.TypeMap.DestinationType,
                    DestinationEntity = !isDestinationEntity && isModel ? mapping.TypeMap.SourceType.Name : mapping.TypeMap.DestinationType.Name
                };
                
                var propertyToAttributeType = to.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));
                
                var entityNodeType = Type.GetType($"{from.GetType().Namespace}.{processingFieldMap.DestinationEntity},{from.GetType().Assembly}");

                if (entityNodeType == null)
                {
                    return new List<FieldMap>();
                }
                
                var entityVariable = (E)Activator.CreateInstance(entityNodeType);
                
                var propertyFromAttributeType = entityVariable.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldDestinationName));
                
                if (propertyToAttributeType == null || propertyFromAttributeType == null)
                {
                    continue;
                }

                var nonNullableFromType = Nullable.GetUnderlyingType(propertyFromAttributeType
                    .PropertyType) ?? propertyFromAttributeType.PropertyType;
                var nonNullableToType = Nullable.GetUnderlyingType(propertyToAttributeType
                    .PropertyType) ?? propertyToAttributeType.PropertyType;
                
                var enumDictionaryFrom = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                
                if (nonNullableFromType != null && nonNullableFromType.IsEnum || 
                    propertyFromAttributeType.PropertyType.IsEnum)
                {
                    var k = 0;
                    foreach (var value in Enum.GetValues(nonNullableFromType))
                    {
                        enumDictionaryFrom.Add(value.ToString()!, k.ToString());
                        k++;
                    }
                }

                var enumDictionaryTo = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                
                if (propertyToAttributeType != null && nonNullableToType.IsEnum || 
                    propertyToAttributeType.PropertyType.IsEnum)
                {
                    var k = 0;
                    foreach (var value in Enum.GetValues(nonNullableToType))
                    {
                        enumDictionaryTo.Add(value.ToString()!, k.ToString());
                        k++;
                    }
                }

                var linkKeyDestinationProperty = models.Contains(processingFieldMap.FieldDestinationName)
                    ? $"{processingFieldMap.FieldDestinationName}Key" : processingFieldMap.FieldDestinationName;
                
                var linkKeySourceProperty = models.Contains(processingFieldMap.FieldSourceName)
                    ? $"{processingFieldMap.FieldSourceName}Key" : processingFieldMap.FieldSourceName;

                if (linkEntityDictionaryTree != null &&
                    linkEntityDictionaryTree.TryGetValue($"{processingFieldMap.DestinationEntity}~{
                        linkKeyDestinationProperty}", out var valueLinkEntity))
                {
                    valueLinkEntity.IsEnumeration = processingFieldMap.FieldSourceType.IsEnum;
                    valueLinkEntity.FromEnumeration = enumDictionaryFrom;
                    valueLinkEntity.ToEnumeration = enumDictionaryTo;
                    linkEntityDictionaryTree[$"{processingFieldMap.DestinationEntity}~{
                        linkKeyDestinationProperty}"] = valueLinkEntity;
                }
                else {
                    linkEntityDictionaryTree.Add($"{processingFieldMap.DestinationEntity}~{linkKeyDestinationProperty}", 
                        new SqlNode()
                        {
                            RelationshipKey = $"{processingFieldMap.SourceModel}~{linkKeySourceProperty}",
                            Column = linkKeyDestinationProperty,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            FromEnumeration = enumDictionaryFrom,
                            ToEnumeration = enumDictionaryTo,
                            IsModel = entityNamespaceName.Matches(from.GetType().Namespace)
                        });
                }
                
                linkKeyDestinationProperty = models.Contains(processingFieldMap.FieldSourceName)
                    ? $"{processingFieldMap.FieldSourceName}Key" : processingFieldMap.FieldSourceName;
                
                linkKeySourceProperty = models.Contains(processingFieldMap.FieldDestinationName)
                    ? $"{processingFieldMap.FieldDestinationName}Key" : processingFieldMap.FieldDestinationName;
                
                if (linkEntityDictionaryTree != null &&
                    linkEntityDictionaryTree.TryGetValue($"{processingFieldMap.SourceModel}~{
                        linkKeyDestinationProperty}", out valueLinkEntity))
                {
                    valueLinkEntity.IsEnumeration = processingFieldMap.FieldSourceType.IsEnum;
                    valueLinkEntity.FromEnumeration = enumDictionaryFrom;
                    valueLinkEntity.ToEnumeration = enumDictionaryTo;
                    linkEntityDictionaryTree[$"{processingFieldMap.SourceModel}~{
                        linkKeyDestinationProperty}"] = valueLinkEntity;
                }
                else {
                    linkEntityDictionaryTree.Add($"{processingFieldMap.SourceModel}~{linkKeyDestinationProperty}", 
                        new SqlNode()
                        {
                            RelationshipKey = $"{processingFieldMap.DestinationEntity}~{linkKeySourceProperty}",
                            Column = linkKeyDestinationProperty,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            FromEnumeration = enumDictionaryFrom,
                            ToEnumeration = enumDictionaryTo,
                            IsModel = processingFieldMap.FieldSourceName.GetType().Namespace.Matches(from.GetType().Namespace)
                        });
                }
                
                var propertyModelAttributeType = to.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));

                if (propertyModelAttributeType != null)
                {
                    var linkAttribute = propertyModelAttributeType.CustomAttributes
                        .FirstOrDefault(a => a.AttributeType == typeof(LinkKeyAttribute));
                
                    if (linkKeys != null && linkAttribute != null)
                    {
                        var linkKey = new LinkKey()
                        {
                            From = $"{processingFieldMap.SourceModel}~{linkAttribute.ConstructorArguments[1].Value.ToString()}",
                            To =
                                $"{linkAttribute.ConstructorArguments[0].Value.ToString()}~{linkAttribute.ConstructorArguments[1].Value.ToString()}"
                        };
                        linkKeys.Add(linkKey);
                    }
                }

                mappingFields!.Add(processingFieldMap);
            }
        }
        
        linkEntityDictionaryTree.Last().Value.Mapping = mappingFields;

        return mappingFields;
    }
}