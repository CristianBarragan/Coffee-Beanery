﻿using Dapper;

namespace CoffeeBeanery.GraphQL.Model;

public class SqlStructure
{
    public Pagination? Pagination { get; set; }

    public bool HasTotalCount { get; set; } = false;

    public bool HasPagination { get; set; } = false;

    public string SqlUpsert { get; set; }

    public string SqlQuery { get; set; }

    public Dictionary<string, Type> SplitOnDapper { get; set; }

    public DynamicParameters Parameters { get; set; }
}