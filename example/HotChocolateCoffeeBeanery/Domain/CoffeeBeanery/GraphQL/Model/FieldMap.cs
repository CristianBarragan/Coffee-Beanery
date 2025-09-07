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

    public string Model { get; set; }

    public bool IsUpsertKey { get; set; }

    public bool IsJoinKey { get; set; }

    public bool IsEnum { get; set; }

    public Dictionary<string, string> DestinationEnumerationValues { get; set; } =  new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
}