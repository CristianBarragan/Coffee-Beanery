namespace CoffeeBeanery.GraphQL.Model;

public class FieldMap
{
    public string FieldSourceName { get; set; }
    
    public string SourceModel { get; set; }
    
    public Type FieldSourceType { get; set; }
    
    public string FieldDestinationName { get; set; }
    
    public string FieldDestinationSchema { get; set; }
    
    public string DestinationEntity { get; set; }
    
    public Type FieldDestinationType { get; set; }

    public string FieldDestinationPropertyType { get; set; }
}