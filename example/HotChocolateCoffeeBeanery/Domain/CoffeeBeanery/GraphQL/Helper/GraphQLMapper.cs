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
        Dictionary<string, SqlNode>? linkEntityDictionaryTree = null, Dictionary<string, string>? upsertKeys = null,
        Dictionary<string, string>? joinKeys = null)
     where E : class where M : class
    {
        var configurationProvider = mapper.Internal().GetAllTypeMaps();
        var mappingFields = new List<FieldMap>();
        
        var mappings = configurationProvider
            .Where(configuration => configuration.SourceType.Name.Matches(to.GetType().Name) ||
                                    configuration.SourceType.Name.Matches(from.GetType().Name));

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

                // if (isModel && linkEntityDictionaryTree != null &&
                if (linkEntityDictionaryTree != null &&
                    !linkEntityDictionaryTree
                        .ContainsKey($"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}"))
                {
                    linkEntityDictionaryTree.Add($"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}", 
                        new SqlNode()
                    {
                        RelationshipKey = $"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}",
                        InsertColumn = processingFieldMap.FieldSourceName,
                        SelectColumn = processingFieldMap.FieldSourceName,
                        ExludedColumn = $"EXCLUDED.\"{processingFieldMap.FieldSourceName}\""
                    });
                }
                
                // if (isModel && linkEntityDictionaryTree != null &&
                if (linkEntityDictionaryTree != null &&
                    !linkEntityDictionaryTree
                        .ContainsKey($"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}"))
                {
                    linkEntityDictionaryTree.Add($"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}", 
                        new SqlNode()
                        {
                            RelationshipKey = $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                            InsertColumn = processingFieldMap.FieldDestinationName,
                            SelectColumn = processingFieldMap.FieldDestinationName,
                            ExludedColumn = $"EXCLUDED.\"{processingFieldMap.FieldDestinationName}\""
                        });
                }
                
                var propertyModelAttributeType = to.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));
                
                var entityNodeType = Type.GetType($"{from.GetType().Namespace}.{processingFieldMap.DestinationEntity},{from.GetType().Assembly}");
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
                
                // var upsertAttribute = propertyModelAttributeType.CustomAttributes
                //     .FirstOrDefault(a => a.AttributeType == typeof(UpsertKeyAttribute));
                // processingFieldMap.IsUpsertKey = upsertAttribute != null;
                //
                // // if (isModel && upsertKeys != null && upsertAttribute != null)
                // if (upsertKeys != null && upsertAttribute != null &&
                //     !upsertKeys.ContainsKey($"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}"))
                // {
                //     upsertKeys.Add(
                //         $"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}",
                //         $"{upsertAttribute
                //             .ConstructorArguments[0].Value.ToString()}~{
                //             upsertAttribute
                //                 .ConstructorArguments[1].Value.ToString()}"
                //     );
                // }
                //
                // if (upsertKeys != null && upsertAttribute != null &&
                //     !upsertKeys.ContainsKey($"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}"))
                // {
                //     upsertKeys.Add(
                //         $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                //         $"{upsertAttribute
                //             .ConstructorArguments[0].Value.ToString()}~{
                //             upsertAttribute
                //                 .ConstructorArguments[1].Value.ToString()}"
                //     );
                // }
                //
                // var joinAttribute = propertyModelAttributeType.CustomAttributes
                //     .FirstOrDefault(a => a.AttributeType == typeof(JoinKeyAttribute));
                // processingFieldMap.IsJoinKey = joinAttribute != null;
                //
                // // if (isModel && joinKeys != null && joinAttribute != null)
                // if (joinKeys != null && joinAttribute != null &&
                //     !joinKeys.ContainsKey($"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}"))
                // {
                //     joinKeys.Add(
                //         $"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}",
                //         $"{joinAttribute
                //             .ConstructorArguments[0].Value.ToString()}~{
                //             joinAttribute
                //                 .ConstructorArguments[1].Value.ToString()}"
                //     );
                // }
                //
                // if (joinKeys != null && joinAttribute != null &&
                //     !joinKeys.ContainsKey($"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}"))
                // {
                //     joinKeys.Add(
                //         $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                //         $"{joinAttribute
                //             .ConstructorArguments[0].Value.ToString()}~{
                //             joinAttribute
                //                 .ConstructorArguments[1].Value.ToString()}"
                //     );
                // }
                
                mappingFields!.Add(processingFieldMap);
            }
        }

        return mappingFields;
    }
}