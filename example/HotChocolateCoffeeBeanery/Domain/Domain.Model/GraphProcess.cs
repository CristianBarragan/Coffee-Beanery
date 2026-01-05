namespace Domain.Model;

public class GraphProcess
{
    public LevelDirection? LevelDirection { get; set; }

    public int? LevelDepth { get; set; }

    public GraphType? GraphType { get; set; }
}

public enum LevelDirection
{
    Outer,
    Inner,
    Full
}

public enum GraphType
{
    None,
    WithInclusiveMathing,
    WithExclusiveMathing,
}