using AutoMapper;
using Domain.Model;
using DatabaseEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public static class TransactionQueryMapping
{
    public static void MapFromCustomer(List<Customer> models, object mappedObject, IMapper mapper)
    {
        if (mappedObject is DatabaseEntity.Transaction)
        {
            var transactionEntity = mappedObject as DatabaseEntity.Transaction;
            
            var index = models.Where(c => c.Product != null).ToList().FindIndex(c =>
                c.Product.Any(cbr => cbr.AccountKey == transactionEntity.AccountKey));
            
            if (index >= 0)
            {
                models[index].Product = models[index].Product ?? [];
                var indexProduct = models[index].Product
                    .FindIndex(x => x.Account.Any(a => a.AccountKey == transactionEntity.AccountKey));
            
                if (indexProduct >= 0)
                {
                    var product = models[index].Product[indexProduct];
                    var accountIndex = product.Account.FindIndex(a => a.AccountKey == transactionEntity.AccountKey);

                    if (accountIndex >= 0)
                    {
                        var transactionIndex = models[index].Product[indexProduct].Account[accountIndex].Transaction.FindIndex(t => t.TransactionKey == transactionEntity.TransactionKey);

                        if (transactionIndex >= 0)
                        {
                            var transaction = models[index].Product[indexProduct].Account[accountIndex].Transaction[transactionIndex];
                            transaction = mapper.Map(transactionEntity, transaction);
                            models[index].Product[indexProduct].Account[accountIndex].Transaction[transactionIndex] =  transaction;
                        }
                        else
                        {
                            var transaction = new Transaction();
                            transaction = mapper.Map(transactionEntity, transaction);
                            models[index].Product[indexProduct].Account[accountIndex].Transaction.Add(transaction);
                        }
                    }
                    else
                    {
                        var account = new Account();
                        account = mapper.Map(transactionEntity, account);
                        var transaction = new Transaction();
                        transaction = mapper.Map(transactionEntity, transaction);
                        account.Transaction.Add(transaction);
                        models[index].Product[indexProduct].Account.Add(account);
                    }
                }
                else
                {
                    var product = new Product();
                    var account = new Account();
                    account = mapper.Map(transactionEntity, account);
                    var transaction = new Transaction();
                    transaction = mapper.Map(transactionEntity, transaction);
                    account.Transaction = account.Transaction ?? [];
                    account.Transaction.Add(transaction);
                    product.Account = product.Account ?? [];
                    product.Account.Add(account);
                    models[index].Product = models[index].Product ?? [];
                    models[index].Product.Add(product);
                }
            }
            else
            {
                var product = new Product();
                var account = new Account();
                var transaction = new Transaction();
                transaction = mapper.Map(transactionEntity, transaction);
                account = mapper.Map(transaction, account);
                account.Transaction = account.Transaction ?? [];
                account.Transaction.Add(transaction);
                product = mapper.Map(account, product);
                product.Account = product.Account ?? [];
                product.Account.Add(account);
                
                var customer = new Customer()
                {
                    Product = new List<Product>()
                    {
                        product
                    }
                };
                models.Add(customer);
            }
        }
    }
}