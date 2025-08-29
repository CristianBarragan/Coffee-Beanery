// using System.ComponentModel.DataAnnotations.Schema;
// using Domain.Shared.GraphQL.Configuration;
// using Domain.Shared.Model;
// using Schema = Database.Common.Configuration.Schema;
//
// namespace Api.Banking.Model;
//
// public class Customer : Process
// {
//     [BusinessKey]
//     public Guid? CustomerKey { get; set; }
//
//     public string? FirstNaming { get; set; }
//
//     public string? LastNaming { get; set; }
//
//     public string? FullNaming { get; set; }
//     
//     public CustomerType? CustomerType { get; set; }
//     
//     public List<ContactPoint>? ContactPoint { get; set; }
//     
//     public List<CustomerBankingRelationship>? CustomerBankingRelationship { get; set; }
//
//     [NotMapped, BusinessSchema, GraphQLIgnore]
//     public Schema Schema { get; set; } =  Schema.Banking;
// }