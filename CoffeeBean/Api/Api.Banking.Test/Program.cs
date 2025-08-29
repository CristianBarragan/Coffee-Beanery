// using Amazon;
// using Amazon.RDS.Util;
// using Api.Banking.Extension;
// using Api.Banking.Mutation;
// using Api.Banking.Query;
// using HotChocolate.Types.Pagination;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.DependencyInjection;
//
// namespace Api.Banking.Test;
//
// internal class Program
// {
//     public static void Main(string[] args)
//     {
//         var builder = WebApplication.CreateBuilder(args);
//         
//         IConfiguration configuration = new ConfigurationBuilder()
//             .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables()
//             .AddCommandLine(args).Build();
//
//         var connectionString = configuration.GetConnectionString("BankingConnectionString");
//                           
//                                   builder.Services.AddNpgsqlDataSource(connectionString!);
//                                   
//                                   builder.Services.AddBankingDomainModelServiceCollection(true);
//                                   
//                                   builder.Services.AddControllers().AddNewtonsoftJson();
//                           
//                                   // builder.Services
//                                   //     .AddAuthorization(
//                                   //         options =>
//                                   //         {
//                                   //             options.AddPolicy("Admin", policy => policy.RequireClaim("role", "admin"));
//                                   //         });
//                           
//                                   builder.Services
//                                       .AddGraphQLServer()
//                                       .AddQueryType(d =>
//                                       {
//                                           d.Field("customer")
//                                               .ResolveWith<CustomerQueryResolver>(r => r.GetCustomer(default, default, default));
//                                       })
//                                       .AddMutationType(d =>
//                                       {
//                                           d.Name("Mutation");
//                                           
//                                           d.Field("customer")
//                                               .Argument("customer", d => d.Type<CustomerInputType>())
//                                               .ResolveWith<CustomerMutationResolver>(r => r.UpsertCustomer(default, default, default, default));
//                                       })
//                                       .AddProjections()
//                                       .SetPagingOptions(new PagingOptions()  {     DefaultPageSize = 10,     IncludeTotalCount = true  })  
//                                       .AddFiltering()  
//                                       .AddSorting();
//
//         var app = builder.Build();
//
//         app.MapGraphQL();
//         app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
//         app.MapGraphQL();
//         app.UseRouting();
//         app.MapControllers();
//
//         app.Run();
//     }
// }