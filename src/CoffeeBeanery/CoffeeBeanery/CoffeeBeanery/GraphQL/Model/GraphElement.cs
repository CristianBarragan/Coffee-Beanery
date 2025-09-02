namespace CoffeeBeanery.GraphQL.Model;

public struct GraphElement
{
    public int EntityId { get; set; }
    public GraphElementType GraphElementType { get; set; }

    public string TableName { get; set; }

    public string FieldName { get; set; }

    public string FieldValue { get; set; }

    public string Field { get; set; }
}

public enum GraphElementType
{
    Edge,
    Node
}