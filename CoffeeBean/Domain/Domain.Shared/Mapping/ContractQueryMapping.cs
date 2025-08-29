using AutoMapper;
using Domain.Model;
using Domain.Util.GraphQL.Extension;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class ContractQueryMapping
{
    public static void MapFromCustomer(List<Customer> models, object mappedObject, IMapper mapper)
    {
        if (mappedObject is DatabaseEntity.Contract)
        {
            var contractModel = mappedObject as DatabaseEntity.Contract;
                
            var index = models.FindIndex(c => c.CustomerBankingRelationship.Any(cbr => cbr.ContractKey
                .ToString().Matches(contractModel.ContractKey.ToString())));
                
            if (index >= 0)
            {
                models[index].CustomerBankingRelationship = models[index].CustomerBankingRelationship ?? [];
                var indexCbs = models[index].CustomerBankingRelationship
                    .FindIndex(x => x.Id == contractModel.CustomerBankingRelationshipId);

                if (indexCbs >= 0)
                {
                    models[index].CustomerBankingRelationship[indexCbs].Contract = models[index].CustomerBankingRelationship[indexCbs].Contract ?? [];
                    var indexCon = models[index].CustomerBankingRelationship[indexCbs].Contract
                        .FindIndex(x => x.CustomerBankingRelationshipId == contractModel.CustomerBankingRelationshipId);

                    if (indexCon >= 0)
                    {
                        models[index].CustomerBankingRelationship[indexCbs].Contract[indexCon] = mapper.Map(
                            contractModel,
                            models[index].CustomerBankingRelationship[indexCbs].Contract[indexCon]);
                    }
                    else
                    {
                        var contract = new Contract();
                        contract = mapper.Map(contractModel, 
                                contract);
                        models[index].CustomerBankingRelationship[indexCbs].Contract.Add(contract);
                    }
                }
                else
                {
                    var contract = new Contract();
                    contract = mapper.Map(contractModel, 
                        contract);
                    
                    var customerBankingRelationship = new CustomerBankingRelationship();
                    customerBankingRelationship.Contract.Add(contract);
                    models[index].CustomerBankingRelationship.Add(customerBankingRelationship);
                }
            }
            else
            {
                var customer = new Customer();
                customer.CustomerBankingRelationship = new List<CustomerBankingRelationship>();
                var customerBankingRelationship = new CustomerBankingRelationship();
                customerBankingRelationship = new  CustomerBankingRelationship();
                var contract = new Contract();
                contract = mapper.Map(contractModel, 
                    contract);
                customerBankingRelationship.Contract = new List<Contract>();
                customerBankingRelationship.Contract.Add(contract);
                customer.CustomerBankingRelationship.Add(customerBankingRelationship);
                models.Add(customer);
            }
        }
    }
    
    public static void MapContract(List<Contract> models, object mappedObject, IMapper mapper)
    {
        Contract model = null!;
        if (mappedObject is DatabaseEntity.Contract)
        {
            var ContractModel = mappedObject as DatabaseEntity.Contract;

            var index = models.FindIndex(x => x.Id == ContractModel.Id);
                
            if (index >= 0)
            {
                if (index >= 0)
                {
                    models[index] = mapper.Map(ContractModel, 
                        models[index]);
                }
                else
                {
                    var Contract = new Contract();
                    Contract = mapper.Map(ContractModel, 
                        Contract);
                    models[index] = Contract;
                }
            }
        }
    }
}