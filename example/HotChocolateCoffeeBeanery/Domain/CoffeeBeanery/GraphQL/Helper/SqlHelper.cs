using System.Text;
using CoffeeBeanery.GraphQL.Extension;
using CoffeeBeanery.GraphQL.Model;
using MoreLinq;

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
        Dictionary<string, SqlNode> sqlNodes, string rootEntityName, string wrapperEntityName,
        List<string> generatedQuery, Dictionary<string, SqlNode> sqlUpsertStatementNodes, NodeTree currentTree,
        List<string> entityNames, Dictionary<string, string> sqlWhereStatement, List<string> entitiesProcessed,
        StringBuilder sqlUpsertBuilder, StringBuilder sqlSelectUpsertBuilder)
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
        
        var sql = string.Empty;
        
        if (upsertingEntity.Value != null)
        {
            sql = GenerateUpsert(processingTree, trees, sqlUpsertStatementNodes, whereCurrentClause, entityNames);
            
            if (!string.IsNullOrEmpty(sql))
            {
                generatedQuery.Add(sql); 
                sqlUpsertBuilder.Append(generatedQuery.Last());
                sqlUpsertBuilder.Insert(0, " ; " + sql);
                sqlSelectUpsertBuilder.Insert(0, " ; " + GenerateSelectUpsert(processingTree, sqlNodes, entityNames,
                    trees, sqlUpsertStatementNodes, sqlWhereStatement, new List<string>(), rootEntityName, generatedQuery, 
                    wrapperEntityName));
            }
        }
        
        foreach (var childTree in trees.Where(t => 
                     entityNames.Contains(t.Key.Split('~')[0])))
        {
            GenerateUpsertStatements(trees, sqlNodes, rootEntityName, wrapperEntityName, generatedQuery,
                sqlUpsertStatementNodes, childTree.Value,
                entityNames, sqlWhereStatement, entitiesProcessed,
                sqlUpsertBuilder, sqlSelectUpsertBuilder);
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
                .Any(u => currentColumns.Any(c => u.Matches(c.Key))))
        {
            return sqlUpsertAux;
        }

        sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                        $" {string.Join(",", currentColumns.Select(s => $"\"{s.Value.RelationshipKey.Split('~')[1]}\"").ToList())}) VALUES ({
                            string.Join(",", currentColumns.Select(s => $"'{s.Value.Value}'").ToList())}) " +
                        $" ON CONFLICT" +
                        $" ({string.Join(",", currentColumns.LastOrDefault().Value.UpsertKeys
                            .Where(u => currentColumns.Any(c => u.Matches(c.Key)))
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
    public static string GenerateSelectUpsert(NodeTree currentTree, Dictionary<string, SqlNode> sqlNodes, 
        List<string> entityNames,
        Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode> sqlUpsertStatementNodes,
        Dictionary<string, string> sqlWhereStatement, List<string> entitiesProcessed, string rootEntityName,
        List<string> generatedQuery, string wrapperEntityName)
    {
        if (entitiesProcessed.Contains(currentTree.Name))
        {
            return string.Empty;
        }
        
        entitiesProcessed.Add(currentTree.Name);
        
        var sqlUpsertAux = string.Empty;
        var hasUpsert = true;
        
        var currentColumns = sqlUpsertStatementNodes
            .Where(k => currentTree.Mapping.Any(f => f.FieldDestinationName.Matches(k.Key.Split('~')[1]) &&
                                       !entityNames.Contains(k.Key.Split('~')[1]))).ToList();

        if (currentColumns.Count == 0)
        {
            return sqlUpsertAux;
        }
        
        var columnsQuery = currentColumns.Where(c => c.Value.UpsertKeys.Any(k => 
                k.Matches(c.Value.RelationshipKey))).ToList();

        var columnValue = columnsQuery.FirstOrDefault(a => a.Key.Split('~')[0]
                .Matches(currentTree.Name)).Value;

        if (columnValue == null)
        {
            return sqlUpsertAux;
        }

        foreach (var joinKey in columnValue.JoinKeys)
        {
            if (!joinKey.To.Split('~')[0].Matches(currentTree.Name))
            {
                var columns = columnsQuery.ToList();
                columns.Add(new KeyValuePair<string, SqlNode>(joinKey.To, currentColumns.Last().Value));
                
                var parentColumns = sqlUpsertStatementNodes
                    .Where(k => trees[joinKey.To.Split('~')[0]].Mapping.Any(f => f
                                                                 .FieldDestinationName.Matches(k.Key.Split('~')[1]) &&
                                                             !entityNames.Any(e => e.Matches(k.Key.Split('~')[1])))).ToList();
                
                sqlUpsertAux += GenerateCommand(columns, trees, currentTree, sqlWhereStatement, parentColumns, entityNames, joinKey.To.Split('~')[0]);
            }
        }
        
        foreach (var joinOneKey in columnValue.JoinOneKeys)
        {
            if (!joinOneKey.From.Split('~')[0].Matches(currentTree.Name))
            {
                var columns = columnsQuery.ToList();
                columns.Add(new KeyValuePair<string, SqlNode>(joinOneKey.From, currentColumns.Last().Value));
                
                var parentColumns = sqlUpsertStatementNodes
                    .Where(k => trees[joinOneKey.From.Split('~')[0]]
                        .Mapping.Any(f => f
                        .FieldDestinationName.Matches(k.Key.Split('~')[1]) &&
                            !entityNames.Any(e => e.Matches(k.Key.Split('~')[1])))).ToList();
                
                sqlUpsertAux += GenerateCommand(columns, trees, currentTree, sqlWhereStatement, parentColumns, entityNames, joinOneKey.From.Split('~')[0]);
            }
        }

        return sqlUpsertAux;
    }

    private static string GenerateCommand(List<KeyValuePair<string, SqlNode>> currentColumns, Dictionary<string, NodeTree> trees,
        NodeTree currentTree, Dictionary<string, string> sqlWhereStatement, List<KeyValuePair<string, SqlNode>> parentColumns,
        List<string> entityNames, string entity)
    {
        
        currentColumns = currentColumns.DistinctBy(a => a.Key).ToList();

        if (string.IsNullOrEmpty(entity))
        {
            return string.Empty;
        }
        
        var parentTree = trees[entity];

        var insertJoin = $"\"{entity}Id\"";
        var selectJoin = $"{entity}.\"Id\" AS" +
                        $" \"{entity}Id\"";

        var onConflictKey = currentColumns.FirstOrDefault(a => a.Value
            .UpsertKeys.Any(x => x == a.Value.RelationshipKey) && a.Value.RelationshipKey.Split('~')[0].Matches(currentTree.Name));
        
        insertJoin += $", \"{onConflictKey.Value.Column}\"";
        selectJoin += $", '{onConflictKey.Value.Value}' AS \"{onConflictKey.Value.Column}\"";
        
        var excludeJoin = $"\"{entity}Id\" = EXCLUDED.\"{entity}Id\"";

        var where = parentColumns.Where(a => a.Key.Split('~')[0].Matches(entity) && a.Value.UpsertKeys.Any(k => k.Matches(a.Value.RelationshipKey)))
            .Select(s => $"{entity}.\"{s.Value.Column}\" = '{s.Value.Value}'").ToList();
        
        if (
            currentColumns.Count == 0 || 
            !currentColumns.Any(a => a.Value.UpsertKeys
                .Any(u => u.Split('~')[1].Matches(a.Value.Column)) && a.Value.SqlNodeType == SqlNodeType.Mutation && 
                                     !string.IsNullOrEmpty(a.Value.Value)) ||
            where.Count == 0)
        {
            return string.Empty;
        }
        
        var sqlUpsertAux = $" ; INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                            insertJoin +
                            $" ) ( SELECT {selectJoin}" + $" FROM \"{parentTree.Schema}\".\"{entity}\" {entity} WHERE {
                               string.Join(" AND ",  where)}";

        var exclude = new List<string>();
        exclude.Add(excludeJoin);

        sqlUpsertAux += $" ) ON CONFLICT" +
                        $" (\"{onConflictKey.Value.Column}\") ";

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