using AutoMapper;
using AutoMapper.EquivalencyExpression;
using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Extension;
using CoffeeBeanery.GraphQL.Helper;
using CoffeeBeanery.GraphQL.Model;
using CoffeeBeanery.Service;
using Domain.Model;
using Domain.Shared.Mapping;
using Domain.Shared.Query;
using FASTER.core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DatabaseCommon = Database.Entity;

namespace Domain.Shared.Extension;

public static class ModelServiceCollectionRegistration
{
    public static IServiceCollection AddBankingDomainModelServiceCollection(this IServiceCollection services)
    {
        services = AddProcessServiceCollection(services);

        services = AddCache(services);

        services.AddScoped<IProcessService<dynamic, dynamic, dynamic>, ProcessService<dynamic, dynamic, dynamic>>();
        services.AddScoped<IQuery<SqlStructure,
                (List<dynamic> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>,
            ProcessQuery<dynamic, dynamic, dynamic>>();

        services.AddScoped<IProcessService<Customer, dynamic, dynamic>, ProcessService<Customer, dynamic, dynamic>>();
        services.AddScoped<IQuery<SqlStructure,
                (List<dynamic> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>,
            ProcessQuery<dynamic, dynamic, dynamic>>();

        services.AddScoped<IQuery<SqlStructure,
                (List<Customer> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>,
            CustomerQueryHandler<Customer, dynamic, dynamic>>();

        return services;
    }

    private static IServiceCollection AddCache(this IServiceCollection services)
    {
        var store = new FasterKV<string, string>(128,
            new LogSettings
            {
                LogDevice = Devices.CreateLogDevice("C:/database"),
                ObjectLogDevice = new ManagedLocalStorageDevice("C:/database")
            });
        store.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver);
        services.AddSingleton<IFasterKV<string, string>>(store);
        return services;
    }

    private static IServiceCollection AddProcessServiceCollection(this IServiceCollection services)
    {
        var mappingProfile = new MappingProfile();

        var mapperConfiguration = new MapperConfiguration(cfg =>
        {
            AutoMapper.Internal.InternalApi.Internal(cfg).ForAllMaps((typeMap, mappingExpression) =>
            {
                mappingExpression.ForAllMembers(memberOptions =>
                {
                    memberOptions.Condition((src, dest, srcMember) => srcMember != null);
                });
            });
            cfg.AddCollectionMappers();
            cfg.AddProfile(mappingProfile);
        }, new LoggerFactory());
        mapperConfiguration.AssertConfigurationIsValid();

        var modelTypes = new List<dynamic>();
        var entityTypes = new List<dynamic>();
        var entities = new List<string>();
        var models = new List<string>();
        var modelNodeId = new List<KeyValuePair<string, int>>();
        var entityNodeId = new List<KeyValuePair<string, int>>();
        var modelDictionaryTree = new Dictionary<string, NodeTree>(StringComparer.OrdinalIgnoreCase);
        var entityDictionaryTree = new Dictionary<string, NodeTree>(StringComparer.OrdinalIgnoreCase);
        var linkEntityDictionaryTree = new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
        var linkModelDictionaryTree = new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
        var linkKeys = new List<LinkKey>();
        var linkBusinessKeys = new List<LinkBusinessKey>();
        var joinKeys = new List<JoinKey>();
        var upsertKeys = new List<string>();
        
        var modelNodeTree = NodeTreeHelper.GenerateTree<dynamic, dynamic>(modelDictionaryTree,
            (dynamic)Activator.CreateInstance(typeof(DatabaseCommon.Wrapper))!,
            (dynamic)Activator.CreateInstance(typeof(Wrapper))!,
            nameof(DatabaseCommon.Wrapper), typeof(DatabaseCommon.Wrapper).Namespace,
            mapperConfiguration, modelNodeId, true, entities, models, linkEntityDictionaryTree,
            linkModelDictionaryTree, upsertKeys, joinKeys, linkKeys, linkBusinessKeys);
        
        foreach (var tree in modelDictionaryTree)
        {
            var entityType = Type.GetType($"{typeof(DatabaseCommon.Customer).Namespace}" +
                                          $".{tree.Value.Name},{typeof(DatabaseCommon.Customer).Assembly}");
            var modelType = Type.GetType($"{typeof(Customer).Namespace}" +
                                         $".{tree.Value.Name},{typeof(Customer).Assembly}");

            if (entityType != null && !entityType.Name.Matches(nameof(Wrapper)))
            {
                entities.Add(entityType.Name);
                entityTypes.Add(entityType);    
            }
            
            if (modelType != null && !modelType.Name.Matches(nameof(Wrapper)))
            {
                models.Add(modelType.Name);
                modelTypes.Add(modelType);    
            }
        }
        
        var entityNodeTree = NodeTreeHelper.GenerateTree<dynamic, dynamic>(entityDictionaryTree,
            (dynamic)Activator.CreateInstance(typeof(Wrapper))!,
            (dynamic)Activator.CreateInstance(typeof(DatabaseCommon.Wrapper))!,
            nameof(Wrapper), typeof(DatabaseCommon.Wrapper).Namespace,
            mapperConfiguration, entityNodeId, false, entities, models, linkEntityDictionaryTree,
            linkModelDictionaryTree, upsertKeys, joinKeys, linkKeys, linkBusinessKeys);

        foreach (var entity in entities)
        {
            foreach (var upsertKey in upsertKeys.Where(u => entity.Matches(u.Split('~')[0])))
            {
                foreach (var linkEntity in linkEntityDictionaryTree
                             .Where(l => entity
                                 .Matches(l.Key.Split('~')[0])))
                {
                    if (!linkEntity.Value.UpsertKeys.Any(u => u.Matches(upsertKey)))
                    {
                        linkEntity.Value.UpsertKeys.Add(upsertKey);
                    }
                }
            }
        }
        
        foreach (var entity in entities)
        {
            foreach (var joinKey in joinKeys.Where(u => entity.Matches(u.From.Split('~')[0])))
            {
                foreach (var linkEntity in linkEntityDictionaryTree
                             .Where(l => entity
                                 .Matches(l.Key.Split('~')[0])))
                {
                    if (!linkEntity.Value.JoinKeys.Any(u => u.From.Matches(joinKey.From)))
                    {
                        linkEntity.Value.JoinKeys.Add(joinKey);
                    }
                }
            }
        }
        
        foreach (var entity in entities)
        {
            foreach (var linkKey in linkKeys.Where(u => entity.Matches(u.From.Split('~')[0])))
            {
                foreach (var linkEntity in linkEntityDictionaryTree
                             .Where(l => linkKey.From.Split('~')[0]
                                 .Matches(l.Key.Split('~')[0]) || linkKey.To.Split('~')[0]
                                 .Matches(l.Key.Split('~')[0])))
                {
                    if (!linkEntity.Value.LinkKeys.Any(u => u.From.Matches(linkKey.From)))
                    {
                        linkEntity.Value.LinkKeys.Add(linkKey);
                    }
                    
                    if (!linkEntity.Value.LinkKeys.Any(u => u.To.Matches(linkKey.To)))
                    {
                        linkEntity.Value.LinkKeys.Add(linkKey);
                    }
                }
            }
        }
        
        foreach (var entity in entities)
        {
            foreach (var linkBusinessKey in linkBusinessKeys.Where(u => entity.Matches(u.From.Split('~')[0])))
            {
                foreach (var linkEntity in linkEntityDictionaryTree
                             .Where(l => linkBusinessKey.From.Split('~')[0]
                                 .Matches(l.Key.Split('~')[0])))
                {
                    if (!linkEntity.Value.LinkBusinessKeys.Any(u => u.From.Matches(linkBusinessKey.From)))
                    {
                        linkEntity.Value.LinkBusinessKeys.Add(linkBusinessKey);
                    }
                }
            }
        }

        foreach (var modelTree in modelDictionaryTree)
        {
            if (entityDictionaryTree.TryGetValue(modelTree.Key, out var value))
            {
                modelTree.Value.Schema = value.Schema;
            }
        }

        foreach (var linkSqlNode in linkEntityDictionaryTree)
        {
            linkSqlNode.Value.IsModel = !(linkSqlNode.Key.Matches(linkSqlNode.Value.RelationshipKey) &&
                                          entities.Any(a => a.Matches(linkSqlNode.Key.Split('~')[0])));
        }

        foreach (var linkedTree in linkEntityDictionaryTree
                     .Where(e => !entities.Any(a => a.Matches(e.Key.Split('~')[0]))
                     ))
        {
            linkEntityDictionaryTree.Remove(linkedTree.Key);
        }
        
        foreach (var linkedTree in linkModelDictionaryTree
                     .Where(e => !models.Any(a => a.Matches(e.Key.Split('~')[0]))
                     ))
        {
            linkModelDictionaryTree.Remove(linkedTree.Key);
        }
        
        var entityTreeMap = new EntityTreeMap<dynamic, dynamic>()
        {
            NodeId = entityNodeId,
            EntityNames = entities,
            ModelNames = models,
            ModelTypes = modelTypes,
            EntityTypes = entityTypes,
            NodeTree = entityNodeTree,
            DictionaryTree = entityDictionaryTree,
            LinkDictionaryTree = linkEntityDictionaryTree
        };

        var modelTreeMap = new ModelTreeMap<dynamic, dynamic>()
        {
            NodeId = modelNodeId,
            EntityNames = entities,
            ModelNames = models,
            ModelTypes = modelTypes,
            EntityTypes = entityTypes,
            NodeTree = modelNodeTree,
            DictionaryTree = modelDictionaryTree,
            LinkDictionaryTree = linkModelDictionaryTree
        };

        services.AddSingleton<IModelTreeMap<dynamic, dynamic>>(
            (IModelTreeMap<dynamic, dynamic>)modelTreeMap);
        services.AddSingleton<IEntityTreeMap<dynamic, dynamic>>(
            (IEntityTreeMap<dynamic, dynamic>)entityTreeMap);
        services.AddSingleton(mapperConfiguration.CreateMapper());
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<UnitOfWork, UnitOfWork>();
        services.AddScoped<IUnitOfWorkContext, UnitOfWorkContext>();
        services.AddScoped<IProcessService<dynamic, dynamic, dynamic>,
            ProcessService<dynamic, dynamic, dynamic>>();
        services.AddScoped<IQuery<SqlStructure,
                (List<dynamic> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>,
            ProcessQuery<dynamic, dynamic, dynamic>>();

        return services;
    }
    
    /// <summary>
    /// Method for adding a value into a dictionary
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="values"></param>
    private static void AddToDictionary(Dictionary<string, string> dictionary, string key, string value)
    {
        if (!dictionary.TryGetValue(key, out var _))
        {
            dictionary.Add(key, value);
        }
        else
        {
            dictionary[key] = value;
        }
    }
}