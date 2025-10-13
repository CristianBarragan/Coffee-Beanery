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
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="isModel"></param>
    /// <param name="models"></param>
    /// <param name="entities"></param>
    /// <param name="entityNamespace"></param>
    /// <param name="linkEntityDictionaryTree"></param>
    /// <param name="linkModelDictionaryTree"></param>
    /// <param name="linkKeys"></param>
    /// <param name="linkBusinessKeys"></param>
    /// <typeparam name="E"></typeparam>
    /// <typeparam name="M"></typeparam>
    /// <returns></returns>
    public static List<FieldMap> GetMappings<E,M>(MapperConfiguration mapper, E from, M to, bool isModel,
        List<string> models, List<string> entities, string entityNamespace, Dictionary<string, SqlNode>? linkEntityDictionaryTree, 
        Dictionary<string, SqlNode>? linkModelDictionaryTree, 
        List<LinkKey> linkKeys, List<LinkBusinessKey> linkBusinessKeys)
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
                    FieldSourceName = mapping.GetSourceMemberName(),
                    FieldSourceType = mapping.TypeMap.SourceType,
                    SourceModel = mapping.TypeMap.SourceType.Name,
                    FieldDestinationName = mapping.DestinationName,
                    FieldDestinationType = mapping.TypeMap.DestinationType,
                    DestinationEntity = mapping.TypeMap.DestinationType.Name
                };

                if (mappingFields.Any(a => a.FieldDestinationName.Matches(processingFieldMap.FieldDestinationName) &&
                                           a.FieldSourceName.Matches(processingFieldMap.FieldSourceName)))
                {
                    continue;
                }
                mappingFields!.Add(processingFieldMap);
                
                var enumDictionaryTo = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                var enumDictionaryFrom = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                var linkKeyDestinationProperty = models.Contains(processingFieldMap.FieldDestinationName)
                    ? $"{processingFieldMap.FieldDestinationName}Key" : processingFieldMap.FieldDestinationName;
                
                var linkKeySourceProperty = models.Contains(processingFieldMap.FieldSourceName)
                    ? $"{processingFieldMap.FieldSourceName}Key" : processingFieldMap.FieldSourceName;

                if (!isModel && to.GetType().Namespace.Matches(entityNamespace))
                {
                    if (linkModelDictionaryTree != null &&
                        !linkModelDictionaryTree.TryGetValue($"{processingFieldMap.DestinationEntity}~{
                            linkKeyDestinationProperty}", out _))
                    {
                        linkModelDictionaryTree.Add($"{processingFieldMap.DestinationEntity}~{linkKeyDestinationProperty}", 
                            new SqlNode()
                            {
                                RelationshipKey = $"{processingFieldMap.SourceModel}~{linkKeySourceProperty}",
                                Column = linkKeySourceProperty,
                                IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                                FromEnumeration = enumDictionaryFrom,
                                ToEnumeration = enumDictionaryTo
                            });

                        if (linkEntityDictionaryTree != null &&
                            !linkEntityDictionaryTree.TryGetValue($"{processingFieldMap.SourceModel}~{linkKeySourceProperty}", out _))
                        {
                            linkEntityDictionaryTree.Add($"{processingFieldMap.SourceModel}~{linkKeySourceProperty}", 
                                new SqlNode()
                                {
                                    RelationshipKey = $"{processingFieldMap.DestinationEntity}~{linkKeyDestinationProperty}",
                                    Column = linkKeySourceProperty,
                                    IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                                    FromEnumeration = enumDictionaryFrom,
                                    ToEnumeration = enumDictionaryTo
                                });    
                        }
                        
                        
                    }
                }
                
                var propertyToAttributeType = to.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));
                
                if (propertyToAttributeType == null)
                {
                    propertyToAttributeType = to.GetType().GetProperties()
                        .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldDestinationName));
                }
                
                if (propertyToAttributeType != null)
                {
                    var nonNullableToType = Nullable.GetUnderlyingType(propertyToAttributeType
                        .PropertyType) ?? propertyToAttributeType.PropertyType;
                    
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
                    
                    var propertyModelAttributeType = to.GetType().GetProperties()
                        .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));

                    if (propertyModelAttributeType != null)
                    {
                        var linkBusinessAttribute = propertyModelAttributeType.CustomAttributes
                            .FirstOrDefault(a => a.AttributeType == typeof(LinkBusinessKeyAttribute));
                
                        if (linkBusinessKeys != null && linkBusinessAttribute != null)
                        {
                            var linkBusinessKey = new LinkBusinessKey()
                            {
                                From = $"{processingFieldMap.SourceModel}~{linkBusinessAttribute.ConstructorArguments[1].Value.ToString()}",
                                To =
                                    $"{linkBusinessAttribute.ConstructorArguments[0].Value.ToString()}~{linkBusinessAttribute.ConstructorArguments[1].Value.ToString()}"
                            };
                            linkBusinessKeys.Add(linkBusinessKey);
                        }
                        
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
                }
                
                var entityNodeType = Type.GetType($"{from.GetType().Namespace}.{processingFieldMap.FieldSourceName},{from.GetType().Assembly}");

                if (entityNodeType == null)
                {
                    entityNodeType = Type.GetType($"{from.GetType().Namespace}.{processingFieldMap.FieldDestinationName},{from.GetType().Assembly}");
                }
                if (entityNodeType == null)
                {
                    continue;
                }
                
                var entityVariable = (E)Activator.CreateInstance(entityNodeType);
                var propertyFromAttributeType = entityVariable.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));

                if (propertyFromAttributeType == null)
                {
                    entityVariable = (E)Activator.CreateInstance(entityNodeType);
                    propertyFromAttributeType = entityVariable.GetType().GetProperties()
                        .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldDestinationName));
                }
                
                if (propertyFromAttributeType != null)
                {
                    var nonNullableFromType = Nullable.GetUnderlyingType(propertyFromAttributeType
                        .PropertyType) ?? propertyFromAttributeType.PropertyType;
                
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
                    
                    var propertyEntityAttributeType = from.GetType().GetProperties()
                        .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldDestinationName));
                
                    if (propertyEntityAttributeType != null)
                    {
                        var linkAttribute = propertyEntityAttributeType.CustomAttributes
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
                }
            }
        }
        
        linkEntityDictionaryTree.Last().Value.Mapping = mappingFields;

        return mappingFields;
    }
}