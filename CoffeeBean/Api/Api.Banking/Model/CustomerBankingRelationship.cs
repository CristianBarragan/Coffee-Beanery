// using System.ComponentModel.DataAnnotations.Schema;
// using Domain.Shared.GraphQL.Configuration;
// using Domain.Shared.Model;
// using Schema = Database.Common.Configuration.Schema;
//
// namespace Api.Banking.Model;
//
// public class CustomerBankingRelationship : Process
// {
//     [BusinessKey]
//     public Guid CustomerBankingRelationshipKey { get; set; }
//
//     public Guid? CustomerKey { get; set; }
//     
//     public Guid? ContractKey { get; set; }
//
//     [JoinKey]
//     public int? CustomerId { get; set; }
//         
//     [GraphQLIgnore]
//     public Customer? Customer { get; set; }
//     
//     public List<Contract>? Contract { get; set; }
//
//     [NotMapped, BusinessSchema, GraphQLIgnore]
//     public Schema Schema { get; set; } =  Schema.Banking;
// }