
namespace CoffeeBeanery.GraphQL.Model;

public class SqlQueryStructure
{
    public int Id { get; set; }
    
    public SqlNodeType SqlNodeType { get; set; } = SqlNodeType.Node;
    
    public string Query { get; set; }

    public SqlNode SqlNode { get; set; }

    public List<string> Columns { get; set; } = new List<string>();

    public List<string> ParentColumns { get; set; } = new List<string>();

    public List<string> SelectColumns { get; set; } = new List<string>();

    public Dictionary<string, string> ChildrenJoinColumns { get; set; } = new Dictionary<string, string>();

    public string WhereClause { get; set; } = string.Empty;
}