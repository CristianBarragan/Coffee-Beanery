﻿using AutoMapper;
using AutoMapper.EquivalencyExpression;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Customer, DataEntity.Customer>()
            .EqualityComparison((src, dest) => src.CustomerKey == dest.CustomerKey)
            .ForMember(dest => dest.ContactPoint, opt => opt.MapFrom(ps => ps.ContactPoint))
            .ForMember(dest => dest.CustomerBankingRelationship, opt => opt.MapFrom(ps => ps.Product))
            .ForMember(dest => dest.CustomerType, opt => opt.MapFrom(ps => ps.CustomerType))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(ps => ps.FirstNaming))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(ps => ps.LastNaming))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(ps => ps.FullNaming))
            .ForMember(dest => dest.Id, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => opt.Ignore())
            .ReverseMap();
        
        CreateMap<Customer, Product>()
            .EqualityComparison((src, dest) => src.CustomerKey == dest.CustomerKey)
            .ForMember(dest => dest.CustomerBankingRelationship, opt => opt.MapFrom(ps => ps.Product))
            .ForMember(dest => dest.CustomerBankingRelationshipKey, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ContractKey, opt => 
                opt.Ignore())
            .ForMember(dest => dest.AccountKey, opt => 
                opt.Ignore())
            .ForMember(dest => dest.AccountName, opt => 
                opt.Ignore())
            .ForMember(dest => dest.AccountNumber, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Amount, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Balance, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProductType, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Contract, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Account, opt => 
                opt.Ignore())
            .ReverseMap();
        
        CreateMap<Transaction, DataEntity.Transaction>()
            .EqualityComparison((src, dest) => src.TransactionKey == dest.TransactionKey)
            .ForMember(dest => dest.TransactionKey, opt => opt.MapFrom(ps => ps.TransactionKey))
            .ForMember(dest => dest.AccountId, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Account, opt => opt.Ignore())
            .ForMember(dest => dest.AccountKey, opt => opt.MapFrom(ps => ps.AccountKey))
            .ForMember(dest => dest.Amount, opt => opt.MapFrom(ps => ps.Amount))
            .ForMember(dest => dest.Balance, opt => opt.MapFrom(ps => ps.Balance))
            .ForMember(dest => dest.ContractKey, opt => opt.MapFrom(ps => ps.ContractKey))
            .ForMember(dest => dest.ContractId, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Contract, opt => opt.Ignore())
            .ForMember(dest => dest.Id, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => opt.Ignore())
            .ReverseMap();
        
        CreateMap<Account, DataEntity.Account>()
            .EqualityComparison((src, dest) => src.AccountKey == dest.AccountKey)
            .ForMember(dest => dest.AccountKey, opt => opt.MapFrom(ps => ps.AccountKey))
            .ForMember(dest => dest.AccountName, opt => opt.MapFrom(ps => ps.AccountName))
            .ForMember(dest => dest.AccountNumber, opt => opt.MapFrom(ps => ps.AccountNumber))
            .ForMember(dest => dest.Contract, opt => opt.Ignore())
            .ForMember(dest => dest.Transaction, opt => opt.Ignore())
            .ForMember(dest => dest.Id, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => opt.Ignore())
            .ReverseMap();
        
        CreateMap<ContactPoint, DataEntity.ContactPoint>()
            .EqualityComparison((src, dest) => src.ContactPointKey == dest.ContactPointKey)
            .ForMember(dest => dest.ContactPointKey, opt =>
                opt.MapFrom(a => a.ContactPointKey))
            .ForMember(dest => dest.ContactPointType, opt =>
                opt.MapFrom(a => a.ContactPointType))
            .ForMember(dest => dest.ContactPointValue, opt =>
                opt.MapFrom(a => a.ContactPointValue))
            .ForMember(dest => dest.CustomerKey, opt =>
                opt.MapFrom(a => a.CustomerKey))
            .ForMember(dest => dest.Id, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => opt.Ignore())
            .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Customer, opt => opt.Ignore())
            .ReverseMap();
        
        CreateMap<Contract, DataEntity.Contract>()
            .EqualityComparison((src, dest) => src.ContractKey == dest.ContractKey)
            .ForMember(dest => dest.ContractKey, opt => opt.MapFrom(ps => ps.ContractKey))
            .ForMember(dest => dest.AccountKey, opt => opt.MapFrom(ps => ps.AccountKey))
            .ForMember(dest => dest.Amount, opt => opt.MapFrom(ps => ps.Amount))
            .ForMember(dest => dest.ContractType, opt => opt.MapFrom(ps => ps.ContractType))
            .ForMember(dest => dest.CustomerBankingRelationshipKey, opt => opt.MapFrom(ps => ps.CustomerBankingRelationshipKey))
            .ForMember(dest => dest.Account, opt => opt.Ignore())
            .ForMember(dest => dest.AccountId, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Transaction, opt => opt.Ignore())
            .ForMember(dest => dest.CustomerBankingRelationship, opt => opt.Ignore())
            .ForMember(dest => dest.CustomerBankingRelationshipId, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Id, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => opt.Ignore())
            .ReverseMap();
        
        CreateMap<CustomerBankingRelationship, DataEntity.CustomerBankingRelationship>()
            .EqualityComparison((src, dest) => src.CustomerBankingRelationshipKey == dest.CustomerBankingRelationshipKey)
            .ForMember(dest => dest.CustomerBankingRelationshipKey, opt => opt.MapFrom(ps => ps.CustomerBankingRelationshipKey))
            .ForMember(dest => dest.CustomerKey, opt => opt.MapFrom(ps => ps.CustomerKey))
            .ForMember(dest => dest.Contract, opt => opt.Ignore())
            .ForMember(dest => dest.Customer, opt => opt.Ignore())
            .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Id, opt => opt.MapFrom(ps => 0))
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => opt.Ignore())
            .ReverseMap();

        CreateMap<Product, DataEntity.Account>()
            .EqualityComparison((src, dest) =>
                src.AccountKey == dest.AccountKey)
            .ForMember(dest => dest.AccountKey, opt =>
                opt.MapFrom(a => a.AccountKey))
            .ForMember(dest => dest.AccountName, opt =>
                opt.MapFrom(a => a.AccountName))
            .ForMember(dest => dest.AccountNumber, opt =>
                opt.MapFrom(a => a.AccountNumber))
            .ForMember(dest => dest.Id, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Contract, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Transaction, opt =>
                opt.Ignore())
            .ReverseMap();
        
        CreateMap<Product, Account>()
            .EqualityComparison((src, dest) =>
                src.AccountKey == dest.AccountKey)
            .ForMember(dest => dest.AccountKey, opt =>
                opt.MapFrom(a => a.AccountKey))
            .ForMember(dest => dest.AccountName, opt =>
                opt.MapFrom(a => a.AccountName))
            .ForMember(dest => dest.AccountNumber, opt =>
                opt.MapFrom(a => a.AccountNumber))
            .ForMember(dest => dest.Transaction, opt =>
                opt.Ignore())
            .ReverseMap();
        
        CreateMap<Account, DataEntity.Transaction>()
            .EqualityComparison((src, dest) =>
                src.AccountKey == dest.AccountKey)
            .ForMember(dest => dest.AccountKey, opt =>
                opt.MapFrom(a => a.AccountKey))
            .ForMember(dest => dest.TransactionKey, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Amount, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Balance, opt =>
                opt.Ignore())
            .ForMember(dest => dest.ContractKey, opt =>
                opt.Ignore())
            .ForMember(dest => dest.ContractId, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Account, opt =>
                opt.Ignore())
            .ForMember(dest => dest.AccountId, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Id, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Contract, opt =>
                opt.Ignore())
            .ReverseMap();
        
        CreateMap<Account, Transaction>()
            .EqualityComparison((src, dest) =>
                src.AccountKey == dest.AccountKey)
            .ForMember(dest => dest.AccountKey, opt =>
                opt.MapFrom(a => a.AccountKey))
            .ForMember(dest => dest.TransactionKey, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Amount, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Balance, opt =>
                opt.Ignore())
            .ForMember(dest => dest.ContractKey, opt =>
                opt.Ignore())
            .ReverseMap();
        
        CreateMap<Account, DataEntity.Contract>()
            .EqualityComparison((src, dest) =>
                src.AccountKey == dest.ContractKey)
            .ForMember(dest => dest.Amount, opt =>
                opt.Ignore())
            .ForMember(dest => dest.ContractKey, opt =>
                opt.Ignore())
            .ForMember(dest => dest.AccountKey, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Account, opt =>
                opt.Ignore())
            .ForMember(dest => dest.AccountId, opt =>
                opt.Ignore())
            .ForMember(dest => dest.ContractType, opt =>
                opt.Ignore())
            .ForMember(dest => dest.CustomerBankingRelationshipKey, opt =>
                opt.Ignore())
            .ForMember(dest => dest.CustomerBankingRelationship, opt =>
                opt.Ignore())
            .ForMember(dest => dest.CustomerBankingRelationshipId, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Id, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => 
                opt.Ignore())
            .ReverseMap();

        CreateMap<Product, DataEntity.Contract>()
            .EqualityComparison((src, dest) =>
                src.ContractKey == dest.ContractKey)
            .ForMember(dest => dest.ContractKey, opt =>
                opt.MapFrom(a => a.ContractKey))
            .ForMember(dest => dest.AccountKey, opt =>
                opt.MapFrom(a => a.AccountKey))
            .ForMember(dest => dest.CustomerBankingRelationshipKey, opt =>
                opt.MapFrom(a => a.CustomerBankingRelationshipKey))
            .ForMember(dest => dest.Amount, opt =>
                opt.MapFrom(a => a.Amount))
            .ForMember(dest => dest.ContractType, opt =>
                opt.MapFrom(a => a.ProductType))
            .ForMember(dest => dest.Id, opt => 
                opt.Ignore())
            .ForMember(dest => dest.AccountId, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Account, opt =>
                opt.Ignore())
            .ForMember(dest => dest.CustomerBankingRelationshipId, opt => 
                opt.Ignore())
            .ForMember(dest => dest.CustomerBankingRelationship, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Transaction, opt => 
                opt.Ignore())
            .ReverseMap();

        CreateMap<Contract, DataEntity.Transaction>()
            .EqualityComparison((src, dest) =>
                src.ContractKey == dest.ContractKey)
            .ForMember(dest => dest.ContractKey, opt =>
                opt.MapFrom(a => a.ContractKey))
            .ForMember(dest => dest.AccountKey, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Amount, opt =>
                opt.MapFrom(a => a.Amount))
            .ForMember(dest => dest.Balance, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Contract, opt =>
                opt.Ignore())
            .ForMember(dest => dest.ContractId, opt =>
                opt.Ignore())
            .ForMember(dest => dest.TransactionKey, opt =>
                opt.Ignore())
            .ForMember(dest => dest.Id, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Account, opt =>
                opt.Ignore())
            .ForMember(dest => dest.AccountId, opt =>
                opt.Ignore())
            .ReverseMap();

        CreateMap<Product, DataEntity.CustomerBankingRelationship>()
            .EqualityComparison((src, dest) =>
                src.CustomerBankingRelationshipKey == dest.CustomerBankingRelationshipKey)
            .ForMember(dest => dest.CustomerBankingRelationshipKey, opt =>
                opt.MapFrom(a => a.CustomerBankingRelationshipKey))
            .ForMember(dest => dest.CustomerKey, opt =>
                opt.MapFrom(a => a.CustomerKey))
            .ForMember(dest => dest.Customer, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Id, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Contract, opt => 
                opt.Ignore())
            .ForMember(dest => dest.CustomerId, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Customer, opt => 
                opt.Ignore())
            .ReverseMap();
        
        CreateMap<CustomerBankingRelationship, DataEntity.Contract>()
            .EqualityComparison((src, dest) =>
                src.CustomerBankingRelationshipKey == dest.CustomerBankingRelationshipKey)
            .ForMember(dest => dest.CustomerBankingRelationshipKey, opt =>
                opt.MapFrom(a => a.CustomerBankingRelationshipKey))
            .ForMember(dest => dest.Id, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Schema, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ProcessedDateTime, opt => 
                opt.Ignore())
            .ForMember(dest => dest.AccountKey, opt => 
                opt.Ignore())
            .ForMember(dest => dest.ContractKey, opt => 
                opt.Ignore())
            .ForMember(dest => dest.AccountId, opt =>
                opt.Ignore())
            .ForMember(dest => dest.ContractType, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Amount, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Account, opt => 
                opt.Ignore())
            .ForMember(dest => dest.CustomerBankingRelationship, opt => 
                opt.Ignore())
            .ForMember(dest => dest.CustomerBankingRelationshipId, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Transaction, opt => 
                opt.Ignore())
            .ForMember(dest => dest.Id, opt => 
                opt.Ignore())
            .ReverseMap();
    }
}