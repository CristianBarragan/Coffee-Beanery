namespace CoffeeBeanery.GraphQL.Model;

public interface ITreeMap<T, M> where T : class where M : class
{
    public List<KeyValuePair<string, string>> NodeId { get; set; }

    public List<string> EntityNames { get; set; }

    public List<M> ModelTypes { get; set; }

    public List<T> EntityTypes { get; set; }

    public NodeTree NodeTree { get; set; }

    public Dictionary<string, NodeTree> DictionaryTree { get; set; }
}

public class TreeMap<T, M> : ITreeMap<T, M>
    where T : class where M : class
{
    public List<KeyValuePair<string, string>> NodeId { get; set; }

    public List<string> EntityNames { get; set; }

    public List<M> ModelTypes { get; set; }

    public List<T> EntityTypes { get; set; }

    public NodeTree NodeTree { get; set; }

    public Dictionary<string, NodeTree> DictionaryTree { get; set; } =
        new Dictionary<string, NodeTree>(StringComparer.OrdinalIgnoreCase);
}