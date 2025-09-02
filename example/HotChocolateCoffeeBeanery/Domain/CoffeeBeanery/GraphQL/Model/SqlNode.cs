namespace CoffeeBeanery.GraphQL.Model;

public class SqlNode
{
    public SqlNodeType SqlNodeType { get; set; } = SqlNodeType.Node;

    public List<string> InsertColumns { get; set; } = new List<string>();

    public List<string> SelectColumns { get; set; } = new List<string>();

    public List<string> UpdateColumns { get; set; } = new List<string>();

    public Dictionary<string, List<string>> Values { get; set; } =
        new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    public NodeTree NodeTree { get; set; }

    public int Id { get; set; }
}

public enum SqlNodeType
{
    Edge,
    Node,
    Mutation
}