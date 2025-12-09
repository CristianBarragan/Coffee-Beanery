using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class TransactionQueryMapping
{
    public static (Customer existingCustomer, Product existingProduct)  MapFromCustomer(
        object mappedObject, IMapper mapper, Customer? existingCustomer, Product? existingProduct)
    {
        if (mappedObject is DatabaseEntity.Transaction)
        {
            var transactionEntity = mappedObject as DatabaseEntity.Transaction;
            
            if (existingCustomer?.CustomerKey != null)
            {
                if (existingProduct?.CustomerKey != null)
                {
                    var productIndex = existingCustomer.Product.FindIndex(a => a.CustomerKey == existingProduct?.CustomerKey);
                    
                    var existingContract = existingProduct.Contract?
                        .FirstOrDefault(c => c.ContractKey == transactionEntity?.ContractKey);

                    if (existingContract?.ContractKey != null)
                    {
                        existingContract.Transaction ??= new List<Transaction>();
                        var existingTransaction = existingContract?.Transaction.FirstOrDefault(t =>
                            t.ContractKey != null &&
                            t.ContractKey == transactionEntity?.ContractKey);

                        if (existingTransaction?.TransactionKey != null)
                        {
                            mapper.Map(transactionEntity, existingTransaction);
                        }
                        else
                        {
                            var transaction = mapper.Map<Transaction>(transactionEntity);
                            existingContract!.Transaction.Add(transaction);
                        }
                    }
                    else
                    {
                        var contract = new Contract();
                        contract.ContractKey = transactionEntity?.ContractKey;
                        contract.Transaction = [];
                        contract.Transaction.Add(mapper.Map<Transaction>(transactionEntity));
                        
                        existingProduct.Contract ??= [];
                        existingProduct.Contract.Add(contract);
                        existingCustomer.Product[productIndex] = existingProduct;
                    }

                    var existingAccount = existingProduct.Account?
                        .FirstOrDefault(c => c.AccountKey == transactionEntity?.AccountKey);

                    if (existingAccount?.AccountKey != null)
                    {
                        if (existingAccount.Transaction?.TransactionKey != null)
                        {
                            mapper.Map(transactionEntity, existingAccount.Transaction);
                        }
                        else
                        {
                            existingAccount.Transaction = mapper.Map<Transaction>(transactionEntity);
                        }
                    }
                    else
                    {
                        var account = new Account();
                        account.AccountKey = transactionEntity?.AccountKey;
                        account.Transaction = mapper.Map<Transaction>(transactionEntity);

                        existingProduct.Account ??= [];
                        existingProduct.Account.Add(account);
                        existingCustomer.Product[productIndex] = existingProduct;
                    }
                }
                else
                {
                    existingProduct = new Product();
                    existingProduct.ContractKey = transactionEntity?.ContractKey;
                    existingProduct.AccountKey = transactionEntity?.AccountKey;
                
                    var account = new Account();
                    account.AccountKey = transactionEntity?.AccountKey;
                    account.Transaction = mapper.Map<Transaction>(transactionEntity);
                
                    var contract = new Contract();
                    contract.ContractKey = transactionEntity?.ContractKey;
                    contract.Transaction = [];
                    contract.Transaction.Add(mapper.Map<Transaction>(transactionEntity));
                
                    existingProduct.Account = [];
                    existingProduct.Account.Add(account);
                    existingProduct.Contract = [];
                    existingProduct.Contract.Add(contract);

                    existingCustomer.Product ??= [];
                    existingCustomer.Product.Add(existingProduct);
                }
            }
            else
            {
                existingCustomer = new Customer();
                existingCustomer.Product = [];
                
                existingProduct = new Product();
                existingProduct.ContractKey = transactionEntity?.ContractKey;
                existingProduct.AccountKey = transactionEntity?.AccountKey;
                
                var account = new Account();
                account.AccountKey = transactionEntity?.AccountKey;
                account.Transaction = mapper.Map<Transaction>(transactionEntity);
                
                var contract = new Contract();
                contract.ContractKey = transactionEntity?.ContractKey;
                contract.Transaction = [];
                contract.Transaction.Add(mapper.Map<Transaction>(transactionEntity));
                
                existingProduct.Account = [];
                existingProduct.Account.Add(account);
                existingProduct.Contract = [];
                existingProduct.Contract.Add(contract);
                existingProduct.CustomerKey = existingCustomer?.CustomerKey;
                existingCustomer?.Product.Add(existingProduct);
            }
        }

        return (existingCustomer, existingProduct);
    }
}