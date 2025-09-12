
namespace Domain.Model;

public class ContactPoint
{

    public Guid? ContactPointKey { get; set; }

    public ContactPointType? ContactPointType { get; set; }

    public string? ContactPointValue { get; set; }
    
    public Guid? CustomerKey { get; set; }
}

public enum ContactPointType
{
    Mobile,
    Landline,
    Email
}