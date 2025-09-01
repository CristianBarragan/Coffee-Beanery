using AutoMapper;
using AutoMapper.EquivalencyExpression;
using Domain.Model;
using Domain.Shared.Mapping;
using Domain.Shared.Query;
using Domain.Shared.Service;
using Domain.Util.GraphQL.Helper;
using Domain.Util.GraphQL.Model;
using FASTER.core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DatabaseCommon = Database.Entity;

namespace Domain.Shared.Extension;

public static class ModelServiceCollectionRegistration
{
    public static IServiceCollection AddBankingDomainModelServiceCollection(this IServiceCollection services, bool ignoreOtherDomainRelationships)
    {
        services = AddProcessServiceCollection(services, ignoreOtherDomainRelationships);

        services = AddCache(services);
        
        services.AddScoped<IProcessService<dynamic, dynamic, dynamic>, ProcessService<dynamic, dynamic, dynamic>>();
        services.AddScoped<IQuery<SqlStructure, 
                (List<dynamic> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>, 
            ProcessQuery<dynamic, dynamic, dynamic>>();
        
        services.AddScoped<IProcessService<Customer, dynamic, dynamic>, ProcessService<Customer,  dynamic, dynamic>>();
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

    private static IServiceCollection AddProcessServiceCollection(this IServiceCollection services, bool ignoreOtherDomainRelationships)
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
        
        var domainEntityTypes = new List<dynamic>();
        var databaseEntityTypes = new List<dynamic>();
        var entities = new List<string>();
        var nodeId = new List<KeyValuePair<string,string>>();
        var dictionaryTree = new Dictionary<string, NodeTree>(StringComparer.OrdinalIgnoreCase);
        
        var nodeTree = NodeTreeHelper.GenerateTree<dynamic, dynamic>(dictionaryTree, entities, databaseEntityTypes,
            (dynamic)Activator.CreateInstance(typeof(DatabaseCommon.Customer)),
            domainEntityTypes, (dynamic)Activator.CreateInstance(typeof(Customer)),
            nameof(DatabaseCommon.Customer),
            mapperConfiguration, ignoreOtherDomainRelationships, nodeId);

        var treeMap = new TreeMap<dynamic, dynamic>()
        {
            NodeId = nodeId,
            EntityNames = entities,
            ModelTypes = domainEntityTypes,
            EntityTypes = databaseEntityTypes,
            NodeTree = nodeTree,
            DictionaryTree = dictionaryTree
        };
        
        services.AddSingleton<ITreeMap<dynamic, dynamic>>(
            (ITreeMap<dynamic, dynamic>)treeMap);
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
}