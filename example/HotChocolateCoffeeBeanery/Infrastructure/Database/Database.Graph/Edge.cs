namespace Database.Graph;

public class Edge
{
    public Schema Schema { get; set; }
    
    public string Name { get; set; }
}

public enum Schema
{
    BankingGraph
}