namespace Domain.Util.GraphQL.Model;

public class NodeTree
{
    public  int Id { get; set; }
    
    public  int ParentId { get; set; }

    public  string Schema { get; set; }

    public string Name { get; set; }
    
    public string ParentName { get; set; }
    
    public  string JoinKey { get; set; }
    
    public  List<string> UpsertKeys { get; set; }

    public  List<NodeTree> Children { get; set; }

    public  List<string> ChildrenNames { get; set; }
    
    public  Dictionary<string, string> Mappings { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    
    public  Dictionary<string, Dictionary<string, string>> EnumerationMappings { get; set; }
}