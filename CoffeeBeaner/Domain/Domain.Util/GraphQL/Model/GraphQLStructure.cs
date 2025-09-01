using Newtonsoft.Json;

namespace Domain.Util.GraphQL.Model;

public class GraphQLStructure
{
    public List<string> NodeSelect { get; set; } = new List<string>();

    public List<string> EdgeSelect { get; set; } = new List<string>();

    public List<string> Filter { get; set; } = new List<string>();

    public List<string> FilterConditions { get; set; } = new List<string>();

    public List<string> Sort { get; set; } = new List<string>();

    [JsonIgnore]
    public Pagination? Pagination { get; set; }

    public bool HasTotalCount { get; set; }

    public Dictionary<string, string> Mutations { get; set; } = new Dictionary<string, string>();
}


