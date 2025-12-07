namespace CoffeeBeanery.GraphQL.Model;

public interface IEntityTreeMap<T, M> where T : class where M : class
{
    public List<KeyValuePair<string, int>> NodeId { get; set; }

    public List<string> EntityNames { get; set; }

    public List<string> ModelNames { get; set; }

    public List<M> ModelTypes { get; set; }

    public List<T> EntityTypes { get; set; }

    public NodeTree NodeTree { get; set; }

    public Dictionary<string, NodeTree> DictionaryTree { get; set; }
    
    public Dictionary<string, SqlNode> LinkDictionaryTreeNode { get; set; }
    
    public Dictionary<string, SqlNode> LinkDictionaryTreeEdge { get; set; }
    
    public Dictionary<string, SqlNode> LinkDictionaryTreeMutation { get; set; }
}

public class EntityTreeMap<T, M> : IEntityTreeMap<T, M>
    where T : class where M : class
{
    public List<KeyValuePair<string, int>> NodeId { get; set; }

    public List<string> EntityNames { get; set; }

    public List<string> ModelNames { get; set; }

    public List<M> ModelTypes { get; set; }

    public List<T> EntityTypes { get; set; }

    public NodeTree NodeTree { get; set; }

    public Dictionary<string, NodeTree> DictionaryTree { get; set; } =
        new Dictionary<string, NodeTree>(StringComparer.OrdinalIgnoreCase);
    
    public Dictionary<string, SqlNode> LinkDictionaryTreeNode { get; set; } =
        new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
    
    public Dictionary<string, SqlNode> LinkDictionaryTreeEdge { get; set; } =
        new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
    
    public Dictionary<string, SqlNode> LinkDictionaryTreeMutation { get; set; } =
        new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
}