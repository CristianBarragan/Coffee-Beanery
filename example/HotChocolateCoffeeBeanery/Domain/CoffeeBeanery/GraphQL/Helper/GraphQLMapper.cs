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
    public static List<FieldMap> GetMappings<E, M>(MapperConfiguration mapper, E from, M to, bool isModel,
        List<string> models, List<string> entities,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeNode,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeNode,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeEdge,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeEdge,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeMutation,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeMutation,
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
                    FieldSourceName = mapping.TypeMap.SourceType != to.GetType()
                        ? mapping.GetSourceMemberName()
                        : mapping.DestinationName,
                    FieldSourceType = mapping.TypeMap.SourceType != to.GetType()
                        ? mapping.TypeMap.SourceType
                        : mapping.TypeMap.DestinationType,
                    SourceModel = mapping.TypeMap.SourceType != to.GetType()
                        ? mapping.TypeMap.SourceType.Name
                        : mapping.TypeMap.DestinationType.Name,
                    FieldDestinationName = mapping.TypeMap.SourceType == to.GetType()
                        ? mapping.GetSourceMemberName()
                        : mapping.DestinationName,
                    FieldDestinationType = mapping.TypeMap.SourceType == to.GetType()
                        ? mapping.TypeMap.SourceType
                        : mapping.TypeMap.DestinationType,
                    DestinationEntity = mapping.TypeMap.SourceType == to.GetType()
                        ? mapping.TypeMap.SourceType.Name
                        : mapping.TypeMap.DestinationType.Name
                };

                if (!entities.Contains(processingFieldMap.SourceModel))
                {
                    entities.Add(processingFieldMap.SourceModel);
                }
                
                if (!models.Contains(processingFieldMap.DestinationEntity))
                {
                    models.Add(processingFieldMap.DestinationEntity);
                }

                if (mappingFields.Any(a => a.FieldDestinationName.Matches(processingFieldMap.FieldDestinationName) &&
                                           a.FieldSourceName.Matches(processingFieldMap.FieldSourceName)))
                {
                    continue;
                }

                mappingFields!.Add(processingFieldMap);

                var propertyToAttributeType = to.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldDestinationName));
                
                var propertyFromAttributeType = from.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));

                if (propertyToAttributeType != null)
                {
                    // var propertyModelAttributeType = from.GetType().GetProperties()
                    //     .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldDestinationName));

                    if (propertyFromAttributeType == null)
                    {
                        continue;
                    }

                    var graphKeyAttribute = propertyFromAttributeType.CustomAttributes
                        .FirstOrDefault(a => a.AttributeType == typeof(GraphKeyAttribute));

                    var fromEnumDictionary = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    var toEnumDictionary = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    
                    var propertyEntityAttributeType = from.GetType().GetProperties()
                        .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));
                    
                    CustomAttributeData? linkAttribute;
                    CustomAttributeData? joinIdAttribute;
                    CustomAttributeData? joinAttribute;
                    var joinTo = string.Empty;
                    
                    if (propertyEntityAttributeType != null)
                    {
                        linkAttribute = propertyEntityAttributeType.CustomAttributes
                            .FirstOrDefault(a => a.AttributeType == typeof(LinkKeyAttribute));
                        joinIdAttribute = propertyEntityAttributeType.CustomAttributes
                            .FirstOrDefault(a => a.AttributeType == typeof(LinkIdKeyAttribute));
                        joinAttribute = propertyEntityAttributeType.CustomAttributes
                            .FirstOrDefault(a => a.AttributeType == typeof(JoinKeyAttribute));

                        if (linkKeys != null && linkAttribute != null && joinIdAttribute != null)
                        {
                            joinTo =
                                $"{joinAttribute.ConstructorArguments[0].Value}~{joinAttribute.ConstructorArguments[1].Value}";
                            var linkKey = new LinkKey()
                            {
                                From = $"{joinAttribute.ConstructorArguments[0].Value}~{joinAttribute.ConstructorArguments[1].Value}",
                                FromId = $"{joinIdAttribute.ConstructorArguments[0].Value}~{joinIdAttribute.ConstructorArguments[1].Value}",
                                To =
                                    $"{linkAttribute.ConstructorArguments[0].Value}~{linkAttribute.ConstructorArguments[1].Value}"
                            };
                            linkKeys.Add(linkKey);
                            
                            // var joinKey = new JoinKey()
                            // {
                            //     From =
                            //         $"{linkAttribute.ConstructorArguments[0].Value}~{linkAttribute.ConstructorArguments[1].Value}",
                            //     To =
                            //         $"{joinAttribute.ConstructorArguments[0].Value}~{joinAttribute.ConstructorArguments[1].Value}"
                            // };
                            // joinKeys.Add(joinKey);
                        }

                        // var joinOneAttribute = propertyEntityAttributeType.CustomAttributes
                        //     .FirstOrDefault(a => a.AttributeType == typeof(JoinOneKeyAttribute));
                        //
                        // if (joinOneKeys != null && joinOneAttribute != null)
                        // {
                        //     var joinOneKey = new JoinOneKey()
                        //     {
                        //         From =
                        //             $"{propertyEntityAttributeType.CustomAttributes
                        //                 .Last().ConstructorArguments[0].Value}~{
                        //                 propertyEntityAttributeType.CustomAttributes
                        //                     .Last().ConstructorArguments[1].Value}",
                        //         To = $"{processingFieldMap.DestinationEntity}~{
                        //             processingFieldMap.FieldDestinationName}"
                        //
                        //     };
                        //     joinOneKeys.Add(joinOneKey);
                        // }
                    }

                    // if (propertyToAttributeType == null)
                    // {
                    //     propertyToAttributeType = to.GetType().GetProperties()
                    //         .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldDestinationName));
                    // }

                    if (propertyToAttributeType != null &&
                        graphKeyAttribute?.AttributeType != null)
                    {
                        var nonNullableToType = Nullable.GetUnderlyingType(propertyToAttributeType
                            .PropertyType) ?? propertyToAttributeType.PropertyType;

                        if (propertyToAttributeType != null && (nonNullableToType.IsEnum ||
                            propertyToAttributeType.PropertyType.IsEnum))
                        {
                            var k = 0;
                            foreach (var value in Enum.GetValues(nonNullableToType))
                            {
                                fromEnumDictionary.Add(value.ToString()!, k.ToString());
                                k++;
                            }
                        }
                        
                        if (propertyFromAttributeType != null && (nonNullableToType.IsEnum ||
                            propertyFromAttributeType.PropertyType.IsEnum))
                        {
                            var k = 0;
                            foreach (var value in Enum.GetValues(nonNullableToType))
                            {
                                toEnumDictionary.Add(value.ToString()!, k.ToString());
                                k++;
                            }
                        }
                    }

                    joinTo = string.IsNullOrEmpty(joinTo)
                        ? $"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}"
                        : joinTo;
                    
                    AddDictionaryValue(linkModelDictionaryTreeEdge,
                        $"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}",
                        new SqlNode()
                        {
                            RelationshipKey =
                                $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                            Column = processingFieldMap.FieldSourceName,
                            Entity = processingFieldMap.SourceModel,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            IsGraph = to.GetType().BaseType.Name.Matches("GraphProcess"),
                            IsColumnGraph = graphKeyAttribute != null,
                            Graph = graphKeyAttribute?.ConstructorArguments[0].Value.ToString(),
                            FromEnumeration = fromEnumDictionary,
                            ToEnumeration = toEnumDictionary,
                            SqlNodeType = SqlNodeType.Edge,
                            Namespace = processingFieldMap.FieldSourceType.Namespace
                        });

                    AddDictionaryValue(linkModelDictionaryTreeNode,
                        $"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}",
                        new SqlNode()
                        {
                            RelationshipKey =
                                $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                            Column = processingFieldMap.FieldSourceName,
                            Entity = processingFieldMap.SourceModel,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            IsGraph = to.GetType().BaseType.Name.Matches("GraphProcess"),
                            IsColumnGraph = graphKeyAttribute != null,
                            Graph = graphKeyAttribute?.ConstructorArguments[0].Value.ToString(),
                            FromEnumeration = fromEnumDictionary,
                            ToEnumeration = toEnumDictionary,
                            SqlNodeType = SqlNodeType.Node,
                            Namespace = processingFieldMap.FieldSourceType.Namespace
                        });

                    AddDictionaryValue(linkModelDictionaryTreeMutation,
                        $"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}",
                        new SqlNode()
                        {
                            RelationshipKey =
                                $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                            Column = processingFieldMap.FieldSourceName,
                            Entity = processingFieldMap.SourceModel,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            IsGraph = to.GetType().BaseType.Name.Matches("GraphProcess"),
                            IsColumnGraph = graphKeyAttribute != null,
                            Graph = graphKeyAttribute?.ConstructorArguments[0].Value.ToString(),
                            FromEnumeration = fromEnumDictionary,
                            ToEnumeration = toEnumDictionary,
                            SqlNodeType = SqlNodeType.Mutation,
                            Namespace = processingFieldMap.FieldSourceType.Namespace
                        });
                    AddDictionaryValue(linkEntityDictionaryTreeEdge,
                        $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                        new SqlNode()
                        {
                            RelationshipKey =
                                $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                            Column = processingFieldMap.FieldSourceName,
                            Entity = processingFieldMap.SourceModel,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            IsGraph = to.GetType().BaseType.Name.Matches("GraphProcess"),
                            IsColumnGraph = graphKeyAttribute != null,
                            Graph = graphKeyAttribute?.ConstructorArguments[0].Value.ToString(),
                            FromEnumeration = fromEnumDictionary,
                            ToEnumeration = toEnumDictionary,
                            SqlNodeType = SqlNodeType.Edge,
                            Namespace = processingFieldMap.FieldDestinationType.Namespace
                        });


                    AddDictionaryValue(linkEntityDictionaryTreeNode,
                        $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                        new SqlNode()
                        {
                            RelationshipKey =
                                $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                            Column = processingFieldMap.FieldSourceName,
                            Entity = processingFieldMap.SourceModel,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            IsGraph = to.GetType().BaseType.Name.Matches("GraphProcess"),
                            IsColumnGraph = graphKeyAttribute != null,
                            Graph = graphKeyAttribute?.ConstructorArguments[0].Value.ToString(),
                            FromEnumeration = fromEnumDictionary,
                            ToEnumeration = toEnumDictionary,
                            SqlNodeType = SqlNodeType.Node,
                            Namespace = processingFieldMap.FieldDestinationType.Namespace
                        });

                    AddDictionaryValue(linkEntityDictionaryTreeMutation,
                        $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                        new SqlNode()
                        {
                            RelationshipKey =
                                $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                            Column = processingFieldMap.FieldSourceName,
                            Entity = processingFieldMap.SourceModel,
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            IsGraph = to.GetType().BaseType.Name.Matches("GraphProcess"),
                            IsColumnGraph = graphKeyAttribute != null,
                            Graph = graphKeyAttribute?.ConstructorArguments[0].Value.ToString(),
                            FromEnumeration = fromEnumDictionary,
                            ToEnumeration = toEnumDictionary,
                            SqlNodeType = SqlNodeType.Mutation,
                            Namespace = processingFieldMap.FieldDestinationType.Namespace
                        });

                    var linkBusinessAttribute = propertyFromAttributeType.CustomAttributes
                        .FirstOrDefault(a => a.AttributeType == typeof(LinkBusinessKey));

                    if (linkBusinessKeys != null && linkBusinessAttribute != null)
                    {

                        if (propertyFromAttributeType != null)
                        {
                            linkBusinessAttribute = propertyFromAttributeType.CustomAttributes
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
                        }
                    }
                }
            }
        }
        
        return mappingFields;
    }

    private static void AddDictionaryValue(Dictionary<string, SqlNode> dictionary, string key, SqlNode value)
    {
        if (!dictionary.ContainsKey(key))
        {
            dictionary.Add(key, value);    
        }
    }
}