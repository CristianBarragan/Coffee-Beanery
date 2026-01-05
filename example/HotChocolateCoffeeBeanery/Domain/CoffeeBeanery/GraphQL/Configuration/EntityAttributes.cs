namespace CoffeeBeanery.GraphQL.Configuration;

public class UpsertKeyAttribute() : Attribute
{
    public UpsertKeyAttribute(string entity, string schema) : this()
    {
        Entity = entity;
        Schema = schema;
    }
    
    public string Entity { get; set; }
    
    public string Schema { get; set; }
}

public class JoinKeyAttribute() : Attribute
{
    public JoinKeyAttribute(string entity, string column) : this()
    {
        Entity = entity;
        Column = column;
    }
    
    public string Entity { get; set; }
    
    public string Column { get; set; }
}

public class JoinOneKeyAttribute() : Attribute
{
    public JoinOneKeyAttribute(string entity, string column) : this()
    {
        Entity = entity;
        Column = column;
    }
    
    public string Entity { get; set; }
    
    public string Column { get; set; }
}

public class LinkKeyAttribute() : Attribute
{
    public LinkKeyAttribute(string entity, string column) : this()
    {
        Entity = entity;
        Column = column;
    }
    
    public string Entity { get; set; }
    
    public string Column { get; set; }
}

public class LinkBusinessKeyAttribute() : Attribute
{
    public LinkBusinessKeyAttribute(string entity, string column) : this()
    {
        Entity = entity;
        Column = column;
    }
    
    public string Entity { get; set; }
    
    public string Column { get; set; }
}

public class GraphKeyAttribute() : Attribute
{
    public GraphKeyAttribute(string entity) : this()
    {
        Entity = entity;
    }
    
    public string Entity { get; set; }
}