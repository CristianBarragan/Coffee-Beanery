namespace CoffeeBeanery.GraphQL.Model;

public interface IGraphQLTreeMapping
{
    public List<Dictionary<string, string>> Mappings { get; set; }
}

public class GraphQLTreeMapping : IGraphQLTreeMapping
{
    public List<Dictionary<string, string>> Mappings { get; set; } = [];
}