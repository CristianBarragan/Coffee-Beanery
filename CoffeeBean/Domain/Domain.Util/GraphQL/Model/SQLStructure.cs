using Dapper;

namespace Domain.Util.GraphQL.Model;

public class SqlStructure
{
    public Pagination? Pagination { get; set; }

    public bool HasTotalCount { get; set; }
    
    public bool HasPagination { get; set; }

    public string SqlUpsert { get; set; }
    
    public string SqlQuery { get; set; }

    public List<string> SplitOnDapper { get; set; }

    public DynamicParameters Parameters { get; set; }
}


