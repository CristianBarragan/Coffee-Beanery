// using System.ComponentModel.DataAnnotations.Schema;
// using Domain.Shared.GraphQL.Configuration;
// using Domain.Shared.Model;
// using Schema = Database.Common.Configuration.Schema;
//
// namespace Api.Banking.Model;
//
// public class ContactPoint : Process
// {
//     [BusinessKey]
//     public Guid ContactPointKey { get; set; }
//
//     public ContactPointType? ContactPointType { get; set; }
//
//     public string? ContactPointValue { get; set; }
//
//     public Guid? CustomerKey { get; set; }
//
//     [JoinKey]
//     public int? CustomerId { get; set; }
//     
//     [GraphQLIgnore]
//     public Customer? Customer { get; set; }
//
//     [NotMapped, BusinessSchema, GraphQLIgnore]
//     public Schema Schema { get; set; } =  Schema.Banking;
// }