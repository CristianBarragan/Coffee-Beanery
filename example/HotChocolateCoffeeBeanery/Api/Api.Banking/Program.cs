using Amazon;
using Amazon.RDS.Util;
using Api.Banking.Extension;
using Api.Banking.Mutation;
using Api.Banking.Query;
using HotChocolate.AspNetCore;
using HotChocolate.Types.Pagination;

namespace Api.Banking;

public class Program
{
    public static void Main(string[] args)
    {
        var app = CreateHostBuilder(args);
        app.UseWebSockets();
        app.UseRouting();
        app.UseHttpsRedirection();
        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        app.MapGraphQL();
        app.MapBananaCakePop("/graphql-ui/").WithOptions(new GraphQLToolOptions()
            { ServeMode = GraphQLToolServeMode.Embedded });
        app.MapControllers();

        app.Run();
    }

    public static WebApplication CreateHostBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var services = builder.Services;

        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables()
            .AddCommandLine(args).Build();

        var connectionString = configuration.GetConnectionString("BankingConnectionString");

        var isRds = false;

        if (isRds)
        {
            services.AddNpgsqlDataSource(connectionString!, dataSourceBuilder =>
            {
                dataSourceBuilder.UsePeriodicPasswordProvider(async (settings, cancellationToken) =>
                    {
                        return await Task.Run(
                            () => RDSAuthTokenGenerator.GenerateAuthToken(RegionEndpoint.APSoutheast2, settings.Host,
                                settings.Port,
                                settings.Username), cancellationToken);
                    }, TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10));
            });
        }
        else
        {
            builder.Services.AddNpgsqlDataSource(connectionString!);
        }

        builder.Services.AddBankingServiceCollection();

        builder.Services.AddControllers().AddNewtonsoftJson();

        builder.Services.AddGraphQLServer()
            .AddQueryType(d =>
            {
                d.Field("customer")
                    .ResolveWith<CustomerQueryResolver>(r => r.GetCustomer(default, default,
                        default));
            })
            .AddMutationType(d =>
            {
                d.Name("Mutation");

                d.Field("wrapper")
                    .Argument("wrapper", d => d.Type<CustomerInputType>())
                    .ResolveWith<CustomerMutationResolver>(r => r.UpsertCustomer(default, default, default));
            })
            .SetPagingOptions(new PagingOptions() { DefaultPageSize = 10, IncludeTotalCount = true })
            .AddFiltering()
            .AddSorting()
            .InitializeOnStartup();

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        return builder.Build();
    }
}