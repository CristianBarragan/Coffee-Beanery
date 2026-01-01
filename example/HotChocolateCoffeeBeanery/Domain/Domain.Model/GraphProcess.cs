namespace Domain.Model;

public class GraphProcess
{
    public Direction Direction { get; set; }
}

public enum Direction
{
    Outer,
    Inner,
    Full
}