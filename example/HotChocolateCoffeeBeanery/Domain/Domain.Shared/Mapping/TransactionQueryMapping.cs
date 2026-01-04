using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class TransactionQueryMapping
{
    public static (CustomerCustomerEdge existingCustomerCustomerEdge, Product existingProduct)  MapFromCustomer(
        object mappedObject, IMapper mapper, CustomerCustomerEdge? existingCustomerCustomerEdge, Product? existingProduct)
    {
        if (mappedObject is DatabaseEntity.Transaction)
        {
            var transactionEntity = mappedObject as DatabaseEntity.Transaction;
            
            if (existingCustomerCustomerEdge.InnerCustomer?.CustomerKey != null)
            {
                if (existingProduct?.CustomerKey != null)
                {
                    var productIndex = existingCustomerCustomerEdge.InnerCustomer.Product.FindIndex(a => a.CustomerKey == existingProduct?.CustomerKey);
                    
                    var existingContract = existingProduct.Contract;

                    if (existingContract?.ContractKey != null)
                    {
                        existingContract.Transaction ??= new List<Transaction>();
                        var existingTransaction = existingContract?.Transaction.FirstOrDefault(t =>
                            t.ContractKey != null &&
                            t.ContractKey == transactionEntity?.ContractKey);
                        
                        mapper.Map(transactionEntity, existingProduct);

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
                        
                        existingProduct.Contract ??= new Contract();
                        existingProduct.Contract = contract;
                        mapper.Map(transactionEntity, existingProduct);
                        
                        existingCustomerCustomerEdge.InnerCustomer.Product[productIndex] = existingProduct;
                    }

                    var existingAccount = existingProduct.Account;

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
                        mapper.Map(transactionEntity, existingProduct);
                    }
                    else
                    {
                        var account = new Account();
                        account.AccountKey = transactionEntity?.AccountKey;
                        account.Transaction = mapper.Map<Transaction>(transactionEntity);

                        existingProduct.Account ??= new Account();
                        existingProduct.Account = account;
                        mapper.Map(transactionEntity, existingProduct);
                        
                        existingCustomerCustomerEdge.InnerCustomer.Product[productIndex] = existingProduct;
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
                
                    existingProduct.Account = new Account();
                    existingProduct.Account = account;
                    existingProduct.Contract = new Contract();
                    existingProduct.Contract = contract;
                    mapper.Map(transactionEntity, existingProduct);

                    existingCustomerCustomerEdge.InnerCustomer.Product ??= [];
                    mapper.Map(transactionEntity, existingProduct);
                    
                    existingCustomerCustomerEdge.InnerCustomer.Product.Add(existingProduct);
                }
            }
            else
            {
                existingCustomerCustomerEdge.InnerCustomer = new Customer();
                existingCustomerCustomerEdge.InnerCustomer.Product = [];
                
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
                
                existingProduct.Account = new Account();
                existingProduct.Account = account;
                existingProduct.Contract = new Contract();
                existingProduct.Contract = contract;
                existingProduct.CustomerKey = existingCustomerCustomerEdge.InnerCustomer?.CustomerKey;
                mapper.Map(transactionEntity, existingProduct);
                
                existingCustomerCustomerEdge.InnerCustomer?.Product.Add(existingProduct);
            }
        }
        return (default, existingProduct);
    }
}