using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using AutoMapper;
using AutoMapper.Configuration.Annotations;
using Domain.Util.GraphQL.Configuration;
using Domain.Util.GraphQL.Model;
using Domain.Util.GraphQL.Extension;

namespace Domain.Util.GraphQL.Helper;

    public static class NodeTreeHelper
    {
        public static NodeTree GenerateTree<T, M>(Dictionary<string, NodeTree> nodeTrees, List<string> entities, List<T> databaseTypes, T nodeDatabaseClass,
            List<M> domainTypes,
            M nodeDomainClass, string name, MapperConfiguration mapperConfiguration, bool ignoreNotMapped, List<KeyValuePair<string, string>> nodeId)
            where T : class where M : class
        {
            if (!entities.Contains(nodeDomainClass.GetType().Name))
            {
                entities.Add(nodeDomainClass.GetType().Name);
            }

            databaseTypes.Add(nodeDatabaseClass);
            domainTypes.Add(nodeDomainClass);

            return IterateTree<T, M>(nodeTrees, entities, databaseTypes, nodeDatabaseClass, domainTypes, nodeDomainClass, name,
                string.Empty,
                1, 0, mapperConfiguration, ignoreNotMapped, nodeId);
        }

        private static NodeTree? IterateTree<T, M>(Dictionary<string, NodeTree> nodeTrees, List<string> entities, List<T> databaseTypes, T? nodeDatabaseClass,
            List<M> domainTypes,
            M? nodeDomainClass, string name, string parentName, int id, int parentId,
            MapperConfiguration mapperConfiguration, bool ignoreNotMapped, List<KeyValuePair<string, string>> nodeId)
            where T : class
        {
            var nonNullableType = Nullable.GetUnderlyingType(nodeDomainClass.GetType()) ?? nodeDomainClass.GetType();

            var schema = string.Empty;

            if (typeof(IList).IsAssignableFrom(nonNullableType))
            {
                nodeDomainClass = (M)Convert.ChangeType(Activator.CreateInstance(nonNullableType.GenericTypeArguments[0]),
                    nonNullableType.GenericTypeArguments[0]);

                schema = nodeDomainClass.GetType().GetProperty("Schema").GetValue(nodeDomainClass).ToString();
            }
            else
            {
                schema = nonNullableType.GetProperty("Schema").GetValue(nodeDomainClass).ToString();
            }

            if (nonNullableType.GetProperties().Length == 0 ||
                (entities.Contains(nodeDomainClass.GetType().Name) && !string.IsNullOrEmpty(parentName)) ||
                nodeDomainClass.GetType().Name == "Process")
            {
                return null;
            }

            nodeId.Add(new KeyValuePair<string, string>(nodeDomainClass.GetType().Name, id.ToString()));
            
            var node = new NodeTree()
            {
                Name = nodeDomainClass.GetType().Name,
                ParentName = parentName,
                Id = id,
                ParentId = parentId,
                Mappings = GraphQLMapper.GetMappings(mapperConfiguration, nodeDomainClass.GetType().Name),
                Children = [],
                UpsertKeys = [],
                ChildrenNames = [],
                EnumerationMappings =
                    new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase),
                Schema = schema
            };
            
            
            
            for (var i = 0;
                 i < nodeDatabaseClass.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)!.Length;
                 i++)
            {
                var databaseProperty =
                    nodeDatabaseClass.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)[i];
                var domainProperty = nodeDomainClass.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x =>
                        x.Name == node.Mappings.FirstOrDefault(k => k.Key.Matches(databaseProperty.Name)).Value);
                
                if (databaseProperty == null || domainProperty == null)
                {
                    continue;
                }

                var t = Nullable.GetUnderlyingType(databaseProperty.PropertyType) ?? databaseProperty.PropertyType;
                var m = Nullable.GetUnderlyingType(domainProperty.PropertyType) ?? domainProperty.PropertyType;
                
                if (domainProperty.Name == "Process" || entities.Any(e => e == domainProperty.Name) ||
                    Attribute.IsDefined(domainProperty, typeof(IgnoreAttribute)))
                {
                    continue;
                }
                
                if (!IsPrimitiveType(m) && (m.IsClass || typeof(IList).IsAssignableFrom(m)))
                {
                    if (typeof(IList).IsAssignableFrom(m))
                    {
                        var varDomain = (M)Convert.ChangeType(
                            Activator.CreateInstance(domainProperty.PropertyType.GenericTypeArguments[0]),
                            domainProperty.PropertyType.GenericTypeArguments[0]);
                        var varDatabase = (T)Convert.ChangeType(
                            Activator.CreateInstance(databaseProperty.PropertyType.GenericTypeArguments[0]),
                            databaseProperty.PropertyType.GenericTypeArguments[0]);

                        var tree = IterateTree<T, M>(nodeTrees,entities, databaseTypes,
                            varDatabase,
                            domainTypes,
                            varDomain,
                            varDomain.GetType().Name, name,
                            id + 1, id,
                            mapperConfiguration, ignoreNotMapped, nodeId);

                        AddEntity(entities, databaseTypes, domainTypes,
                            varDatabase, varDomain);

                        if (tree is not null)
                        {
                            node.ChildrenNames.Add(tree.Name);
                            node.Children.Add(tree);
                            id += 1;
                        }
                    }
                    else
                    {
                        var varDomain = (M)Convert.ChangeType(Activator.CreateInstance(m), m);
                        var varDatabase = (T)Convert.ChangeType(Activator.CreateInstance(t), t);

                        var tree = IterateTree<T, M>(nodeTrees,entities, databaseTypes,
                            varDatabase,
                            domainTypes,
                            varDomain,
                            varDomain.GetType().Name, name,
                            id + 1, id,
                            mapperConfiguration, ignoreNotMapped, nodeId);

                        AddEntity(entities, databaseTypes, domainTypes,
                            varDatabase, varDomain);

                        if (tree is not null)
                        {
                            node.ChildrenNames.Add(tree.Name);
                            node.Children.Add(tree);
                            id += 1;
                        }
                    }
                    
                    continue;
                }
                
                if (Attribute.IsDefined(domainProperty, typeof(SchemaAttribute)))
                {
                    if (domainProperty.PropertyType.IsEnum)
                    {
                        node.Schema = Enum.GetValues(typeof(Schema))
                            .Cast<Schema>().FirstOrDefault(s => s.ToString().Matches(domainProperty.GetType()
                                .GetProperty("Schema").GetValue(domainProperty).ToString())).ToString();
                    }

                    continue;
                }

                if (ignoreNotMapped && Attribute.IsDefined(domainProperty, typeof(NotMappedAttribute)))
                {
                    continue;
                }

                if (Attribute.IsDefined(domainProperty, typeof(BusinessKeyAttribute)))
                {
                    node.UpsertKeys.Add(domainProperty.Name);
                    continue;
                }

                if (Attribute.IsDefined(domainProperty, typeof(JoinKeyAttribute)))
                {
                    node.JoinKey = domainProperty.Name;
                    continue;
                }

                if (m.IsEnum)
                {
                    var enumDictionary = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    var k = 0;
                    foreach (var value in Enum.GetValues(m))
                    {
                        enumDictionary.Add(value.ToString(), k.ToString());
                        k++;
                    }

                    node.EnumerationMappings.Add(domainProperty.Name, enumDictionary);
                    continue;
                }
            }
            
            nodeTrees.Add(node.Name, node);
            return node;
        }

        private static bool IsPrimitiveType(Type m)
        {
            return m == typeof(string) || m == typeof(bool) ||
                   m == typeof(Guid) || m == typeof(DateTime) ||
                   m == typeof(decimal) || m == typeof(int);
        }

        private static void AddEntity<T, M>(List<string> entities,
            List<T> databaseTypes, List<M> domainTypes, T varDatabase, M varDomain)
            where T : class
        {
            if (!entities.Contains(varDomain.GetType().Name))
            {
                entities.Add(varDomain.GetType().Name);
                databaseTypes.Add(varDatabase);
                domainTypes.Add(varDomain);
            }
        }
    }