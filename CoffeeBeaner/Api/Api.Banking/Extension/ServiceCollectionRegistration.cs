using Domain.Shared.Extension;

namespace Api.Banking.Extension;

public static class ServiceCollectionRegistration
{
    public static IServiceCollection AddBankingServiceCollection(this IServiceCollection services,
        bool ignoreOtherDomainRelationships)
    {
        services.AddBankingDomainModelServiceCollection(ignoreOtherDomainRelationships);
        return services;
    }
}