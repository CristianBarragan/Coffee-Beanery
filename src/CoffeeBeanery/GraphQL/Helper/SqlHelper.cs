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
        Dictionary<string, SqlNode> sqlUpsertStatementNodes,
        List<string> entityNames, Dictionary<string, string> sqlWhereStatement)
    {
        var sqlUpsert = string.Empty;
        var sqlSelectUpsert = string.Empty;

        foreach (var treeKv in trees.Where(t => 
                     entityNames.Contains(t.Key.Split('~')[0])))
        {
            var processingTree = treeKv.Value;
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
                s.Key.Split('~')[0].Matches(processingTree.Name) && !s.Value.JoinKeys
                    .Any(a => a.To.Split('~')[0].Matches(processingTree.ParentName)));
            
            if (upsertingEntity.Value != null && upsertingEntity.Value.LinkKeys.Count > 0)
            {
                sqlUpsert += GenerateUpsert(processingTree, trees, sqlUpsertStatementNodes, whereCurrentClause);    
            }
            
            if ((entityNames.Any(e => processingTree.Children.Any(c => c.Name.Matches(e))) ||
                 entityNames.Any(e => e.Matches(processingTree.ParentName))) &&
                entityNames.Any(e => e.Matches(processingTree.Name)))
            {
                sqlSelectUpsert += GenerateSelectUpsert(processingTree, entityNames, trees,
                    sqlUpsertStatementNodes, whereCurrentClause);
            }
        }

        return sqlUpsert + " " + sqlSelectUpsert;
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
        string whereClause)
    {
        var sqlUpsertAux = string.Empty;
        var sqlNode = sqlUpsertStatementNodes.FirstOrDefault(s => s.Key.Split('~')[0].Matches(currentTree.Name));

        var currentColumns = sqlUpsertStatementNodes
            .Where(k => k.Key.Split('~')[0].Matches(currentTree.Name) &&
                        !k.Value.LinkBusinessKeys.Any(b => b.From.Matches(k.Key)) &&
                        (currentTree.Mapping.Any(m => m.FieldDestinationName.Matches(k.Key.Split('~')[1]) &&
                                                      !trees.Any(t => t.Value.Name.Matches(k.Key.Split('~')[1]))) ||
                         sqlNode.Value.UpsertKeys.Contains(k.Key))).ToList();

        if (currentColumns.Count == 0 ||
            !currentColumns.LastOrDefault().Value.UpsertKeys
                .Any(u => currentColumns.Any(c => c.Key.Matches(u))))
        {
            return sqlUpsertAux;
        }

        sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                        $" {string.Join(",", currentColumns.Select(s => $"\"{s.Value.Column}\"").ToList())}) VALUES ({
                            string.Join(",", currentColumns.Select(s => $"'{s.Value.Value}'").ToList())}) " +
                        $" ON CONFLICT" +
                        $" ({string.Join(",", currentColumns.LastOrDefault().Value.UpsertKeys
                            .Where(u => currentColumns.Any(c => c.Key.Matches(u)))
                            .Select(s => $"\"{s.Split('~')[1]}\"").ToList())}) ";

        var exclude = new List<string>();
        exclude.AddRange(
            currentColumns.Where(c => c.Value.UpsertKeys
                    .Any(u => !u.Matches(c.Value.Column)))
                .Select(e => $"\"{e.Value.Column}\" = EXCLUDED.\"{e.Value.Column}\"")
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
        Dictionary<string, SqlNode> sqlUpsertStatementNodes, string whereClause)
    {
        var sqlUpsertAux = string.Empty;
        var hasUpsert = true;
        var sqlNode = sqlUpsertStatementNodes.FirstOrDefault(s => s.Key.Split('~')[0].Matches(currentTree.Name));
        
        var currentColumns = sqlUpsertStatementNodes
            .Where(k => k.Key.Split('~')[0].Matches(currentTree.Name) &&
                        !k.Value.LinkBusinessKeys.Any(b => b.From.Matches(k.Key))).ToList();

        if (currentColumns.Count == 0 || !currentColumns.LastOrDefault().Value
                .UpsertKeys.All(c => currentColumns.Any(u => u.Key.Matches(c))))
        {
            return sqlUpsertAux;
        }

        foreach (var linkKey in currentColumns.LastOrDefault().Value.LinkKeys)
        {
            var tree = trees[linkKey.From.Split('~')[0]];

            if (currentTree.Name.Matches(tree.Name) ||
                (currentColumns.LastOrDefault().Value.JoinOneKeys.Count > 0 &&
                    currentColumns.LastOrDefault().Value.JoinOneKeys[0].To.Matches($"{currentTree.Name}~Id")))
            {
                continue;
            }

            var currentColumnsLink = sqlUpsertStatementNodes
                .Where(k => k.Key.Split('~')[0].Matches(tree.Name) &&
                            !k.Value.LinkBusinessKeys.Any(b => b.From.Matches(k.Key)) &&
                            (tree.Mapping.Any(m => m.FieldDestinationName.Matches(k.Key.Split('~')[1])) ||
                             sqlNode.Value.UpsertKeys.Contains(k.Key))).ToList();

            var upsertKeys = currentColumns.LastOrDefault().Value.UpsertKeys;
            var selectJoin = string.Empty;
            var insertJoin = string.Empty;
            var excludeJoin = string.Empty;

            insertJoin = $", \"{tree.Name}Id\"";

            selectJoin = $"{tree.Name}.\"Id\" AS" +
                         $" \"{tree.Name}Id\"";
            excludeJoin = $"\"{tree.Name}Id\" = EXCLUDED.\"{tree.Name}Id\"";

            var where = string.Join(" AND ", currentColumnsLink.Where(c =>
                    !entityNames.Contains(c.Value.Column))
                .Select(s => $"\"{s.Value.Column}\" = '{s.Value.Value}'"));

            var columns = currentColumns.Where(c => currentColumns
                .LastOrDefault().Value.UpsertKeys.Any(u => u.Split('~')[1].Matches(c.Value.Column))).ToList();

            var sqlUpsertAux2 = $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                                $" {string.Join(",", columns.Select(s => $"\"{
                                    s.Value.Column}\"").ToList())} {insertJoin} ) " +
                                $" ( SELECT {
                                    string.Join(",", columns.Select(m => $"'{
                                        m.Value.Value}' AS \"{
                                            m.Value.Column}\"").ToList())}" +
                                $", {selectJoin}" + $" FROM \"{tree.Schema}\".\"{tree.Name}\" {tree.Name} {
                                    (string.IsNullOrEmpty(where) ? "" : $" WHERE {where}")}";

            var exclude = new List<string>();
            exclude.AddRange(
                columns.Select(e =>
                    $"\"{e.Value.Column}\" = EXCLUDED.\"{e.Value.Column}\"")
            );

            exclude.Add(excludeJoin);

            sqlUpsertAux2 += $" ) ON CONFLICT" +
                             $" ({string.Join(",", upsertKeys.Select(s => $"\"{s.Split('~')[1]}\""))}) ";

            if (!string.IsNullOrEmpty(whereClause) || !string.IsNullOrEmpty(where))
            {
                var f = false;
            }

            if (exclude.Count > 0)
            {
                sqlUpsertAux2 += $" DO UPDATE SET {string.Join(",", exclude)} {whereClause};";
            }
            else
            {
                sqlUpsertAux2 += $" DO NOTHING {whereClause};";
            }

            sqlUpsertAux += sqlUpsertAux2;
        }

        return sqlUpsertAux;
    }
}