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
    public static void HandleQueryClause(NodeTree rootTree, StringBuilder sqlQuery, string sqlOrderStatement,
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
        sqlQuery.Clear();
        sqlQuery.Append($" {sql} a ) " +
                        $"SELECT * FROM ( SELECT (SELECT COUNT(DISTINCT \"{rootTree.Name}_{"Id".ToSnakeCase(rootTree.Id)}\") FROM {rootTree.Schema}s) \"RecordCount\", " +
                        $"{totalCount} * FROM {rootTree.Schema}s) a {sqlWhereStatement.Replace('~', 'a')}");
    }
    
    public static string GenerateUpsertStatements(Dictionary<string,SqlNode> linkEntityDictionaryTree,
        Dictionary<string, NodeTree> trees, Dictionary<string,SqlNode> sqlUpsertStatementNodes,
        List<string> entityNames, Dictionary<string, string> sqlWhereStatement)
    {
        var sqlUpsertStatement = string.Empty;

        foreach (var treeKv in trees.Where(t => entityNames.Contains(t.Key.Split('~')[0])))
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

            sqlUpsertStatement += GenerateUpsert(processingTree, sqlUpsertStatementNodes, whereCurrentClause, entityNames);
            
            if ((entityNames.Any(e => processingTree.Children.Any(c => c.Name.Matches(e))) ||
                 entityNames.Any(e => e.Matches(processingTree.ParentName))) &&
                entityNames.Any(e => e.Matches(processingTree.Name)))
            {
                sqlUpsertStatement += GenerateSelectUpsert(processingTree, trees,
                    sqlUpsertStatementNodes, whereCurrentClause);
            }
        }
        return sqlUpsertStatement;
    }
    
    public static string GenerateUpsert(NodeTree currentTree, Dictionary<string,SqlNode> sqlUpsertStatementNodes, 
        string whereClause, List<string> entityNames)
    {
        var sqlUpsertAux = string.Empty;
        var currentColumns = sqlUpsertStatementNodes
            .Where(k => !k.Value.LinkKeys.Any(l => l.From.Matches(k.Key)) && 
                        k.Key.Split('~')[0].Matches(currentTree.Name)).ToList();
        
        if (currentColumns.Count == 0 ||
            !currentColumns[currentColumns!.Count-1].Value.UpsertKeys
                .Any(u => currentColumns.Any(c => c.Key.Matches(u))))
        {
            return sqlUpsertAux;
        }
        
        sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                        $" {string.Join(",", currentColumns.Select(s => $"\"{s.Value.Column}\"").ToList())}) VALUES ({
                            string.Join(",", currentColumns.Select(s => $"'{s.Value.Value}'").ToList())}) " +
                        $" ON CONFLICT" +
                        $" ({string.Join(",", currentColumns[currentColumns!.Count-1].Value.UpsertKeys
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

    public static string GenerateSelectUpsert(NodeTree currentTree, Dictionary<string, NodeTree> trees, 
        Dictionary<string,SqlNode> sqlUpsertStatementNodes, string whereClause)
    {
        var sqlUpsertAux = string.Empty;
        var hasUpsert = true;
        var currentColumns = sqlUpsertStatementNodes
            .Where(k => k.Key.Split('~')[0].Matches(currentTree.Name)).ToList();

        if (currentColumns.Count == 0 || !currentColumns.LastOrDefault().Value
                .UpsertKeys.All(c => currentColumns.Any(u => u.Key.Matches(c))))
        {
            return sqlUpsertAux;
        }
        
        foreach (var joinKey in currentColumns.LastOrDefault().Value.JoinKeys.Where(jk => jk.From.Split('~')[0].Matches(currentTree.Name)))
        {
            hasUpsert = true;
            for (var i = 0; i < 1; i++)
            {
                var tree = trees[joinKey.To.Split('~')[0]];
                var upsertKeys = currentColumns.LastOrDefault().Value.UpsertKeys;
                var selectJoin = string.Empty;
                var insertJoin = string.Empty;
                // var whereColumns = new List<KeyValuePair<string, SqlNode>>();
                var excludeJoin = string.Empty;
                var sqlUpsertStatementNodesJoin = sqlUpsertStatementNodes
                    .Where(k => k.Key.Split('~')[0].Matches(tree.Name)).ToList();

                if (!sqlUpsertStatementNodesJoin.Any())
                {
                    continue;
                }

                if (i == 0)
                {
                    insertJoin = $", \"{joinKey.To.Split('~')[1]}\"";
                    selectJoin = $"{joinKey.To.Split('~')[0]}.\"{joinKey.To.Split('~')[1]}\" AS" +
                                 $" \"{joinKey.To.Split('~')[0]}{joinKey.To.Split('~')[1]}\"";
                    excludeJoin = $"\"{joinKey.To.Split('~')[1]}\" = EXCLUDED.\"{joinKey.To.Split('~')[1]}\"";
                    // whereColumns = sqlUpsertStatementNodesJoin
                }

                if (i == 1)
                {
                    insertJoin = $", \"{joinKey.From.Split('~')[1]}\"";
                    selectJoin = $"{tree.Name}.\"Id\" AS" +
                                 $" \"{joinKey.From.Split('~')[1]}\"";
                    excludeJoin = $"\"{joinKey.From.Split('~')[1]}\" = EXCLUDED.\"{joinKey.From.Split('~')[1]}\"";
                    // whereColumns = sqlUpsertStatementNodesJoin
                    //     .Where(k => k.Key.Split('~')[0].Matches(joinKey.From.Split('~')[0])).ToList();
                }

                var sqlUpsertAux2 = $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                                    $" {string.Join(",", currentColumns.Select(s => $"\"{
                                        s.Value.Column}\"").ToList())} {insertJoin} ) " +
                                    $" ( SELECT {
                                        string.Join(",", currentColumns.Select(m => $"'{
                                            m.Value.Value}' AS \"{
                                                m.Value.Column}\"").ToList())}" +
                                    $", {selectJoin}" + $" FROM \"{tree.Schema}\".\"{tree.Name}\" {tree.Name} WHERE {
                                        string.Join(" AND ", sqlUpsertStatementNodesJoin.Select(s => $"\"{s.Value.Column}\" = '{s.Value.Value}'"))
                                    }";

                var exclude = new List<string>();
                exclude.AddRange(
                    currentColumns.Where(c => !currentColumns[currentColumns!.Count-1].Value
                        .UpsertKeys.Contains($"{currentTree.Name}~{c.Value.Column}")).Select(e => 
                        $"\"{e.Value.Column}\" = EXCLUDED.\"{e.Value.Column}\"")
                );

                exclude.Add(excludeJoin);
            
                sqlUpsertAux2 += $" ) ON CONFLICT" +
                                 $" ({string.Join(",", upsertKeys.Select(s => $"\"{s.Split('~')[1]}\""))}) ";

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
        }

        if (hasUpsert)
        {
            foreach (var joinKey in currentColumns.LastOrDefault().Value.LinkKeys.Where(jk => jk.From.Split('~')[0].Matches(currentTree.Name)))
            {
                for (var i = 0; i < 1; i++)
                {
                    var tree = trees[joinKey.To.Split('~')[0]];
                    currentColumns = sqlUpsertStatementNodes
                        .Where(k => k.Key.Split('~')[0].Matches(tree.Name)).ToList();

                    if (!currentColumns.Any())
                    {
                        continue;
                    }
                    
                    var upsertKeys = currentColumns.LastOrDefault().Value.UpsertKeys
                        .Where(u => tree.Mapping.Any(m => $"{m.DestinationEntity}~{m.FieldDestinationName}".Matches(u))).ToList();
                    var selectJoin = string.Empty;
                    var insertJoin = string.Empty;
                    // var whereColumns = new List<KeyValuePair<string, SqlNode>>();
                    var excludeJoin = string.Empty;
                    var sqlUpsertStatementNodesJoin = sqlUpsertStatementNodes
                        .Where(k => k.Key.Split('~')[0].Matches(currentTree.Name)).ToList();
                    
                    if (!sqlUpsertStatementNodesJoin.Any(n => upsertKeys.Contains(n.Key)))
                    {
                        continue;
                    }

                    if (i == 0)
                    {
                        insertJoin = $", \"{joinKey.From.Split('~')[0]}Id\"";
                        selectJoin = $"{joinKey.From.Split('~')[0]}.\"Id\" AS" +
                                     $" \"{joinKey.From.Split('~')[0]}Id\"";
                        excludeJoin = $"\"{joinKey.From.Split('~')[0]}Id\" = EXCLUDED.\"{joinKey.From.Split('~')[0]}Id\"";
                        // whereColumns = sqlUpsertStatementNodesJoin
                    }

                    if (i == 1)
                    {
                        insertJoin = $", \"{joinKey.From.Split('~')[1]}\"";
                        selectJoin = $"{tree.Name}.\"{joinKey.From.Split('~')[1]}\" AS" +
                                     $" \"{joinKey.From.Split('~')[1]}\"";
                        excludeJoin = $"\"{joinKey.From.Split('~')[1]}\" = EXCLUDED.\"{joinKey.From.Split('~')[1]}\"";
                        // whereColumns = sqlUpsertStatementNodesJoin
                        //     .Where(k => k.Key.Split('~')[0].Matches(joinKey.From.Split('~')[0])).ToList();
                    }

                    var sqlUpsertAux2 = $" INSERT INTO \"{tree.Schema}\".\"{tree.Name}\" ( " +
                                        $" {string.Join(",", currentColumns.Select(s => $"\"{
                                            s.Value.Column}\"").ToList())} {insertJoin} ) " +
                                        $" ( SELECT {
                                            string.Join(",", currentColumns.Select(m => $"'{
                                                m.Value.Value}' AS \"{
                                                    m.Value.Column}\"").ToList())}" +
                                        $", {selectJoin}" + $" FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name} WHERE {
                                            string.Join(" AND ", sqlUpsertStatementNodesJoin.Select(s => $"\"{s.Value.Column}\" = '{s.Value.Value}'"))
                                        }";

                    var exclude = new List<string>();
                    exclude.AddRange(
                        currentColumns.Where(c => !currentColumns.LastOrDefault().Value
                            .UpsertKeys.Contains($"{tree.Name}~{c.Value.Column}")).Select(e => 
                            $"\"{e.Value.Column}\" = EXCLUDED.\"{e.Value.Column}\"")
                    );

                    exclude.Add(excludeJoin);
                
                    sqlUpsertAux2 += $" ) ON CONFLICT" +
                                     $" ({string.Join(",", upsertKeys.Select(s => $"\"{s.Split('~')[1]}\""))}) ";

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
            }
        }
        
        

        return sqlUpsertAux;
    }
}