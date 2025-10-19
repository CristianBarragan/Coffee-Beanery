﻿using AutoMapper;
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
    public static List<FieldMap> GetMappings<E, M>(MapperConfiguration mapper, E from, M to, bool isModel,
        List<string> models, List<string> entities,
        Dictionary<string, SqlNode>? linkEntityDictionaryTree,
        Dictionary<string, SqlNode>? linkModelDictionaryTree,
        List<LinkKey> linkKeys, List<JoinKey> joinKeys, List<JoinOneKey> joinOneKeys, List<LinkBusinessKey> linkBusinessKeys)
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

                if (processingFieldMap.FieldDestinationName.Matches("CustomerType") ||
                    processingFieldMap.FieldSourceName.Matches("CustomerType"))
                {
                    var a = true;
                }

                if (mappingFields.Any(a => a.FieldDestinationName.Matches(processingFieldMap.FieldDestinationName) &&
                                           a.FieldSourceName.Matches(processingFieldMap.FieldSourceName)))
                {
                    continue;
                }

                mappingFields!.Add(processingFieldMap);

                var enumDictionary = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                var linkKeyDestinationProperty = models.Contains(processingFieldMap.FieldDestinationName)
                    ? $"{processingFieldMap.FieldDestinationName}Key"
                    : processingFieldMap.FieldDestinationName;

                var linkKeySourceProperty = models.Contains(processingFieldMap.FieldSourceName)
                    ? $"{processingFieldMap.FieldSourceName}Key"
                    : processingFieldMap.FieldSourceName;

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
                            enumDictionary.Add(value.ToString()!, k.ToString());
                            k++;
                        }
                    }
                }

                var propertyFromAttributeType = from.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));

                if (linkModelDictionaryTree.ContainsKey($"{processingFieldMap.DestinationEntity}~{
                    linkKeyDestinationProperty}"))
                {
                    if (enumDictionary.Count > 0 && isModel)
                    {
                        linkModelDictionaryTree[$"{processingFieldMap.DestinationEntity}~{
                            linkKeyDestinationProperty}"].FromEnumeration = enumDictionary;
                    }

                    if (enumDictionary.Count > 0 && !isModel)
                    {
                        linkModelDictionaryTree[$"{processingFieldMap.DestinationEntity}~{
                            linkKeyDestinationProperty}"].ToEnumeration = enumDictionary;
                    }
                }
                else if (!models.Contains(linkKeyDestinationProperty) && !models.Contains(linkKeySourceProperty))
                {
                    linkModelDictionaryTree.Add(
                        $"{processingFieldMap.DestinationEntity}~{linkKeyDestinationProperty}",
                        new SqlNode()
                        {
                            RelationshipKey = $"{processingFieldMap.SourceModel}~{linkKeySourceProperty}",
                            Column = linkKeySourceProperty,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum
                        });

                    var modelCreation = linkModelDictionaryTree.Last();
                    if (enumDictionary.Count > 0 && isModel)
                    {
                        modelCreation.Value.FromEnumeration = enumDictionary;
                    }

                    if (enumDictionary.Count > 0 && !isModel)
                    {
                        modelCreation.Value.ToEnumeration = enumDictionary;
                    }
                }

                if (linkEntityDictionaryTree.ContainsKey($"{processingFieldMap.SourceModel}~{linkKeySourceProperty}"))
                {
                    if (enumDictionary.Count > 0 && isModel)
                    {
                        linkEntityDictionaryTree[$"{processingFieldMap.SourceModel}~{linkKeySourceProperty}"]
                            .FromEnumeration = enumDictionary;
                    }

                    if (enumDictionary.Count > 0 && !isModel)
                    {
                        linkEntityDictionaryTree[$"{processingFieldMap.SourceModel}~{linkKeySourceProperty}"]
                            .ToEnumeration = enumDictionary;
                    }
                }
                else if (!entities.Contains(linkKeyDestinationProperty) && !entities.Contains(linkKeySourceProperty))
                {
                    linkEntityDictionaryTree.Add($"{processingFieldMap.SourceModel}~{linkKeySourceProperty}",
                        new SqlNode()
                        {
                            RelationshipKey = $"{processingFieldMap.DestinationEntity}~{linkKeyDestinationProperty}",
                            Column = linkKeySourceProperty,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum
                        });

                    var entityCreation = linkEntityDictionaryTree.Last();
                    if (enumDictionary.Count > 0 && isModel)
                    {
                        entityCreation.Value.FromEnumeration = enumDictionary;
                    }

                    if (enumDictionary.Count > 0 && !isModel)
                    {
                        entityCreation.Value.ToEnumeration = enumDictionary;
                    }
                }

                if (propertyToAttributeType != null)
                {
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
                                From =
                                    $"{processingFieldMap.SourceModel}~{linkBusinessAttribute.ConstructorArguments[1].Value.ToString()}",
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
                                From =
                                    $"{processingFieldMap.SourceModel}~{linkAttribute.ConstructorArguments[1].Value}",
                                To =
                                    $"{linkAttribute.ConstructorArguments[0].Value}~{linkAttribute.ConstructorArguments[1].Value}"
                            };
                            linkKeys.Add(linkKey);
                        }
                        
                        var joinAttribute = propertyModelAttributeType.CustomAttributes
                            .FirstOrDefault(a => a.AttributeType == typeof(JoinKeyAttribute));

                        if (joinKeys != null && joinAttribute != null)
                        {
                            var joinKey = new JoinKey()
                            {
                                From =
                                    $"{linkAttribute.ConstructorArguments[0].Value}~{joinAttribute.ConstructorArguments[0].Value}Id",
                                To =
                                    $"{joinAttribute.ConstructorArguments[0].Value}~{joinAttribute.ConstructorArguments[1].Value}"
                            };
                            joinKeys.Add(joinKey);
                        }
                        
                        var joinOneAttribute = propertyModelAttributeType.CustomAttributes
                            .FirstOrDefault(a => a.AttributeType == typeof(JoinOneKeyAttribute));

                        if (joinOneKeys != null && joinOneAttribute != null)
                        {
                            var joinOneKey = new JoinOneKey()
                            {
                                From =
                                    $"{linkAttribute.ConstructorArguments[0].Value}~{joinOneAttribute.ConstructorArguments[0].Value}Id",
                                To =
                                    $"{joinOneAttribute.ConstructorArguments[0].Value}~{joinOneAttribute.ConstructorArguments[1].Value}"
                            };
                            joinOneKeys.Add(joinOneKey);
                        }
                    }
                }

                var entityNodeType =
                    Type.GetType(
                        $"{from.GetType().Namespace}.{processingFieldMap.FieldSourceName},{from.GetType().Assembly}");

                if (entityNodeType == null)
                {
                    entityNodeType =
                        Type.GetType(
                            $"{from.GetType().Namespace}.{processingFieldMap.FieldDestinationName},{from.GetType().Assembly}");
                }

                if (entityNodeType == null)
                {
                    continue;
                }

                var entityVariable = (E)Activator.CreateInstance(entityNodeType);
                propertyFromAttributeType = entityVariable.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));

                if (propertyFromAttributeType == null)
                {
                    entityVariable = (E)Activator.CreateInstance(entityNodeType);
                    propertyFromAttributeType = entityVariable.GetType().GetProperties()
                        .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldDestinationName));
                }

                if (propertyFromAttributeType != null)
                {
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
                                From =
                                    $"{processingFieldMap.SourceModel}~{linkAttribute.ConstructorArguments[1].Value.ToString()}",
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