using CoffeeBeanery.GraphQL.Extension;
using CoffeeBeanery.GraphQL.Model;

namespace CoffeeBeanery.GraphQL.Helper;

public static class SqlHelper
{
    /// <summary>
    /// Method for adding pagination into the query SQL statement 
    /// </summary>
    /// <param name="rootTree"></param>
    /// <param name="sqlQuery"></param>
    /// <param name="sqlOrderStatement"></param>
    /// <param name="pagination"></param>
    /// <param name="hasTotalCount"></param>
    public static string HandleQueryClause(NodeTree rootTree, string sqlQuery, string sqlOrderStatement,
        Pagination pagination, bool hasTotalCount = false)
    {
        var from = 1;
        var to = pagination!.PageSize;
        var sqlWhereStatement = string.Empty;

        if (!string.IsNullOrEmpty(pagination!.After) && pagination.First > 0 &&
            hasTotalCount)
        {
            from = int.Parse(pagination.After) + 1;
            to = from + pagination.First!.Value;
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN {from} AND {to}"
                : $" AND \"RowNumber\" BETWEEN {from} AND {to}";
        }
        else if (!string.IsNullOrEmpty(pagination?.Before) && pagination.Last > 0 &&
                 hasTotalCount)
        {
            to = int.Parse(pagination.Before) - 1;
            from = to - pagination.Last!.Value;
            to = to >= 1 ? to : 1;
            from = from >= 1 ? from : 1;
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN {from} AND {to}"
                : $" AND \"RowNumber\" BETWEEN {from} AND {to}";
        }
        else if (pagination!.First > 0 && pagination!.Last > 0 && hasTotalCount)
        {
            to = pagination!.First!.Value;
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN {pagination!.First} AND {pagination!.Last}"
                : $" AND \"RowNumber\" BETWEEN {pagination!.First} AND {pagination!.Last}";
        }
        else if (pagination!.First > 0 && hasTotalCount)
        {
            to = pagination!.First!.Value;
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN {pagination!.First} AND \"RowNumber\""
                : $" AND \"RowNumber\" BETWEEN {pagination!.First} AND \"RowNumber\"";
        }
        else if (pagination!.Last > 0 && hasTotalCount)
        {
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN \"RowNumber\" - {pagination!.Last} AND \"RowNumber\""
                : $" AND \"RowNumber\" BETWEEN \"RowNumber\" - {pagination!.Last} AND \"RowNumber\"";
        }
        else
        {
            to = 0;
            from = 0;
        }

        var hasPagination = pagination.First > 0 || pagination.Last > 0 ||
                            (pagination.First > 0 &&
                             !string.IsNullOrEmpty(pagination.After)) ||
                            (pagination.Last > 0 &&
                             !string.IsNullOrEmpty(pagination.Before));
        var sql = $"WITH {rootTree.Schema}s AS (SELECT * FROM (SELECT * FROM (" + sqlQuery + $") {rootTree.Name} ) ";
        var totalCount = hasPagination && hasTotalCount
            ? $" DENSE_RANK() OVER({sqlOrderStatement}) AS \"RowNumber\","
            : "";
        sqlQuery = ($" {sql} a ) " +
                    $"SELECT * FROM ( SELECT (SELECT COUNT(DISTINCT \"{"Id".ToSnakeCase(rootTree.Id)}\") FROM {rootTree.Schema}s) \"RecordCount\", " +
                    $"{totalCount} * FROM {rootTree.Schema}s) a {sqlWhereStatement.Replace('~', 'a')}");
        return sqlQuery;
    }

    /// <summary>
    /// Generate upserts 
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="sqlUpsertStatementNodes"></param>
    /// <param name="entityNames"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <returns></returns>
    public static string GenerateUpsertStatements(Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode> sqlUpsertStatementNodes, NodeTree currentTree,
        List<string> entityNames, Dictionary<string, string> sqlWhereStatement, List<string> entitiesProcessed)
    {
        var sqlUpsert = string.Empty;

        if (entitiesProcessed.Contains(currentTree.Name))
        {
            return string.Empty;
        }

        entitiesProcessed.Add(currentTree.Name);

        var processingTree = currentTree;
        var whereParentValue = sqlWhereStatement.GetValueOrDefault(processingTree.ParentName);
        var whereParentClause = string.Empty;
        if (!string.IsNullOrEmpty(whereParentValue))
        {
            whereParentClause = $" WHERE {whereParentValue.Replace("~", processingTree.ParentName)}";
        }

        var whereCurrentValue = sqlWhereStatement.GetValueOrDefault(processingTree.Name);
        var whereCurrentClause = string.Empty;

        if (!string.IsNullOrEmpty(whereCurrentClause) && string.IsNullOrEmpty(whereParentClause))
        {
            whereCurrentClause = $" WHERE {whereCurrentValue.Replace("~", processingTree.Name)}";
        }

        if (!string.IsNullOrEmpty(whereCurrentClause) && !string.IsNullOrEmpty(whereParentClause))
        {
            whereCurrentClause += $" {whereParentClause} {whereCurrentValue.Replace("~", processingTree.Name)}";
        }

        var upsertingEntity = sqlUpsertStatementNodes.FirstOrDefault(s =>
            s.Key.Split('~')[0].Matches(processingTree.Name) || !s.Value.JoinKeys
                .Any(a => a.To.Split('~')[0].Matches(processingTree.ParentName)));
            
        if (upsertingEntity.Value != null && upsertingEntity.Value.LinkKeys.Count > 0)
        {
            sqlUpsert += GenerateUpsert(processingTree, trees, sqlUpsertStatementNodes, whereCurrentClause, entityNames);    
        }
        
        foreach (var childTree in trees.Where(t => 
                     entityNames.Contains(t.Key.Split('~')[0])))
        {
            sqlUpsert += GenerateUpsertStatements(trees,
                sqlUpsertStatementNodes, childTree.Value,
                entityNames, sqlWhereStatement, entitiesProcessed);
        }
        return sqlUpsert;
    }

    /// <summary>
    /// Generate the main upsert without "Join columns [Ids]"
    /// </summary>
    /// <param name="currentTree"></param>
    /// <param name="trees"></param>
    /// <param name="sqlUpsertStatementNodes"></param>
    /// <param name="whereClause"></param>
    /// <returns></returns>
    public static string GenerateUpsert(NodeTree currentTree, Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode> sqlUpsertStatementNodes,
        string whereClause, List<string> entityNames)
    {
        var sqlUpsertAux = string.Empty;

        var currentColumns = sqlUpsertStatementNodes
            .Where(k => k.Key.Split('~')[0].Matches(currentTree.Name) && 
                        ! entityNames.Contains(k.Value.RelationshipKey.Split('~')[1]) &&
                        ! k.Value.LinkBusinessKeys.Any(b => b.From.Matches(k.Key)) &&
                        ! k.Value.LinkKeys.Any(b => b.From.Matches(k.Key)) &&
                        ! k.Value.LinkKeys.Any(b => trees.Keys.Any(a => a.Matches(k.Key.Split('~')[1])))).ToList();
        
        if (currentColumns.Count == 0 ||
            !currentColumns.LastOrDefault().Value.UpsertKeys
                .Any(u => currentColumns.Any(c => c.Key.Matches(u))))
        {
            return sqlUpsertAux;
        }

        sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                        $" {string.Join(",", currentColumns.Select(s => $"\"{s.Value.RelationshipKey.Split('~')[1]}\"").ToList())}) VALUES ({
                            string.Join(",", currentColumns.Select(s => $"'{s.Value.Value}'").ToList())}) " +
                        $" ON CONFLICT" +
                        $" ({string.Join(",", currentColumns.LastOrDefault().Value.UpsertKeys
                            .Where(u => currentColumns.Any(c => c.Key.Matches(u)))
                            .Select(s => $"\"{s.Split('~')[1]}\"").ToList())}) ";

        var exclude = new List<string>();
        exclude.AddRange(
            currentColumns.Where(c => c.Value.UpsertKeys
                    .Any(u => !u.Matches(c.Value.RelationshipKey.Split('~')[1])))
                .Select(e => $"\"{e.Value.RelationshipKey.Split('~')[1]}\" = EXCLUDED.\"{e.Value.RelationshipKey.Split('~')[1]}\"")
        );

        if (exclude.Count > 0)
        {
            sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)} {whereClause};";
        }
        else
        {
            sqlUpsertAux += $" DO NOTHING {whereClause};";
        }

        return sqlUpsertAux;
    }

    /// <summary>
    /// Generate the upsert for "Join columns [Ids]"
    /// </summary>
    /// <param name="currentTree"></param>
    /// <param name="entityNames"></param>
    /// <param name="trees"></param>
    /// <param name="sqlUpsertStatementNodes"></param>
    /// <param name="whereClause"></param>
    /// <returns></returns>
    public static string GenerateSelectUpsert(NodeTree currentTree, List<string> entityNames,
        Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode> sqlUpsertStatementNodes,
        Dictionary<string, string> sqlWhereStatement, List<string> entitiesProcessed, string rootEntityName,
        List<string> generatedQuery)
    {
        if (entitiesProcessed.Contains(currentTree.Name))
        {
            return string.Empty;
        }
        
        entitiesProcessed.Add(currentTree.Name);
        
        var sqlUpsertAux = string.Empty;
        var hasUpsert = true;
        
        var currentColumns = sqlUpsertStatementNodes
            .Where(k => k.Key.Split('~')[0].Matches(currentTree.Name) &&
                        !entityNames.Contains(k.Key.Split('~')[1])).ToList();

        if (currentColumns.Count == 0)
        {
            return sqlUpsertAux;
        }
        
        var upsertKeys = currentColumns.LastOrDefault().Value
            .UpsertKeys;
        
        if (!currentColumns.All(k => 
                upsertKeys.Contains(k.Key)))
        {
            return sqlUpsertAux;
        }

        foreach (var joinKey in currentColumns.Last().Value.JoinKeys)
        {
            if (!generatedQuery.Contains(joinKey.From.Split('~')[1]) &&
                !joinKey.From.Split('~')[1].Matches($"{currentTree.Name}Id"))
            {
                sqlUpsertAux += GenerateCommand(new List<KeyValuePair<string, SqlNode>>()
                {
                    new KeyValuePair<string, SqlNode>(joinKey.From, currentColumns.Last().Value)
                }, trees, currentTree, sqlWhereStatement);
                
                generatedQuery.Add(joinKey.From.Split('~')[1]);
            }
        }
        
        foreach (var joinOneKey in currentColumns.Last().Value.JoinOneKeys)
        {
            if (!generatedQuery.Contains(joinOneKey.From.Split('~')[1]) &&
                !joinOneKey.From.Split('~')[1].Matches($"{currentTree.Name}Id"))
            {
                sqlUpsertAux += GenerateCommand(new List<KeyValuePair<string, SqlNode>>()
                {
                    new KeyValuePair<string, SqlNode>(joinOneKey.From, currentColumns.Last().Value)
                }, trees, currentTree, sqlWhereStatement);
                
                generatedQuery.Add(joinOneKey.From.Split('~')[1]);
            }
        }
        
        var parentTree = trees[currentTree.ParentName];
        
        if (!parentTree.Name.Matches(rootEntityName))
        {
            sqlUpsertAux += GenerateSelectUpsert(parentTree, entityNames,
                trees, sqlUpsertStatementNodes, sqlWhereStatement, entitiesProcessed, rootEntityName, generatedQuery);
        }

        return sqlUpsertAux;
    }

    private static string GenerateCommand(List<KeyValuePair<string, SqlNode>> currentColumns, Dictionary<string, NodeTree> trees,
        NodeTree currentTree, Dictionary<string, string> sqlWhereStatement)
    {
        currentColumns = currentColumns.DistinctBy(a => a.Key).ToList();
        var parentTree = trees[currentTree.ParentName];

        var insertJoin = $"\"{parentTree.Name}Id\"";
        var selectJoin = $"{parentTree.Name}.\"Id\" AS" +
                        $" \"{parentTree.Name}Id\"";
        var excludeJoin = $"\"{parentTree.Name}Id\" = EXCLUDED.\"{parentTree.Name}Id\"";
        var where = sqlWhereStatement.GetValueOrDefault(parentTree.Name);

        if (string.IsNullOrEmpty(where))
        {
            return string.Empty;
        }
        
        var sqlUpsertAux = $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                            insertJoin +
                            $" ) ( SELECT {selectJoin}" + $" FROM \"{parentTree.Schema}\".\"{parentTree.Name}\" {parentTree.Name} {
                                (string.IsNullOrEmpty(where) ? "" : $" WHERE {where.Replace("~",parentTree.Name)}")}";

        var exclude = new List<string>();
        exclude.Add(excludeJoin);

        sqlUpsertAux += $" ) ON CONFLICT" +
                        $" ({string.Join(",", currentColumns.Last().Value.UpsertKeys.Select(s => $"\"{s.Split('~')[1]}\""))}) ";

        if (exclude.Count > 0)
        {
            sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)}";
        }
        else
        {
            sqlUpsertAux += $" DO NOTHING";
        }
        
        return sqlUpsertAux;
    }
}