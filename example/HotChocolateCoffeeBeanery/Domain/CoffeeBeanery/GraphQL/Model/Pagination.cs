namespace CoffeeBeanery.GraphQL.Model;

public class Pagination : Process
{
    public string? After { get; set; }

    public string? Before { get; set; }

    public int? First { get; set; }

    public int? Last { get; set; }

    public int? StartCursor { get; set; }

    public int? EndCursor { get; set; }

    public int PageSize { get; set; }

    public TotalRecordCount TotalRecordCount { get; set; }

    public TotalPageRecords TotalPageRecords { get; set; }
}

public class TotalRecordCount : Process
{
    public int RecordCount { get; set; }
}

public class TotalPageRecords : Process
{
    public int PageRecords { get; set; }
}