namespace CoffeeBeanery.GraphQL.Model;

public interface IModelTreeMap<T, M> where T : class where M : class
{
    public List<KeyValuePair<string, int>> NodeId { get; set; }

    public List<string> EntityNames { get; set; }

    public List<string> ModelNames { get; set; }

    public List<M> ModelTypes { get; set; }

    public List<T> EntityTypes { get; set; }

    public NodeTree NodeTree { get; set; }

    public Dictionary<string, NodeTree> DictionaryTree { get; set; }

    public Dictionary<string, SqlNode> LinkEntityDictionaryTree { get; set; }
}

public class ModelTreeMap<T, M> : IModelTreeMap<T, M>
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
    
    public Dictionary<string, SqlNode> LinkEntityDictionaryTree { get; set; } =
        new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
}