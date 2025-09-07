using AutoMapper;
using AutoMapper.EquivalencyExpression;
using CoffeeBeanery.CQRS;
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
        var nodeId = new List<KeyValuePair<string, int>>();
        var dictionaryTree = new Dictionary<string, NodeTree>(StringComparer.OrdinalIgnoreCase);

        var nodeTree = NodeTreeHelper.GenerateTree<dynamic, dynamic>(dictionaryTree, entities, models, entityTypes,
            (dynamic)Activator.CreateInstance(typeof(DatabaseCommon.Customer))!,
            modelTypes, (dynamic)Activator.CreateInstance(typeof(Customer))!,
            nameof(DatabaseCommon.Customer),
            mapperConfiguration, nodeId);

        var treeMap = new TreeMap<dynamic, dynamic>()
        {
            NodeId = nodeId,
            EntityNames = entities,
            ModelNames = models,
            ModelTypes = modelTypes,
            EntityTypes = entityTypes,
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