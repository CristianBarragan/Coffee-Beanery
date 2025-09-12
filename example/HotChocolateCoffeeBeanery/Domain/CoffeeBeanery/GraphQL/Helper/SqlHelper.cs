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

            if (entityNames.Any(e => e.Matches(processingTree.ParentName)) &&
                entityNames.Any(e => e.Matches(processingTree.Name)))
            {
                sqlUpsertStatement += GenerateSelectUpsert(processingTree, trees[processingTree.ParentName],
                    sqlUpsertStatementNodes, whereCurrentClause, entityNames);
            }
            else if (entityNames.Any(e => e.Matches(processingTree.Name)))
            {
                sqlUpsertStatement += GenerateUpsert(processingTree, sqlUpsertStatementNodes, whereCurrentClause, entityNames);
            }
        }
        return sqlUpsertStatement;
    }
    
    public static string GenerateUpsert(NodeTree currentTree, Dictionary<string,SqlNode> sqlUpsertStatementNodes, 
        string whereClause, List<string> entityNames)
    {
        var sqlUpsertAux = string.Empty;
        var currentColumns = sqlUpsertStatementNodes
            .Where(k => k.Key.Split('~')[0].Matches(currentTree.Name)).ToList();

        if (currentColumns.Count == 0)
        {
            return sqlUpsertAux;
        }
        
        sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                        $" {string.Join(",", currentColumns.Select(s => $"\"{s.Value.Column}\"").ToList())}) VALUES ({
                            string.Join(",", currentColumns.Select(s => $"'{s.Value.Value}'").ToList())}) " +
                        $" ON CONFLICT" +
                        $" ({string.Join(",", currentColumns[0].Value.UpsertKeys.Select(s => $"\"{s.Split('~')[1]}\"").ToList())}) ";
        
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

    public static string GenerateSelectUpsert(NodeTree currentTree, NodeTree parentTree, 
        Dictionary<string,SqlNode> sqlUpsertStatementNodes, string whereClause, List<string> entityNames)
    {
        var sqlUpsertAux = string.Empty;
        var insert = new List<string>();
        var currentColumns = sqlUpsertStatementNodes
            .Where(k => k.Key.Split('~')[0].Matches(currentTree.Name)).ToList();

        if (currentColumns.Count == 0)
        {
            return sqlUpsertAux;
        }
        
        insert.AddRange(currentColumns.Select(m => m.Value.Column));
        insert.Add($"{currentTree.ParentName}Id");
        var joinKey = currentColumns[0].Value.JoinKeys.FirstOrDefault(jk => jk.To.Split('~')[0].Matches(parentTree.Name));

        sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                        $" {string.Join(",", currentColumns.Select(s => $"\"{
                            s.Value.Column}\"").ToList())} {
                            (joinKey != null ? $", \"{joinKey.To.Split('~')[0]}\".{joinKey.To.Split('~')[1]}" : "")} ) " +
                        $" ( SELECT {
                            string.Join(",", currentColumns.Select(m => $"'{
                                m.Value.Value}' AS \"{
                                    m.Value.Column}\"").ToList())}" +
                        $" {
                            (joinKey != null ? $", {joinKey.To.Split('~')[0]}.\"{joinKey.To.Split('~')[1]}\" AS" +
                                               $" \"{joinKey.To.Split('~')[0]}{joinKey.To.Split('~')[1]}\" " : "")}" +
                        $" FROM \"{parentTree.Schema}\".\"{parentTree.Name}\" {parentTree.Name} ";

        if (currentTree.Name.Contains("Cont"))
        {
            var a = false;
        }
        
        var upsertKeys = currentColumns[0].Value.UpsertKeys;

        var exclude = new List<string>();
        exclude.AddRange(
                currentColumns.Where(c => !currentColumns[0].Value
                    .UpsertKeys.Contains($"{currentTree.Name}~{c.Value.Column}")).Select(e => 
                $"\"{e.Value.Column}\" = EXCLUDED.\"{e.Value.Column}\"")
            );
        
        sqlUpsertAux += $" ) ON CONFLICT" +
                        $" ({string.Join(",", upsertKeys.Select(s => $"\"{s.Split('~')[1]}\""))}) ";

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
}