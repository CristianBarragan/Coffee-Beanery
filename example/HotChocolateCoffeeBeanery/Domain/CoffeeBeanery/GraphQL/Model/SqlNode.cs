namespace CoffeeBeanery.GraphQL.Model;

public class SqlNode
{
    public SqlNodeType SqlNodeType { get; set; } = SqlNodeType.Node;

    public bool IsModel { get; set; }

    public string Value { get; set; } = string.Empty;
    
    public Dictionary<string, string> FromEnumeration { get; set; } = new  Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
    
    public Dictionary<string, string> ToEnumeration { get; set; } = new  Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

    public List<FieldMap> Mapping { get; set; }

    public bool IsEnumeration { get; set; }

    public bool IsGraph { get; set; }
    
    public string RelationshipKey { get; set; } = String.Empty;

    public string Entity { get; set; } = String.Empty;

    public string Column { get; set; } = String.Empty;
    
    public List<string> UpsertKeys { get; set; } = [];
    
    public List<JoinKey> JoinKeys { get; set; } = [];
    
    public List<JoinOneKey> JoinOneKeys { get; set; } = [];
    
    public List<LinkKey> LinkKeys { get; set; } = [];
    
    public List<LinkBusinessKey> LinkBusinessKeys { get; set; } = [];

    public string Namespace { get; set; }
}

public class JoinKey()
{
    public string From { get; set; }
    
    public string To { get; set; }
}

public class JoinOneKey()
{
    public string From { get; set; }
    
    public string To { get; set; }
}

public class LinkKey()
{
    public string From { get; set; }
    
    public string To { get; set; }
}

public class LinkBusinessKey()
{
    public string From { get; set; }
    
    public string To { get; set; }
}

public enum SqlNodeType
{
    Edge,
    Node,
    Mutation
}