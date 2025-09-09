using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using AutoMapper;
using AutoMapper.Configuration.Annotations;
using CoffeeBeanery.GraphQL.Configuration;
using CoffeeBeanery.GraphQL.Model;
using CoffeeBeanery.GraphQL.Extension;

namespace CoffeeBeanery.GraphQL.Helper;

public static class NodeTreeHelper
{
    /// <summary>
    /// Method that creates a tree for each entity
    /// </summary>
    /// <param name="nodeTrees"></param>
    /// <param name="entities"></param>
    /// <param name="databaseTypes"></param>
    /// <param name="nodeDatabaseClass"></param>
    /// <param name="domainTypes"></param>
    /// <param name="nodeDomainClass"></param>
    /// <param name="name"></param>
    /// <param name="mapperConfiguration"></param>
    /// <param name="ignoreNotMapped"></param>
    /// <param name="nodeId"></param>
    /// <typeparam name="E"></typeparam>
    /// <typeparam name="M"></typeparam>
    /// <returns></returns>
    public static NodeTree GenerateTree<E, M>(Dictionary<string, NodeTree> nodeTrees, List<string> entities,
        List<string> models,
        List<E> databaseTypes, E nodeEntityClass,
        List<M> domainTypes,
        M nodeDomainClass, string name, MapperConfiguration mapperConfiguration,
        List<KeyValuePair<string, int>> nodeId)
        where E : class where M : class
    {
        if (!entities.Contains(nodeEntityClass.GetType().Name))
        {
            entities.Add(nodeEntityClass.GetType().Name);
        }

        databaseTypes.Add(nodeEntityClass);
        domainTypes.Add(nodeDomainClass);

        return IterateTree<E, M>(nodeTrees, entities, models, databaseTypes, nodeEntityClass, domainTypes, nodeDomainClass,
            name, string.Empty, mapperConfiguration, nodeId)!;
    }

    /// <summary>
    /// Recursive method to visit every node and creates the tree node based on property names and enums using generics
    /// </summary>
    /// <param name="nodeTrees"></param>
    /// <param name="entities"></param>
    /// <param name="databaseTypes"></param>
    /// <param name="nodeDatabaseClass"></param>
    /// <param name="domainTypes"></param>
    /// <param name="nodeDomainClass"></param>
    /// <param name="name"></param>
    /// <param name="parentName"></param>
    /// <param name="id"></param>
    /// <param name="parentId"></param>
    /// <param name="mapperConfiguration"></param>
    /// <param name="ignoreNotMapped"></param>
    /// <param name="nodeId"></param>
    /// <typeparam name="E"></typeparam>
    /// <typeparam name="M"></typeparam>
    /// <returns></returns>
    private static NodeTree? IterateTree<E, M>(Dictionary<string, NodeTree> nodeTrees, List<string> entities,
        List<string> models, List<E> entityTypes, E? nodeEntityClass, List<M> modelTypes,
        M? nodeModelClass, string name, string parentName,
        MapperConfiguration mapperConfiguration, List<KeyValuePair<string, int>> nodeId)
        where E : class where  M : class
    {
        
        var nonNullableEntityType = Nullable.GetUnderlyingType(nodeEntityClass.GetType()) ?? nodeEntityClass.GetType();
        var nonNullableModelType = Nullable.GetUnderlyingType(nodeModelClass.GetType()) ?? nodeModelClass.GetType();
        
        if (typeof(IList).IsAssignableFrom(nonNullableModelType))
        {
            nodeModelClass = (M)Convert.ChangeType(Activator.CreateInstance(nonNullableModelType.GenericTypeArguments[0]),
                nonNullableModelType.GenericTypeArguments[0])!;
        }
        else
        {
            nodeModelClass = (M)Convert.ChangeType(
                Activator.CreateInstance(nonNullableModelType),
                nodeModelClass.GetType())!;
        }

        if (!nodeId.Any(i => i.Key.Matches(nodeModelClass!.GetType().Name)))
        {
            nodeId.Add(new KeyValuePair<string, int>(nodeModelClass!.GetType().Name!, nodeId.Count + 1));
        }
        
        var entityMapping = GraphQLMapper.GetMappings<E, M>(mapperConfiguration, 
            nodeModelClass, nodeEntityClass);

        var nodeName = nodeModelClass.GetType().Name;
        
        if (nodeModelClass.GetType().IsGenericType)
        {
            nodeName = nodeModelClass.GetType().GetGenericArguments()[0].Name;
        }

        var nodeIdParent = nodeId.FirstOrDefault(i => i.Key.Matches(parentName));

        var node = new NodeTree()
        {
            Name = nodeName,
            ParentName = parentName,
            Id = nodeId.Count + 1,
            ParentId = string.IsNullOrEmpty(nodeIdParent.Key) ? nodeId.Count : nodeIdParent.Value,
            Mappings = entityMapping,
            Children = [],
            ChildrenNames = [],
            JoinKey= entityMapping.Where(f => f.IsJoinKey).ToList(),
            UpsertKeys = entityMapping.Where(f => f.IsUpsertKey).ToList()
        };

        if (entityMapping.Count > 0 && !entities.Contains(entityMapping[0].DestinationEntity))
        {
            entities.Add(entityMapping[0].DestinationEntity);
            var entityType = Type.GetType($"{nodeEntityClass.GetType().Namespace}.{entityMapping[0].DestinationEntity},{nodeEntityClass.GetType().Assembly}");
            var entityVariable = (E)Activator.CreateInstance(entityType);
            entityTypes.Add(entityVariable);
        }
        
        if (!models.Contains(nodeModelClass.GetType().Name))
        {
            models.Add(nodeModelClass.GetType().Name);
            modelTypes.Add(nodeModelClass);
        }
        
        var schema = node.Mappings.FirstOrDefault(f => !string.IsNullOrEmpty(f.FieldDestinationSchema));
        if (schema != null)
        {
            node.Mappings.ForEach(f => f.FieldDestinationSchema = schema.FieldDestinationSchema);
            node.Schema = schema.FieldDestinationSchema;
        }

        var properties = nodeModelClass.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();

        var tree = new NodeTree();
        var childId = 0;
        var j = 0;
        for (var i = 0; i < properties.Count(); i++)
        {
            var schemaValue = string.Empty;
            var modelProperty = nodeModelClass.GetType().GetProperties()
                .FirstOrDefault(n => n.Name.Matches(properties[i].Name));
            
            nonNullableModelType = Nullable.GetUnderlyingType(modelProperty?.PropertyType!) ?? modelProperty?.PropertyType;
            
            M modelVariable = null;

            if (modelProperty != null && nonNullableModelType.CustomAttributes
                    .Any(a => a.AttributeType == typeof(LinkKeyAttribute)))
            {
                var model = modelProperty.CustomAttributes.First().ConstructorArguments[0].Value.ToString();
                var modelType = Type.GetType($"{nodeModelClass.GetType().Namespace}.{model},{nodeModelClass.GetType().Assembly}");
                modelVariable = (M)Activator.CreateInstance(modelType);
            }
            
            if (modelProperty != null && modelProperty.CustomAttributes
                    .Any(a => a.AttributeType == typeof(UpsertKeyAttribute)))
            {
                schemaValue = modelProperty.CustomAttributes.First().ConstructorArguments[1].Value.ToString();
                node.Schema = schemaValue;
            }
            
            if (GraphQLFieldExtension.IsPrimitiveType(nonNullableEntityType))
            {
                continue;
            }

            if (typeof(IList).IsAssignableFrom(nonNullableModelType))
            {
                if (modelVariable == null)
                {
                    modelVariable = (M)Convert.ChangeType(
                        Activator.CreateInstance(nonNullableModelType.GenericTypeArguments[0]),
                        nonNullableModelType.GenericTypeArguments[0])!;
                }
            }
            else if (nonNullableModelType.IsClass && nonNullableModelType != typeof(string))
            {
                if (modelVariable == null)
                {
                    modelVariable = (M)Convert.ChangeType(
                        Activator.CreateInstance(nonNullableModelType),
                        nonNullableModelType)!;
                }
            }
            else
            {
                continue;
            }
            
            tree = IterateTree<E, M>(nodeTrees, entities, models, entityTypes,
                nodeEntityClass,
                modelTypes,
                modelVariable,
                modelVariable.GetType().Name, name,
                mapperConfiguration, nodeId);
            
            if (tree != null)
            {
                schema = tree.Mappings.FirstOrDefault(f => !string.IsNullOrEmpty(f.FieldDestinationSchema));
                if (schema != null)
                {
                    tree.JoinKey = tree.JoinKey ?? new List<FieldMap>();
                    tree.JoinKey.AddRange(tree.Mappings.Where(f => f.IsJoinKey && !tree.JoinKey.Contains(f)));
                    tree.UpsertKeys = tree.UpsertKeys ?? new List<FieldMap>();
                    tree.UpsertKeys.AddRange(tree.Mappings.Where(f => f.IsUpsertKey && !tree.UpsertKeys.Contains(f)));
                    tree.Mappings.ForEach(f => f.FieldDestinationSchema = schema.FieldDestinationSchema);
                    tree.Schema = schema.FieldDestinationSchema;
                }
                
                node.ChildrenNames.Add(tree.Name);
                node.Children.Add(tree);
            }
        }
        
        if (nodeTrees.TryGetValue(node.Name, out _))
        {
            nodeTrees[node.Name] = node;
        }
        else
        {
            nodeTrees.Add(node.Name, node);    
        }
        
        return node;
    }
}