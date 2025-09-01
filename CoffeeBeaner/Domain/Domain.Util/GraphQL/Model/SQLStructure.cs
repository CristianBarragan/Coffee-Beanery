using Dapper;

namespace Domain.Util.GraphQL.Model;

public class SqlStructure
{
    public Pagination? Pagination { get; set; }

    public bool HasTotalCount { get; set; } = false;
    
    public bool HasPagination { get; set; } = false;

    public string SqlUpsert { get; set; }
    
    public string SqlQuery { get; set; }

    public List<string> SplitOnDapper { get; set; }

    public DynamicParameters Parameters { get; set; }
}


