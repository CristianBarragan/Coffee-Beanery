using System.Collections;
using System.Reflection;
using AutoMapper;
using CoffeeBeanery.GraphQL.Configuration;
using CoffeeBeanery.GraphQL.Model;
using CoffeeBeanery.GraphQL.Extension;

namespace CoffeeBeanery.GraphQL.Helper;

public static class NodeTreeHelper
{
    public static void GenerateTree<E, M>(Dictionary<string, NodeTree> entityNodeTrees,
        Dictionary<string, NodeTree> modelNodeTrees,
        E nodeFromClass, M nodeToClass, string name,
        MapperConfiguration mapperConfiguration,
        List<KeyValuePair<string, int>> entityNodesId, List<KeyValuePair<string, int>> modelNodesId, 
        bool isModel, List<string> entities, List<string> models,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeNode,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeNode,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeEdge,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeEdge,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeMutation,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeMutation,
        List<string>? upsertKeys, List<JoinKey>? joinKeys, List<JoinOneKey>? joinOneKeys, List<LinkKey>? linkKeys,
        List<LinkBusinessKey>? linkBusinessKeys)
        where E : class where M : class
    {
        IterateTreeDictionary<E, M>(entityNodeTrees, nodeFromClass, nodeToClass,
            name, string.Empty, mapperConfiguration, entityNodesId, false,
            models, entities, [], 
            linkEntityDictionaryTreeNode,
            linkModelDictionaryTreeNode,
            linkEntityDictionaryTreeEdge,
            linkModelDictionaryTreeEdge,
            linkEntityDictionaryTreeMutation,
            linkModelDictionaryTreeMutation,
            upsertKeys, joinKeys, joinOneKeys, linkKeys, linkBusinessKeys);
        
        IterateTreeDictionary<M, E>(modelNodeTrees, nodeToClass, nodeFromClass,
            name, string.Empty, mapperConfiguration, modelNodesId, true,
            models, entities, [], 
            linkEntityDictionaryTreeNode,
            linkModelDictionaryTreeNode,
            linkEntityDictionaryTreeEdge,
            linkModelDictionaryTreeEdge,
            linkEntityDictionaryTreeMutation,
            linkModelDictionaryTreeMutation,
            upsertKeys, joinKeys, joinOneKeys, linkKeys, linkBusinessKeys);
        
        IterateTree<M, E>(entityNodeTrees, modelNodeTrees, nodeToClass,
            nodeFromClass,
            name, string.Empty, mapperConfiguration, entityNodesId, true,
            models, entities, [],
            linkEntityDictionaryTreeNode,
            linkModelDictionaryTreeNode,
            linkEntityDictionaryTreeEdge,
            linkModelDictionaryTreeEdge,
            linkEntityDictionaryTreeMutation,
            linkModelDictionaryTreeMutation,
            upsertKeys, joinKeys, joinOneKeys, linkKeys, linkBusinessKeys);
    }

    /// <summary>
    /// Recursively iterate schema creating the parent-child relationships 
    /// </summary>
    /// <param name="nodeTrees"></param>
    /// <param name="nodeFromClass"></param>
    /// <param name="nodeToClass"></param>
    /// <param name="name"></param>
    /// <param name="parentName"></param>
    /// <param name="mapperConfiguration"></param>
    /// <param name="nodeId"></param>
    /// <param name="isModel"></param>
    /// <param name="models"></param>
    /// <param name="entities"></param>
    /// <param name="visitedNode"></param>
    /// <param name="linkEntityDictionaryTree"></param>
    /// <param name="linkModelDictionaryTree"></param>
    /// <param name="upsertKeys"></param>
    /// <param name="joinKeys"></param>
    /// <param name="linkKeys"></param>
    /// <param name="linkBusinessKeys"></param>
    /// <typeparam name="E"></typeparam>
    /// <typeparam name="M"></typeparam>
    /// <returns></returns>
    private static (NodeTree? entity, NodeTree? model) IterateTree<E, M>(Dictionary<string, NodeTree> entityNodeTrees,
        Dictionary<string, NodeTree> modelNodeTrees,
        E? nodeFromClass, M? nodeToClass, string name, string parentName,
        MapperConfiguration mapperConfiguration, List<KeyValuePair<string, int>> nodeId, bool isModel,
        List<string> models, List<string> entities, List<string> visitedNode,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeNode,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeNode,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeEdge,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeEdge,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeMutation,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeMutation, List<string>? upsertKeys, List<JoinKey>? joinKeys,
        List<JoinOneKey>? joinOneKeys, List<LinkKey>? linkKeys, List<LinkBusinessKey>? linkBusinessKeys)
        where E : class where M : class
    {
        if (visitedNode.Any(v => v.Matches($"{name}{parentName}")))
        {
            return default;
        }

        visitedNode.Add($"{name}{parentName}");

        
        var nonNullableFromType = Nullable.GetUnderlyingType(nodeFromClass.GetType()) ?? nodeFromClass.GetType();
        
        var nonNullableToType = Nullable.GetUnderlyingType(nodeToClass.GetType()) ?? nodeToClass.GetType();

        if (typeof(IList).IsAssignableFrom(nonNullableToType))
        {
            nodeToClass = (M)Convert.ChangeType(Activator.CreateInstance(nonNullableToType.GenericTypeArguments[0]),
                nonNullableToType.GenericTypeArguments[0])!;
        }
        else
        {
            nodeToClass = (M)Convert.ChangeType(
                Activator.CreateInstance(nonNullableToType),
                nodeToClass.GetType())!;
        }

        if (!nodeId.Any(i => i.Key.Matches(nodeToClass!.GetType().Name)))
        {
            nodeId.Add(new KeyValuePair<string, int>(nodeToClass!.GetType().Name!, nodeId.Count + 1));
        }

        var fromMapping = GraphQLMapper.GetMappings<E, M>(mapperConfiguration,
            nodeFromClass, nodeToClass, isModel, models, entities, linkEntityDictionaryTreeNode,
            linkModelDictionaryTreeNode, linkEntityDictionaryTreeEdge, linkModelDictionaryTreeEdge, 
            linkEntityDictionaryTreeMutation, linkModelDictionaryTreeMutation, linkKeys, joinKeys, joinOneKeys, linkBusinessKeys);

        var toProperty = nodeToClass.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(t => t.CustomAttributes
                .Any(a => a.AttributeType == typeof(UpsertKeyAttribute)));

        var schemaValue = string.Empty;

        if (toProperty != null && toProperty.CustomAttributes
                .Any(a => a.AttributeType == typeof(UpsertKeyAttribute)))
        {
            schemaValue = toProperty.CustomAttributes.Last().ConstructorArguments[1].Value.ToString();
            fromMapping.ForEach(f => f.FieldDestinationSchema = schemaValue);

            var upsertKeyAttribute =
                toProperty.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(UpsertKeyAttribute));
            var entity = upsertKeyAttribute.ConstructorArguments[0].Value;
            var column = upsertKeyAttribute.ConstructorArguments[1].Value;

            upsertKeys.Add($"{entity}~{column}");

            if (!linkEntityDictionaryTreeNode.ContainsKey($"{entity}~{column}"))
            {
                var columnMapped = linkEntityDictionaryTreeNode.FirstOrDefault(a => a.Key.Split('~')[0]
                    .Matches(entity.ToString()));

                if (columnMapped.Value != null)
                {
                    linkEntityDictionaryTreeNode.Add($"{entity}~{column}",
                        new SqlNode()
                        {
                            RelationshipKey = $"{entity}~{column}",
                            Column = column.ToString(),
                            Entity = entity.ToString(),
                            Namespace = nodeFromClass.GetType().Namespace,
                            SqlNodeType = SqlNodeType.Node,
                            // JoinKeys = columnMapped.Value.JoinKeys,
                            IsGraph = columnMapped.Value.IsGraph,
                            // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                            LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                            LinkKeys = columnMapped.Value.LinkKeys
                        });
                    
                    linkEntityDictionaryTreeEdge.Add($"{entity}~{column}",
                        new SqlNode()
                        {
                            Column = column.ToString(),
                            Entity = entity.ToString(),
                            Namespace = nodeFromClass.GetType().Namespace,
                            SqlNodeType = SqlNodeType.Edge,
                            // JoinKeys = columnMapped.Value.JoinKeys,
                            IsGraph = columnMapped.Value.IsGraph,
                            // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                            LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                            LinkKeys = columnMapped.Value.LinkKeys
                        });
                    
                    linkEntityDictionaryTreeMutation.Add($"{entity}~{column}",
                        new SqlNode()
                        {
                            Column = column.ToString(),
                            Entity = entity.ToString(),
                            Namespace = nodeFromClass.GetType().Namespace,
                            SqlNodeType = SqlNodeType.Mutation,
                            // JoinKeys = columnMapped.Value.JoinKeys,
                            IsGraph = columnMapped.Value.IsGraph,
                            // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                            LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                            LinkKeys = columnMapped.Value.LinkKeys
                        });   
                }
            }
        }
        
        if (fromMapping.Count > 0 && linkEntityDictionaryTreeNode.Count > 0)
        {
            var columnMapped = linkEntityDictionaryTreeNode.First();
            
            if (!linkEntityDictionaryTreeNode.ContainsKey($"{nodeFromClass.GetType().Name}~Id"))
            {
                linkEntityDictionaryTreeNode.Add($"{nodeFromClass.GetType().Name}~Id",
                    new SqlNode()
                    {
                        RelationshipKey = $"{nodeFromClass.GetType().Name}~Id",
                        Column = $"{nodeFromClass.GetType().Name}Id",
                        Entity = nodeFromClass.GetType().Name,
                        Namespace = nodeFromClass.GetType().Namespace,
                        SqlNodeType = SqlNodeType.Node,
                        // JoinKeys = columnMapped.Value.JoinKeys,
                        IsGraph = columnMapped.Value.IsGraph,
                        // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                        LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                        LinkKeys = columnMapped.Value.LinkKeys
                });
                
                linkEntityDictionaryTreeEdge.Add($"{nodeFromClass.GetType().Name}~Id",
                    new SqlNode()
                    {
                        RelationshipKey = $"{nodeFromClass.GetType().Name}~Id",
                        Column = $"{nodeFromClass.GetType().Name}Id",
                        Entity = nodeFromClass.GetType().Name,
                        Namespace = nodeFromClass.GetType().Namespace,
                        SqlNodeType = SqlNodeType.Edge,
                        // JoinKeys = columnMapped.Value.JoinKeys,
                        IsGraph = columnMapped.Value.IsGraph,
                        // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                        LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                        LinkKeys = columnMapped.Value.LinkKeys
                });
                
                linkEntityDictionaryTreeMutation.Add($"{nodeFromClass.GetType().Name}~Id",
                    new SqlNode()
                    {
                        RelationshipKey = $"{nodeFromClass.GetType().Name}~Id",
                        Column = $"{nodeFromClass.GetType().Name}Id",
                        Entity = nodeFromClass.GetType().Name,
                        Namespace = nodeFromClass.GetType().Namespace,
                        SqlNodeType = SqlNodeType.Mutation,
                        // JoinKeys = columnMapped.Value.JoinKeys,
                        IsGraph = columnMapped.Value.IsGraph,
                        // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                        LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                        LinkKeys = columnMapped.Value.LinkKeys
                });
            }
        }
        
        var entityNodeName = nodeToClass.GetType().Name;

        if (nodeFromClass.GetType().IsGenericType)
        {
            entityNodeName = nodeToClass.GetType().GetGenericArguments()[0].Name;
        }

        var entityNodeIdParent = nodeId.FirstOrDefault(i => i.Key.Matches(parentName));

        var properties = nodeToClass.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
        
        var childId = 0;
        var j = 0;
        E fromVariable = null;
        M toVariable = null;
        var treesKey = new List<string>();
        var treesCombinationKey = new List<string>();
        
        foreach (var modelNode in modelNodeTrees)
        {
            foreach (var entityNode in entityNodeTrees)
            {
                var modelType = Type.GetType($"{nodeToClass.GetType().Namespace}.{modelNode.Value.Name},{nodeToClass.GetType().Assembly}");
                var model = (M) Activator.CreateInstance(modelType);

                var entityType = Type.GetType($"{nodeFromClass.GetType().Namespace}.{entityNode.Value.Name},{nodeFromClass.GetType().Assembly}");;
                var entity = (E) Activator.CreateInstance(entityType);
                
                IterateTree<E, M>(entityNodeTrees, modelNodeTrees,
                    entity, model, entity.GetType().Name, model.GetType().Name,
                    mapperConfiguration, nodeId, isModel, models, entities, visitedNode,
                    linkEntityDictionaryTreeNode,
                    linkModelDictionaryTreeNode,
                    linkEntityDictionaryTreeEdge,
                    linkModelDictionaryTreeEdge,
                    linkEntityDictionaryTreeMutation,
                    linkModelDictionaryTreeMutation,
                    upsertKeys, joinKeys, joinOneKeys, linkKeys, linkBusinessKeys);
            }
        }
        
        return default;
    }
    
    /// <summary>
    /// Recursively iterate schema creating the parent-child relationships 
    /// </summary>
    /// <param name="nodeTrees"></param>
    /// <param name="nodeFromClass"></param>
    /// <param name="nodeToClass"></param>
    /// <param name="name"></param>
    /// <param name="parentName"></param>
    /// <param name="mapperConfiguration"></param>
    /// <param name="nodeId"></param>
    /// <param name="isModel"></param>
    /// <param name="models"></param>
    /// <param name="entities"></param>
    /// <param name="visitedNode"></param>
    /// <param name="linkEntityDictionaryTree"></param>
    /// <param name="linkModelDictionaryTree"></param>
    /// <param name="upsertKeys"></param>
    /// <param name="joinKeys"></param>
    /// <param name="linkKeys"></param>
    /// <param name="linkBusinessKeys"></param>
    /// <typeparam name="E"></typeparam>
    /// <typeparam name="M"></typeparam>
    /// <returns></returns>
    private static NodeTree? IterateTreeDictionary<E, M>(Dictionary<string, NodeTree> nodeTrees,
        E? nodeFromClass, M? nodeToClass, string name, string parentName,
        MapperConfiguration mapperConfiguration, List<KeyValuePair<string, int>> nodeId, bool isModel,
        List<string> models, List<string> entities, List<string> visitedNode,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeNode,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeNode,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeEdge,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeEdge,
        Dictionary<string, SqlNode>? linkEntityDictionaryTreeMutation,
        Dictionary<string, SqlNode>? linkModelDictionaryTreeMutation, List<string>? upsertKeys, List<JoinKey>? joinKeys,
        List<JoinOneKey>? joinOneKeys, List<LinkKey>? linkKeys, List<LinkBusinessKey>? linkBusinessKeys)
        where E : class where M : class
    {
        if (visitedNode.Any(v => v.Matches($"{name}")))
        {
            return new NodeTree();
        }

        visitedNode.Add($"{name}");

        var nonNullableFromType = Nullable.GetUnderlyingType(nodeFromClass.GetType()) ?? nodeFromClass.GetType();
        
        var nonNullableToType = Nullable.GetUnderlyingType(nodeToClass.GetType()) ?? nodeToClass.GetType();

        if (typeof(IList).IsAssignableFrom(nonNullableToType))
        {
            nodeToClass = (M)Convert.ChangeType(Activator.CreateInstance(nonNullableToType.GenericTypeArguments[0]),
                nonNullableToType.GenericTypeArguments[0])!;
        }
        else
        {
            nodeToClass = (M)Convert.ChangeType(
                Activator.CreateInstance(nonNullableToType),
                nodeToClass.GetType())!;
        }

        if (!nodeId.Any(i => i.Key.Matches(nodeToClass!.GetType().Name)))
        {
            nodeId.Add(new KeyValuePair<string, int>(nodeToClass!.GetType().Name!, nodeId.Count + 1));
        }

        var fromMapping = GraphQLMapper.GetMappings<E, M>(mapperConfiguration,
            nodeFromClass, nodeToClass, isModel, models, entities, linkEntityDictionaryTreeNode,
            linkModelDictionaryTreeNode, linkEntityDictionaryTreeEdge, linkModelDictionaryTreeEdge, 
            linkEntityDictionaryTreeMutation, linkModelDictionaryTreeMutation, linkKeys, joinKeys, joinOneKeys, linkBusinessKeys);

        if (fromMapping.Count > 0)
        {
            var columnMapped = linkEntityDictionaryTreeNode.FirstOrDefault();

            if (columnMapped.Value != null)
            {
                if (!linkEntityDictionaryTreeNode.ContainsKey($"{nodeToClass.GetType().Name}~Id"))
                {
                    linkEntityDictionaryTreeNode.Add($"{nodeToClass.GetType().Name}~Id",
                        new SqlNode()
                        {
                            RelationshipKey = $"{nodeToClass.GetType().Name}~Id",
                            Column = $"{nodeToClass.GetType().Name}Id",
                            Namespace = nodeToClass.GetType().Namespace,
                            SqlNodeType = SqlNodeType.Node,
                            // JoinKeys = columnMapped.Value.JoinKeys,
                            IsGraph = columnMapped.Value.IsGraph,
                            // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                            LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                            LinkKeys = columnMapped.Value.LinkKeys
                    });
                    
                    linkEntityDictionaryTreeEdge.Add($"{nodeToClass.GetType().Name}~Id",
                        new SqlNode()
                        {
                            RelationshipKey = $"{nodeToClass.GetType().Name}~Id",
                            Column = $"{nodeToClass.GetType().Name}Id",
                            Namespace = nodeToClass.GetType().Namespace,
                            SqlNodeType = SqlNodeType.Edge,
                            // JoinKeys = columnMapped.Value.JoinKeys,
                            IsGraph = columnMapped.Value.IsGraph,
                            // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                            LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                            LinkKeys = columnMapped.Value.LinkKeys
                    });
                    
                    linkEntityDictionaryTreeMutation.Add($"{nodeToClass.GetType().Name}~Id",
                        new SqlNode()
                        {
                            RelationshipKey = $"{nodeToClass.GetType().Name}~Id",
                            Column = $"{nodeToClass.GetType().Name}Id",
                            Namespace = nodeToClass.GetType().Namespace,
                            SqlNodeType = SqlNodeType.Mutation,
                            // JoinKeys = columnMapped.Value.JoinKeys,
                            IsGraph = columnMapped.Value.IsGraph,
                            // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                            LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                            LinkKeys = columnMapped.Value.LinkKeys
                    });
                }
            }
        }
        
        var nodeName = nodeToClass.GetType().Name;

        if (nodeToClass.GetType().IsGenericType)
        {
            nodeName = nodeToClass.GetType().GetGenericArguments()[0].Name;
        }

        var nodeIdParent = nodeId.FirstOrDefault(i => i.Key.Matches(parentName));

        var node = new NodeTree()
        {
            Name = nodeName,
            ParentName = parentName,
            Id = nodeId.Count + 1,
            ParentId = string.IsNullOrEmpty(nodeIdParent.Key) ? nodeId.Count : nodeIdParent.Value,
            Children = [],
            ChildrenName = [],
            Mapping = fromMapping
        };

        var toProperty = nodeToClass.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(t => t.CustomAttributes
                .Any(a => a.AttributeType == typeof(UpsertKeyAttribute)));

        if (toProperty != null && toProperty.CustomAttributes
                .Any(a => a.AttributeType == typeof(UpsertKeyAttribute)))
        {
            var schemaValue = toProperty.CustomAttributes.Last().ConstructorArguments[1].Value.ToString();
            node.Schema = schemaValue;
            node.Mapping.ForEach(f => f.FieldDestinationSchema = schemaValue);

            var upsertKeyAttribute =
                toProperty.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(UpsertKeyAttribute));
            var entity = upsertKeyAttribute.ConstructorArguments[0].Value;
            var column = toProperty.Name;

            upsertKeys.Add($"{entity}~{column}");

            if (!linkEntityDictionaryTreeNode.ContainsKey($"{entity}~{column}"))
            {
                var columnMapped = linkEntityDictionaryTreeNode.FirstOrDefault(a => a.Key.Split('~')[0]
                    .Matches(entity.ToString()));

                if (columnMapped.Value != null)
                {
                    linkEntityDictionaryTreeNode.Add($"{entity}~{column}",
                        new SqlNode()
                        {
                            RelationshipKey = $"{entity}~{column}",
                            Column = column,
                            Namespace = nodeToClass.GetType().Namespace,
                            SqlNodeType = SqlNodeType.Node,
                            // JoinKeys = columnMapped.Value.JoinKeys,
                            IsGraph = columnMapped.Value.IsGraph,
                            // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                            LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                            LinkKeys = columnMapped.Value.LinkKeys
                        });
                    
                    linkEntityDictionaryTreeEdge.Add($"{entity}~{column}",
                        new SqlNode()
                        {
                            Column = column,
                            Namespace = nodeToClass.GetType().Namespace,
                            SqlNodeType = SqlNodeType.Edge,
                            // JoinKeys = columnMapped.Value.JoinKeys,
                            IsGraph = columnMapped.Value.IsGraph,
                            // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                            LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                            LinkKeys = columnMapped.Value.LinkKeys
                        });
                    
                    linkEntityDictionaryTreeMutation.Add($"{entity}~{column}",
                        new SqlNode()
                        {
                            Column = column,
                            Namespace = nodeToClass.GetType().Namespace,
                            SqlNodeType = SqlNodeType.Mutation,
                            // JoinKeys = columnMapped.Value.JoinKeys,
                            IsGraph = columnMapped.Value.IsGraph,
                            // JoinOneKeys = columnMapped.Value.JoinOneKeys,
                            LinkBusinessKeys = columnMapped.Value.LinkBusinessKeys,
                            LinkKeys = columnMapped.Value.LinkKeys
                        });
                }
            }
        }

        var properties = nodeToClass.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();

        var tree = new NodeTree();
        var childId = 0;
        var j = 0;
        for (var i = 0; i < properties.Count(); i++)
        {
            toProperty = nodeToClass.GetType().GetProperties()
                .FirstOrDefault(n => n.Name.Matches(properties[i].Name));

            nonNullableToType = Nullable.GetUnderlyingType(toProperty?.PropertyType!) ?? toProperty?.PropertyType;

            M toVariable = null;

            if (GraphQLFieldExtension.IsPrimitiveType(nonNullableFromType))
            {
                continue;
            }

            if (typeof(IList).IsAssignableFrom(nonNullableToType))
            {
                if (toVariable == null)
                {
                    toVariable = (M)Convert.ChangeType(
                        Activator.CreateInstance(nonNullableToType.GenericTypeArguments[0]),
                        nonNullableToType.GenericTypeArguments[0])!;
                }
            }
            else if (nonNullableToType.IsClass && nonNullableToType != typeof(string))
            {
                if (toVariable == null)
                {
                    toVariable = (M)Convert.ChangeType(
                        Activator.CreateInstance(nonNullableToType),
                        nonNullableToType)!;
                }
            }
            
            E fromVariable = null;
            
            if (typeof(IList).IsAssignableFrom(nonNullableFromType))
            {
                if (fromVariable == null)
                {
                    fromVariable = (E)Convert.ChangeType(
                        Activator.CreateInstance(nonNullableFromType.GenericTypeArguments[0]),
                        nonNullableFromType.GenericTypeArguments[0])!;
                }
            }
            else if (nonNullableFromType.IsClass && nonNullableFromType != typeof(string))
            {
                if (fromVariable == null)
                {
                    fromVariable = (E)Convert.ChangeType(
                        Activator.CreateInstance(nonNullableFromType),
                        nonNullableFromType)!;
                }
            }

            if (toVariable == null || fromVariable == null)
            {
                continue;
            }

            tree = IterateTreeDictionary<E, M>(nodeTrees,
                fromVariable, toVariable, toVariable?.GetType().Name, name,
                mapperConfiguration, nodeId, isModel, models, entities, visitedNode,
                linkEntityDictionaryTreeNode,
                linkModelDictionaryTreeNode,
                linkEntityDictionaryTreeEdge,
                linkModelDictionaryTreeEdge,
                linkEntityDictionaryTreeMutation,
                linkModelDictionaryTreeMutation,
                upsertKeys, joinKeys, joinOneKeys, linkKeys, linkBusinessKeys);

            if (tree != null && !string.IsNullOrEmpty(tree.Name))
            {
                var fieldMap = tree.Mapping.FirstOrDefault(f => !string.IsNullOrEmpty(f.FieldDestinationSchema));
                if (fieldMap != null)
                {
                    tree.Mapping.ForEach(f => f.FieldDestinationSchema = fieldMap.FieldDestinationSchema);
                }

                node.ChildrenName.Add(tree.Name);
                node.Children.Add(tree);

                if (nodeTrees.TryGetValue(node.Name, out _))
                {
                    nodeTrees[node.Name] = node;
                }
                else
                {
                    nodeTrees.Add(node.Name, node);
                }
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