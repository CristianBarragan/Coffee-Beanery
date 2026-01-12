namespace Database.Graph;

public class CustomerCustomerRelationshipEdge : Edge
{
    CustomerCustomerRelationshipEdge()
    {
        Schema = Schema.BankingGraph;
        Name = nameof(CustomerCustomerRelationshipEdge);
    }

    public int OuterCustomerId { get; set; }
    
    public int InnerCustomerId { get; set; }
    
    public int CustomerCustomerRelationshipId { get; set; }
}