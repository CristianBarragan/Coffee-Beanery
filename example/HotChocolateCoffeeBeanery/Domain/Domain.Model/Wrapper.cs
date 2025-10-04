
namespace Domain.Model;

public class Wrapper
{
    public string CacheKey { get; set; }
    
    public List<Customer>? Customer { get; set; }
}