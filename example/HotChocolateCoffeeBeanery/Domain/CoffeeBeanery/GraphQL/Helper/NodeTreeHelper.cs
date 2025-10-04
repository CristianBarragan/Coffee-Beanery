using System.Collections;
using System.Reflection;
using AutoMapper;
using CoffeeBeanery.GraphQL.Configuration;
using CoffeeBeanery.GraphQL.Model;
using CoffeeBeanery.GraphQL.Extension;

namespace CoffeeBeanery.GraphQL.Helper;

public static class NodeTreeHelper
{
    public static NodeTree GenerateTree<E, M>(Dictionary<string, NodeTree> nodeTrees,
        E nodeFromClass, M nodeToClass, string name, string entityNamespaceName, MapperConfiguration mapperConfiguration,
        List<KeyValuePair<string, int>> nodeId, bool isModel, List<string> entities, List<string> models, 
        Dictionary<string, SqlNode>? linkEntityDictionaryTree, Dictionary<string, SqlNode>? linkModelDictionaryTree,
        List<string>? upsertKeys, List<JoinKey>? joinKeys, List<LinkKey>? linkKeys, List<LinkBusinessKey>? linkBusinessKeys)
        where E : class where M : class
    {
        var visitedNode = new List<string>();
        return IterateTree<E, M>(nodeTrees, nodeFromClass, nodeToClass,
            name, entityNamespaceName, string.Empty, mapperConfiguration, nodeId, isModel, 
            models, entities, visitedNode, linkEntityDictionaryTree, linkModelDictionaryTree,
            upsertKeys, joinKeys, linkKeys, linkBusinessKeys)!;
    }

    private static NodeTree? IterateTree<E, M>(Dictionary<string, NodeTree> nodeTrees,
        E? nodeFromClass, M? nodeToClass, string name, string entityNamespaceName, string parentName,
        MapperConfiguration mapperConfiguration, List<KeyValuePair<string, int>> nodeId, bool isModel, 
        List<string> models, List<string> entities, List<string> visitedNode, Dictionary<string, SqlNode>? linkEntityDictionaryTree,
        Dictionary<string, SqlNode>? linkModelDictionaryTree, List<string>? upsertKeys, List<JoinKey>? joinKeys, 
        List<LinkKey>? linkKeys, List<LinkBusinessKey>? linkBusinessKeys)
        where E : class where  M : class
    {
        if (visitedNode.Any(v => v.Matches(name)))
        {
            return new  NodeTree();
        }
        
        visitedNode.Add(name);
        
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

        if (!linkEntityDictionaryTree.ContainsKey($"{nodeToClass.GetType().Name}~Id"))
        {
            linkEntityDictionaryTree.Add($"{nodeToClass.GetType().Name}~Id",
                new SqlNode()
                {
                    Column = "Id",
                    Namespace = nodeToClass.GetType().Namespace
                });
        }
        
        var fromMapping = GraphQLMapper.GetMappings<E, M>(mapperConfiguration, 
            nodeFromClass, nodeToClass, isModel, models, entities, entityNamespaceName, linkEntityDictionaryTree, 
            linkModelDictionaryTree, linkKeys, linkBusinessKeys);

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
            var schemaValue = toProperty.CustomAttributes.First().ConstructorArguments[1].Value.ToString();
            node.Schema = schemaValue;
            node.Mapping.ForEach(f => f.FieldDestinationSchema = schemaValue);
                
            var upsertKeyAttribute = toProperty.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(UpsertKeyAttribute));
            var entity = upsertKeyAttribute.ConstructorArguments[0].Value;
            var column = toProperty.Name;

            upsertKeys.Add($"{entity}~{column}");
            
            if (!linkEntityDictionaryTree.ContainsKey($"{entity}~{column}"))
            {
                linkEntityDictionaryTree.Add($"{entity}~{column}",
                    new SqlNode()
                    {
                        Column = column,
                        Namespace = nodeToClass.GetType().Namespace
                    });
            }
        }
        
        toProperty = nodeToClass.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(t => t.CustomAttributes
                .Any(a => a.AttributeType == typeof(JoinKeyAttribute)));
        
        if (toProperty != null && toProperty.CustomAttributes
                .Any(a => a.AttributeType == typeof(JoinKeyAttribute)))
        {
            var attribute = toProperty.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(JoinKeyAttribute));
            var joinKey = new JoinKey()
            {
                From = $"{nodeToClass.GetType().Name}~{toProperty.Name}",
                To =
                    $"{attribute.ConstructorArguments[0].Value.ToString()}~{attribute.ConstructorArguments[1].Value.ToString()}"
            };
            
            joinKeys.Add(joinKey);
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
            else
            {
                continue;
            }
            
            tree = IterateTree<E, M>(nodeTrees,
                nodeFromClass, toVariable, toVariable.GetType().Name, entityNamespaceName, name,
                mapperConfiguration, nodeId, isModel, models, entities, visitedNode, linkEntityDictionaryTree, linkModelDictionaryTree, 
                upsertKeys, joinKeys, linkKeys, linkBusinessKeys);
            
            if (tree != null)
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