using AutoMapper;
using AutoMapper.EquivalencyExpression;
using Database.Entity;
using DomainModel = Domain.Model;

namespace Domain.Shared.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Customer, DomainModel.Customer>()
            .EqualityComparison((src, dest) => src.CustomerKey == dest.CustomerKey)
            .ForMember(pts => pts.ContactPoint, opt => opt.Ignore())
            .ForMember(pts => pts.CustomerBankingRelationship, opt => opt.Ignore())
            .ForMember(pts => pts.CustomerType, opt => opt.MapFrom(ps => ps.CustomerType))
            .ForMember(pts => pts.FirstNaming, opt => opt.MapFrom(ps => ps.FirstName))
            .ForMember(pts => pts.LastNaming, opt => opt.MapFrom(ps => ps.LastName))
            .ForMember(pts => pts.FullNaming, opt => opt.MapFrom(ps => ps.FullName))
            .ReverseMap();
        
        CreateMap<CustomerBankingRelationship, DomainModel.CustomerBankingRelationship>()
            .EqualityComparison((src, dest) => src.CustomerBankingRelationshipKey == dest.CustomerBankingRelationshipKey)
            .ForMember(dest => dest.Schema, opt => opt.Ignore())
            .ForMember(dest => dest.CustomerKey, opt => 
                opt.MapFrom(src => src.CustomerKey))
            .ForMember(dest => dest.ContractKey, opt => 
                opt.MapFrom(src => src.ContractKey))
            .ForMember(dest => dest.Id, opt => 
                opt.MapFrom(src => src.Id))
            .ForMember(pts => pts.Contract, opt => opt.Ignore())
            .ReverseMap();
        
        CreateMap<ContactPoint, DomainModel.ContactPoint>()
            .EqualityComparison((src, dest) => src.ContactPointKey == dest.ContactPointKey)
            .ForMember(dest => dest.Schema, opt => opt.Ignore())
            .ForMember(dest => dest.ContactPointKey, opt =>
                opt.MapFrom(a => a.ContactPointKey))
            .ForMember(dest => dest.ContactPointType, opt =>
                opt.MapFrom(a => a.ContactPointType))
            .ForMember(dest => dest.ContactPointValue, opt =>
                opt.MapFrom(a => a.ContactPointValue))
            .ForMember(dest => dest.CustomerKey, opt =>
                opt.MapFrom(a => a.CustomerKey))
            .ForMember(dest => dest.CustomerId, opt =>
                opt.MapFrom(a => a.CustomerId))
            .ReverseMap();
        
        CreateMap<Contract, DomainModel.Contract>()
            .EqualityComparison((src, dest) => src.ContractKey == dest.ContractKey)
            .ForMember(dest => dest.Schema, opt => opt.Ignore())
            .ForMember(dest => dest.ContractKey, opt =>
                opt.MapFrom(a => a.ContractKey))
            .ForMember(dest => dest.ContractType, opt =>
                opt.MapFrom(a => a.ContractType))
            .ForMember(dest => dest.Amount, opt =>
                opt.MapFrom(a => a.Amount))
            .ForMember(dest => dest.CustomerBankingRelationshipId, opt =>
                opt.MapFrom(a => a.CustomerBankingRelationshipId))
            .ForMember(dest => dest.CustomerBankingRelationshipKey, opt =>
                opt.MapFrom(a => a.CustomerBankingRelationshipKey))
            .ReverseMap();
    }
}
