using CoffeeBeanery.GraphQL.Model;

namespace CoffeeBeanery.Service;

public class ProcessQueryParameters
{
    public SqlStructure SqlStructure { get; set; }

    public string Sql { get; set; }

    public List<string> SplitOnDapper { get; set; }

    public int StartCursor { get; set; }

    public int EndCursor { get; set; }

    public bool HasTotalCount { get; set; }

    public bool HasPagination { get; set; }
}