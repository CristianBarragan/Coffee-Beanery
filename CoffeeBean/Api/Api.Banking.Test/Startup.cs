using Api.Banking.Extension;
using Api.Banking.Mutation;
using Api.Banking.Query;
using HotChocolate.Execution;
using HotChocolate.Types.Pagination;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Banking.Test;

public class Startup
{
    public IHostBuilder CreateHostBuilder()
    {
        return new HostBuilder().ConfigureServices(services =>
        {
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", true, true)
                .AddEnvironmentVariables().Build();
            
            var connectionString = configuration.GetConnectionString("BankingConnectionString");
                          
            services.AddNpgsqlDataSource(connectionString!);
                                  
            services.AddBankingServiceCollection(true);
                                  
            services.AddControllers().AddNewtonsoftJson();
                          
            // builder.Services
            //     .AddAuthorization(
            //         options =>
            //         {
            //             options.AddPolicy("Admin", policy => policy.RequireClaim("role", "admin"));
            //         });
                          
            services
                .AddGraphQLServer()
                .AddQueryType(d =>
                {
                    d.Field("customer")
                      .ResolveWith<CustomerQueryResolver>(r => r.GetCustomer(default, default, default));
                })
                .AddMutationType(d =>
                {
                    d.Name("Mutation");

                    d.Field("customer")
                        .Argument("customer", d => d.Type<CustomerInputType>())
                        .ResolveWith<CustomerMutationResolver>(r => r.UpsertCustomer(default, default, default));
                })
                .AddProjections()
                .SetPagingOptions(new PagingOptions()  {     DefaultPageSize = 10,     IncludeTotalCount = true  })  
                .AddFiltering()  
                .AddSorting()
                .Services
                .AddSingleton(
                    sp => new RequestExecutorProxy(
                        sp.GetRequiredService<IRequestExecutorResolver>(),
                        Schema.DefaultName))
                .BuildServiceProvider();
        });
    }
}