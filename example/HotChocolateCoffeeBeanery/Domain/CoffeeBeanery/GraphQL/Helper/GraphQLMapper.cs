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
                
                var propertyToAttributeType = to.GetType().GetProperties()
                    .FirstOrDefault(n => n.Name.Matches(processingFieldMap.FieldSourceName));
                
                var entityNodeType = Type.GetType($"{from.GetType().Namespace}.{processingFieldMap.DestinationEntity},{from.GetType().Assembly}");
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

                if (linkEntityDictionaryTree != null &&
                    linkEntityDictionaryTree.TryGetValue($"{processingFieldMap.DestinationEntity}~{
                        processingFieldMap.FieldDestinationName}", out var valueLinkEntity))
                {
                    valueLinkEntity.IsEnumeration = processingFieldMap.FieldSourceType.IsEnum;
                    valueLinkEntity.FromEnumeration = enumDictionaryFrom;
                    valueLinkEntity.ToEnumeration = enumDictionaryTo;
                    linkEntityDictionaryTree[$"{processingFieldMap.DestinationEntity}~{
                        processingFieldMap.FieldDestinationName}"] = valueLinkEntity;
                }
                else {
                    linkEntityDictionaryTree.Add($"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}", 
                        new SqlNode()
                        {
                            RelationshipKey = $"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}",
                            InsertColumn = processingFieldMap.FieldDestinationName,
                            SelectColumn = processingFieldMap.FieldDestinationName,
                            ExludedColumn = $"EXCLUDED.\"{processingFieldMap.FieldDestinationName}\"",
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            FromEnumeration = enumDictionaryFrom,
                            ToEnumeration = enumDictionaryTo
                        });
                }
                
                if (linkEntityDictionaryTree != null &&
                    linkEntityDictionaryTree.TryGetValue($"{processingFieldMap.SourceModel}~{
                        processingFieldMap.FieldSourceName}", out valueLinkEntity))
                {
                    valueLinkEntity.IsEnumeration = processingFieldMap.FieldSourceType.IsEnum;
                    valueLinkEntity.FromEnumeration = enumDictionaryFrom;
                    valueLinkEntity.ToEnumeration = enumDictionaryTo;
                    linkEntityDictionaryTree[$"{processingFieldMap.SourceModel}~{
                        processingFieldMap.FieldSourceName}"] = valueLinkEntity;
                }
                else {
                    linkEntityDictionaryTree.Add($"{processingFieldMap.SourceModel}~{processingFieldMap.FieldSourceName}", 
                        new SqlNode()
                        {
                            RelationshipKey = $"{processingFieldMap.DestinationEntity}~{processingFieldMap.FieldDestinationName}",
                            InsertColumn = processingFieldMap.FieldSourceName,
                            SelectColumn = processingFieldMap.FieldSourceName,
                            ExludedColumn = $"EXCLUDED.\"{processingFieldMap.FieldSourceName}\"",
                            IsEnumeration = processingFieldMap.FieldSourceType.IsEnum,
                            FromEnumeration = enumDictionaryFrom,
                            ToEnumeration = enumDictionaryTo
                        });
                }
                
                mappingFields!.Add(processingFieldMap);
            }
        }

        return mappingFields;
    }
}