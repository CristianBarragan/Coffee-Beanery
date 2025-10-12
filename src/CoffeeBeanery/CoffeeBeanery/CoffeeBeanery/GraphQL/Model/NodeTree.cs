namespace CoffeeBeanery.GraphQL.Model;

public class NodeTree
{
    public int Id { get; set; }

    public int ParentId { get; set; }

    public string Schema { get; set; }

    public string Name { get; set; }

    public string ParentName { get; set; }

    public List<NodeTree> Children { get; set; } = [];

    public List<string> ChildrenName { get; set; } = [];

    public List<FieldMap> Mapping { get; set; } = [];
}