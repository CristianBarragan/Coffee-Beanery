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
    public JoinKeyAttribute(string entity) : this()
    {
        Entity = entity;
    }
    
    public string Entity { get; set; }
}

public class LinkKeyAttribute() : Attribute
{
    public LinkKeyAttribute(string entity) : this()
    {
        Entity = entity;
    }
    
    public string Entity { get; set; }
}