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
        E nodeFromClass, M nodeToClass, string name, MapperConfiguration mapperConfiguration,
        List<KeyValuePair<string, int>> nodeId, bool isModel, Dictionary<string, SqlNode>? linkEntityDictionaryTree = null,
        Dictionary<string, string>? upsertKeys = null, Dictionary<string, string>? linkKeys = null,
        Dictionary<string, string>? joinKeys = null)
        where E : class where M : class
    {
        var visitedNode = new List<string>();
        return IterateTree<E, M>(nodeTrees, nodeFromClass, nodeToClass,
            name, string.Empty, mapperConfiguration, nodeId, isModel, visitedNode, linkEntityDictionaryTree,
            upsertKeys, linkKeys, joinKeys)!;
    }

    private static NodeTree? IterateTree<E, M>(Dictionary<string, NodeTree> nodeTrees,
        E? nodeFromClass,
        M? nodeToClass, string name, string parentName,
        MapperConfiguration mapperConfiguration, List<KeyValuePair<string, int>> nodeId, 
        bool isModel, List<string> visitedNode, 
        Dictionary<string, SqlNode>? linkEntityDictionaryTree = null,
        Dictionary<string, string>? upsertKeys = null, Dictionary<string, string> linkKeys = null,
        Dictionary<string, string> joinKeys = null)
        where E : class where  M : class
    {
        if (visitedNode.Any(v => v.Matches(name)))
        {
            return null;
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
                    InsertColumn = "Id",
                    SelectColumn = "Id",
                    ExludedColumn  = string.Empty
                });
        }
        
        var fromMapping = GraphQLMapper.GetMappings<E, M>(mapperConfiguration, 
            nodeFromClass, nodeToClass, isModel, linkEntityDictionaryTree, upsertKeys);

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
            Mappings = fromMapping,
            Children = [],
            ChildrenNames = []
        };
        
        var schema = node.Mappings.FirstOrDefault(f => !string.IsNullOrEmpty(f.FieldDestinationSchema));
        if (schema != null)
        {
            node.Mappings.ForEach(f => f.FieldDestinationSchema = schema.FieldDestinationSchema);
            node.Schema = schema.FieldDestinationSchema;
        }

        var properties = nodeToClass.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();

        var tree = new NodeTree();
        var childId = 0;
        var j = 0;
        for (var i = 0; i < properties.Count(); i++)
        {
            var schemaValue = string.Empty;
            var toProperty = nodeToClass.GetType().GetProperties()
                .FirstOrDefault(n => n.Name.Matches(properties[i].Name));
            
            nonNullableToType = Nullable.GetUnderlyingType(toProperty?.PropertyType!) ?? toProperty?.PropertyType;
            
            M toVariable = null;
            
            if (toProperty != null && toProperty.CustomAttributes
                    .Any(a => a.AttributeType == typeof(JoinKeyAttribute)))
            {
                var joinKeyAttribute = toProperty.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(JoinKeyAttribute));
                var model = joinKeyAttribute.ConstructorArguments[0].Value;
                
                if (joinKeys != null)
                {
                    var column = joinKeyAttribute.ConstructorArguments[1].Value.ToString();
                    var joinKey = $"{model}~{column}";
            
                    if (!joinKeys.ContainsKey(joinKey))
                    {
                        joinKeys.Add(joinKey,$"{nodeToClass.GetType().Name}~{properties[i].Name}");    
                    }
                }   
                
                var modelType = Type.GetType($"{nodeToClass.GetType().Namespace}.{model},{nodeToClass.GetType().Assembly}");
                toVariable = (M)Activator.CreateInstance(modelType);
            }
            
            
            if (toProperty != null && toProperty.CustomAttributes
                    .Any(a => a.AttributeType == typeof(LinkKeyAttribute)))
            {
                var linkKeyAttribute = toProperty.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(LinkKeyAttribute));
                var model = linkKeyAttribute.ConstructorArguments[0].Value;
                
                if (linkKeys != null)
                {
                    var column = linkKeyAttribute.ConstructorArguments[1].Value.ToString();
                    var linkKey = $"{model}~{column}";

                    if (!linkKeys.ContainsKey(linkKey))
                    {
                        linkKeys.Add(linkKey,$"{nodeToClass.GetType().Name}~{properties[i].Name}");    
                    }
                }
                
                var modelType = Type.GetType($"{nodeToClass.GetType().Namespace}.{model},{nodeToClass.GetType().Assembly}");
                toVariable = (M)Activator.CreateInstance(modelType);
            }
            
            if (toProperty != null && toProperty.CustomAttributes
                    .Any(a => a.AttributeType == typeof(UpsertKeyAttribute)))
            {
                schemaValue = toProperty.CustomAttributes.First().ConstructorArguments[1].Value.ToString();
                node.Schema = schemaValue;
                
                var upsertKeyAttribute = toProperty.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(UpsertKeyAttribute));
                var model = upsertKeyAttribute.ConstructorArguments[0].Value;
                
                var column = upsertKeyAttribute.ConstructorArguments[1].Value.ToString();
                var upsertKey = $"{model}~{column}";

                if (!upsertKeys.ContainsKey(upsertKey))
                {
                    upsertKeys.Add(upsertKey,$"{nodeToClass.GetType().Name}~{properties[i].Name}");    
                }
            }
            
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
                nodeFromClass,
                toVariable,
                toVariable.GetType().Name, name,
                mapperConfiguration, nodeId, isModel, visitedNode, linkEntityDictionaryTree, upsertKeys, linkKeys, joinKeys);
            
            if (tree != null)
            {
                var fieldMap = tree.Mappings.FirstOrDefault(f => !string.IsNullOrEmpty(f.FieldDestinationSchema));
                if (fieldMap != null)
                {
                    tree.JoinKey = tree.JoinKey ?? new List<FieldMap>();
                    tree.JoinKey.AddRange(tree.Mappings.Where(f => !tree.JoinKey.Contains(f)));
                    tree.UpsertKeys = tree.UpsertKeys ?? new List<FieldMap>();
                    tree.UpsertKeys.AddRange(tree.Mappings.Where(f => !tree.UpsertKeys.Contains(f)));
                    tree.Mappings.ForEach(f => f.FieldDestinationSchema = fieldMap.FieldDestinationSchema);
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
    
    /// <summary>
    /// Method for adding a value into a dictionary
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="values"></param>
    private static void AddToDictionary(Dictionary<string, List<string>> dictionary, string key, string value)
    {
        if (!dictionary.TryGetValue(key, out var currentValues))
        {
            currentValues = new List<string>();
            currentValues.Add(value);
            dictionary.Add(key, currentValues);
        }
        else
        {
            currentValues.Add(value);
            dictionary[key] = currentValues;
        }
    }
}