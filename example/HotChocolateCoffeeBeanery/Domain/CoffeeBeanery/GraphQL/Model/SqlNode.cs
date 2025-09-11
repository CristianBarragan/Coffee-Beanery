namespace CoffeeBeanery.GraphQL.Model;

public class SqlNode
{
    public SqlNodeType SqlNodeType { get; set; } = SqlNodeType.Node;

    public string Value { get; set; } = string.Empty;
    
    public Dictionary<string, string> FromEnumeration { get; set; } = new  Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
    
    public Dictionary<string, string> ToEnumeration { get; set; } = new  Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

    public bool IsEnumeration { get; set; }
    
    public string RelationshipKey { get; set; } = String.Empty;

    public string InsertColumn { get; set; } = String.Empty;

    public string SelectColumn { get; set; } = String.Empty;

    public string ExludedColumn { get; set; } = String.Empty;
    
    public List<string> UpsertKeys { get; set; } = [];

    public Dictionary<string,string> JoinKeys { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    
    public Dictionary<string,string> LinkKeys { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public enum SqlNodeType
{
    Edge,
    Node,
    Mutation
}