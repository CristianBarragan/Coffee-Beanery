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
        var joinKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var upsertKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var linkKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var modelNodeTree = NodeTreeHelper.GenerateTree<dynamic, dynamic>(modelDictionaryTree,
            (dynamic)Activator.CreateInstance(typeof(DatabaseCommon.Wrapper))!,
            (dynamic)Activator.CreateInstance(typeof(Wrapper))!,
            nameof(DatabaseCommon.Wrapper),
            mapperConfiguration, modelNodeId, true, linkEntityDictionaryTree, upsertKeys, linkKeys, joinKeys);
        
        var entityNodeTree = NodeTreeHelper.GenerateTree<dynamic, dynamic>(entityDictionaryTree,
            (dynamic)Activator.CreateInstance(typeof(Wrapper))!,
            (dynamic)Activator.CreateInstance(typeof(DatabaseCommon.Wrapper))!,
            nameof(Wrapper),
            mapperConfiguration, entityNodeId, false);
        
        foreach (var tree in modelDictionaryTree)
        {
            var entityType = Type.GetType($"{typeof(DatabaseCommon.Customer).Namespace}" +
                                          $".{tree.Value.Name},{typeof(DatabaseCommon.Customer).Assembly}");
            var modelType = Type.GetType($"{typeof(Customer).Namespace}" +
                                         $".{tree.Value.Name},{typeof(Customer).Assembly}");

            if (entityType != null)
            {
                entities.Add(entityType.Name);
                entityTypes.Add(entityType);    
            }
            
            if (modelType != null)
            {
                models.Add(modelType.Name);
                modelTypes.Add(modelType);    
            }
        }

        foreach (var upsertKey in upsertKeys)
        {
            foreach (var linkEntity in linkEntityDictionaryTree
                .Where(l => upsertKey.Key
                    .Matches(l.Key)).Select(l => l.Key))
            {
                var link = linkEntityDictionaryTree[upsertKey.Key];
                if (!link.UpsertKeys.Any(u => u.Matches(upsertKey.Value)))
                {
                    link.UpsertKeys.Add(upsertKey.Value);
                }
                var entityId = entityNodeId.First(id => id.Key.Matches(linkEntity.Split('~')[0])).Value;
                link.SelectColumn = $"\"{link.SelectColumn}\" AS \"{link.SelectColumn.ToSnakeCase(entityId)}\"";
            }
        }
        
        foreach (var joinKey in joinKeys)
        {
            foreach (var _ in linkEntityDictionaryTree
                         .Where(l => joinKey.Key
                             .Matches(l.Key)).Select(l => l.Key))
            {
                var link = linkEntityDictionaryTree[joinKey.Key];
                AddToDictionary(link.JoinKeys, joinKey.Key, joinKey.Value);
            }
        }
        
        foreach (var linkKey in linkKeys)
        {
            foreach (var _ in linkEntityDictionaryTree
                         .Where(l => linkKey.Key
                             .Matches(l.Key)).Select(l => l.Key))
            {
                var link = linkEntityDictionaryTree[linkKey.Key];
                AddToDictionary(link.LinkKeys, linkKey.Key, linkKey.Value);
            }
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
            LinkEntityDictionaryTree = linkEntityDictionaryTree
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
            LinkEntityDictionaryTree = linkEntityDictionaryTree
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