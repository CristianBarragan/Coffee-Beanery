using System.Text;
using Dapper;
using Domain.Util.GraphQL.Extension;
using HotChocolate.Language;
using Domain.Util.GraphQL.Model;
using FASTER.core;
using HotChocolate.Execution.Processing;

namespace Domain.Util.GraphQL.Helper;

public static class SqlNodeResolverHelper
{
    public static SqlStructure HandleGraphQL(ISelection graphQlSelection, Dictionary<string, NodeTree> trees,
        string rootEntityName, List<string> entityNames, IFasterKV<string, string> cache,
        Dictionary<string, List<string>> permissions = null)
    {
        var sqlWhereStatement = new Dictionary<string, string>();
        var sqlOrderStatement = string.Empty;
        var upsertSqlNodes = new List<SqlNode>();
        var parameters = new DynamicParameters();
        var sqlUpsert = new StringBuilder();
        var sqlNodes = new List<SqlNode>();
        var childrenColumns = new List<string>();
        var generatedEntities = new List<string>();
        var sqlQuery = new StringBuilder();
        var whereFields = new List<string>();
        var pagination = new Pagination();

        //Where conditions
        GetFieldsWhere(trees, whereFields, sqlWhereStatement, graphQlSelection.SyntaxNode.Arguments
                .Where(a => a.Name.Value.Matches("where")).First(), trees.Last().Value.Name, rootEntityName, Entity.ClauseTypes,
            permissions);

        //Arguments
        foreach (var argument in graphQlSelection.SyntaxNode.Arguments.Where(a => !a.Name.Value.Matches("where")))
        {
            switch (argument.Name.ToString())
            {
                case "first":
                    pagination.First = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                        ? 0
                        : int.Parse(argument.Value?.Value.ToString());
                    break;
                case "last":
                    pagination.Last = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                        ? 0
                        : int.Parse(argument.Value?.Value.ToString());
                    break;
                case "before":
                    pagination.Before = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                        ? ""
                        : argument.Value?.Value.ToString();
                    break;
                case "after":
                    pagination.After = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                        ? ""
                        : argument.Value?.Value.ToString();
                    break;
            }

            if (argument.Name.ToString().Contains("order"))
            {
                foreach (var orderNode in argument.GetNodes())
                {
                    sqlOrderStatement = GetFieldsOrdering(trees, orderNode, rootEntityName);
                }
            }

            if (argument.Name.Value.Matches(rootEntityName))
            {
                var nodeTreeRoot = new NodeTree();
                nodeTreeRoot.Name = string.Empty;
                GetMutations(ref parameters, trees, sqlUpsert, argument.Value.GetNodes().ToList()[0], nodeTreeRoot,
                    nodeTreeRoot, upsertSqlNodes, 1, sqlWhereStatement);
            }
        }

        // Query Select
        var sqlStament = string.Empty;

        //Read cache
        // using var cacheReadSession = cache.NewSession(new SimpleFunctions<string, string>());
        // var cacheKey = graphQlSelection.SelectionSet.Selections.ToString();
        // make where statement as parameters so it can be cached
        // cacheReadSession.Read(ref cacheKey, ref sqlStament);

        if (string.IsNullOrEmpty(sqlStament))
        {
            if (graphQlSelection.SelectionSet?.Selections!
                    .FirstOrDefault(s => s.ToString().StartsWith("edges")) != null)
            {
                GetFields(trees, graphQlSelection.SelectionSet?.Selections!
                        .FirstOrDefault(s => s.ToString().StartsWith("edges")).GetNodes().Skip(1).First(), sqlQuery,
                    sqlNodes,
                    childrenColumns, trees.Last().Value, new NodeTree(), entityNames, "Edges", generatedEntities,
                    sqlWhereStatement);
            }

            if (graphQlSelection.SelectionSet?.Selections!
                    .FirstOrDefault(s => s.ToString().StartsWith("nodes")) != null)
            {
                GetFields(trees, graphQlSelection.SelectionSet?.Selections!
                        .FirstOrDefault(s => s.ToString().StartsWith("nodes")), sqlQuery, sqlNodes,
                    childrenColumns, trees.Last().Value, new NodeTree(), entityNames, "Nodes", generatedEntities,
                    sqlWhereStatement);
            }

            sqlStament = sqlQuery.ToString();

            //Update cache
            // var status = cacheReadSession.Upsert(ref cacheKey, ref sqlStament);
        }
        else
        {
            sqlQuery.Append(sqlStament);
        }

        var splitOn = sqlStament.Split(" FROM ")[0].Split(',').Where(c => c.Split(" AS ")[1]
            .Contains("_Id_") && c.Split(" AS ")[1].Replace("_", "").Sanitize()
            .Length > 2).Select(c => c.Split(" AS ")[1].Sanitize()).ToList();

        var hasTotalCount = false;
        // Query Where, Sort, and Pagination

        var rootTree = new NodeTree();
        if (sqlNodes.Count == 0)
        {
            var rootTreeDotInt = sqlStament.IndexOf('.');
            var rootTreeSpaceInt = sqlStament.TrimStart(' ').IndexOf(' ');
            var rootTreeString = sqlStament.TrimStart(' ')
                .Substring(rootTreeSpaceInt + 1, rootTreeDotInt - rootTreeSpaceInt - 2);
            rootTree = trees[rootTreeString];
        }
        else
        {
            rootTree = sqlNodes.Last().NodeTree;
        }

        HandleQueryClause(rootTree, sqlQuery,
            sqlOrderStatement, pagination, hasTotalCount);

        var sqlStructure = new SqlStructure()
        {
            SqlQuery = sqlQuery.ToString(),
            Parameters = parameters,
            SqlUpsert = sqlUpsert.ToString(),
            SplitOnDapper = splitOn,
            Pagination = pagination,
            HasTotalCount = false
        };

        return sqlStructure;
    }

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

    public static void GetMutations(in DynamicParameters parameters, Dictionary<string, NodeTree> trees,
        StringBuilder sqlUpsert, ISyntaxNode upsertNode, NodeTree currentTree,
        NodeTree parentTree, List<SqlNode> sqlNodes, int child, Dictionary<string, string> sqlWhereStatement,
        Dictionary<string, List<string>> permissions = null)
    {
        var insert = new List<string>();
        var select = new List<string>();
        var exclude = new List<string>();

        if (!upsertNode.ToString().Split(':')[0].Replace("{", "").Trim().Matches(currentTree.Name) &&
            trees.ContainsKey(upsertNode.ToString().Split(':')[0].Replace("{", "").Trim()))
        {
            parentTree = currentTree;
            currentTree = trees[upsertNode.ToString().Split(':')[0].Replace("{", "").Trim()];
        }

        foreach (var uNode in upsertNode.GetNodes())
        {
            if (uNode.ToString().Split('{').Length > 1)
            {
                GetMutations(parameters, trees, sqlUpsert, uNode, currentTree, parentTree, sqlNodes,
                    child, sqlWhereStatement, permissions);
            }
            else
            {
                if (uNode.ToString().Split(':').Length == 2)
                {
                    SqlGraphQLHelper.HandleUpsertField(insert, select, exclude, currentTree, sqlNodes,
                        uNode.ToString().Split(':')[0].Trim(),
                        uNode.ToString().Split(':')[1].Sanitize(), false, child++);
                    continue;
                }

                if (uNode.ToString().Split(':').Length == 4 && uNode.ToString().Split('.')[1].Length == 5 &&
                    uNode.ToString().Split('.')[1][3] == 'Z')
                {
                    SqlGraphQLHelper.HandleUpsertField(insert, select, exclude, currentTree, sqlNodes,
                        uNode.ToString().Split(':')[0].Trim(),
                        string.Join('`', uNode.ToString().Split(':').Skip(1).ToList()).Replace('"', '\''), true,
                        child++);
                }
            }
        }

        var sqlUpsertAux = string.Empty;

        if (insert.Count > 0 && sqlNodes.Count > 0)
        {
            var querySelect = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < sqlNodes.Count; i++)
            {
                sqlUpsertAux = string.Empty;
                for (var j = 0; j < sqlNodes[i].Values.Count; j++)
                {
                    for (var k = 0; k < sqlNodes[i].Values.ElementAt(j).Value.Count; k++)
                    {
                        if (sqlNodes[i].Values.ElementAt(j).Value.Count == 0)
                        {
                            break;
                        }

                        var fieldParts = sqlNodes[i].Values.ElementAt(j).Value[k].Split('~');

                        if (!fieldParts[0].Matches(currentTree.Id.ToString()))
                        {
                            break;
                        }

                        if (i != 0)
                        {
                            querySelect.Add(new KeyValuePair<string, string>($"{fieldParts[2]}", $"'{fieldParts[3]}'"));
                        }
                        else
                        {
                            querySelect.Add(new KeyValuePair<string, string>($"{fieldParts[2]}", $"'{fieldParts[3]}'"));
                        }

                        sqlNodes[i].Values.ElementAt(j).Value.RemoveAt(k);
                    }
                }
            }

            var whereClause = string.Empty;
            var whereParentValue = sqlWhereStatement.GetValueOrDefault(parentTree.Name);

            if (!string.IsNullOrEmpty(whereParentValue) &&
                !parentTree.UpsertKeys.Any(u => whereParentValue.Contains(u)) &&
                !parentTree.UpsertKeys.Any(u => u.Matches(whereParentValue
                    .Split('=')[0].Split('.')[1].Sanitize())))
            {
                whereClause = $" WHERE {whereParentValue.Replace("~", parentTree.Name)}";
            }

            var whereCurrentValue = sqlWhereStatement.GetValueOrDefault(currentTree.Name);

            if (querySelect.Count > 0 && !string.IsNullOrEmpty(currentTree.ParentName) &&
                sqlNodes.Any(n => n.NodeTree.Name.Matches(currentTree.ParentName)) &&
                sqlNodes.Any(n => n.NodeTree.Name.Matches(currentTree.Name)))
            {
                if (!string.IsNullOrEmpty(whereCurrentValue) &&
                    !parentTree.UpsertKeys.Any(u => whereCurrentValue.Contains(u)))
                {
                    if (string.IsNullOrEmpty(whereClause))
                    {
                        whereClause = $" WHERE {whereCurrentValue.Replace("~", currentTree.Name)}";
                    }
                    else
                    {
                        whereClause += $" AND {whereCurrentValue.Replace("~", currentTree.Name)}";
                    }
                }

                insert.Add($"{currentTree.ParentName}Id");

                sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                                $" {string.Join(",", insert.Select(s => $"\"{s}\""))} ) " +
                                $" ( SELECT {string.Join(",", querySelect.Select(s => $"{s.Value} AS \"{s.Key}\""))}," +
                                $" {trees[currentTree.ParentName].Name}.\"Id\" AS \"{currentTree.ParentName}Id\" " +
                                $" FROM \"{trees[currentTree.ParentName].Schema}\".\"{currentTree.ParentName}\" {currentTree.ParentName} WHERE ";

                var whereUpsertKeys = string.Empty;
                var parentTreeNode = trees[currentTree.ParentName];

                for (var i = 0; i < parentTreeNode.UpsertKeys.Count; i++)
                {
                    whereUpsertKeys +=
                        $" {parentTreeNode.Name}.\"{parentTreeNode.UpsertKeys[i]}\" = {querySelect.Where(p =>
                            p.Key.Matches(parentTreeNode.UpsertKeys[i]))?.FirstOrDefault().Value ?? default} ";
                    whereUpsertKeys += i == parentTreeNode.UpsertKeys.Count - 1 ? " ) " : " AND ";
                }

                sqlUpsertAux += $" {whereUpsertKeys} ";
                sqlUpsertAux += $" ON CONFLICT" +
                                $" ({string.Join(",", currentTree.UpsertKeys.Select(s => $"\"{s}\""))}) ";

                if (exclude.Count > 0)
                {
                    sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)} {whereClause};";
                }
                else
                {
                    sqlUpsertAux += $" DO NOTHING {whereClause};";
                }

                sqlUpsert.Insert(0, sqlUpsertAux);
            }
            else if (querySelect.Count > 0 && sqlNodes.Any(n => n.NodeTree.Name.Matches(currentTree.Name)) &&
                     !sqlNodes.Any(n => n.NodeTree.Name.Matches(currentTree.ParentName)))
            {
                sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                                $" {string.Join(",", insert.Select(s => $"\"{s}\""))}) VALUES ({
                                    string.Join(",", querySelect.Where(p => p.Key.Length > 1)
                                        .Select(k => k.Value))}) " +
                                $" ON CONFLICT" +
                                $" ({string.Join(",", currentTree.UpsertKeys.Select(s => $"\"{s}\""))}) ";
                if (exclude.Count > 0)
                {
                    sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)} {whereClause};";
                }
                else
                {
                    sqlUpsertAux += $" DO NOTHING {whereClause};";
                }

                sqlUpsert.Insert(0, sqlUpsertAux);
            }
        }
    }

    public static void GetFields(Dictionary<string, NodeTree> trees, ISyntaxNode node, StringBuilder sql,
        List<SqlNode> sqlNodes, List<string> childrenColumns, NodeTree currentTree, NodeTree parentTree,
        List<string> entityNames, string queryType,
        List<string> generatedEntities, Dictionary<string, string> sqlWhereStatement)
    {
        var treeKey = node.ToString().Split(':')[0].Trim();

        if (trees.ContainsKey(treeKey))
        {
            parentTree = currentTree;
            currentTree = trees[treeKey];
        }

        treeKey = node.ToString().Split('{')[0].Trim();
        if (trees.ContainsKey(treeKey))
        {
            currentTree = trees[treeKey];
        }

        if (node.ToString().Split('{')[1].Trim().Matches("node") ||
            node.ToString().Split('{')[1].Trim().Matches("nodes"))
        {
            currentTree = trees.Last().Value;
        }

        foreach (var uNode in node.GetNodes())
        {
            if (uNode.ToString().Split('{').Length > 1)
            {
                GetFields(trees, uNode, sql, sqlNodes, childrenColumns, currentTree, parentTree, entityNames, queryType,
                    generatedEntities, sqlWhereStatement);
            }
            else
            {
                if (!uNode.ToString().Matches("node") &&
                    uNode.ToString().Split('~').Length == 1 &&
                    currentTree.Mappings.ContainsKey(uNode.ToString()) &&
                    !entityNames.Any(e => e.Matches(uNode.ToString())))
                {
                    var field = $"{currentTree.Name}.\"{currentTree.Mappings[uNode.ToString()]}\"";
                    if (!string.IsNullOrEmpty(field))
                    {
                        var sqlNode = sqlNodes.FirstOrDefault(s => s.NodeTree.Name.Matches(currentTree.Name));

                        if (sqlNode != null)
                        {
                            if (sqlNode.SqlNodeType != SqlNodeType.Edge &&
                                queryType.Matches("Edges"))
                            {
                                sqlNode.SqlNodeType = SqlNodeType.Edge;
                            }

                            if (!sqlNode.SelectColumns.ToList()
                                    .Any(column => sqlNode.SelectColumns.Contains(field)))
                            {
                                sqlNode.SelectColumns.Add(field);
                            }
                        }
                        else
                        {
                            sqlNode = new SqlNode();
                            sqlNode.SqlNodeType = queryType.Matches("Edges") ? SqlNodeType.Edge : SqlNodeType.Node;
                            sqlNode.SelectColumns = new List<string>();
                            sqlNode.SelectColumns.Add(field);

                            if (!currentTree.JoinKey.Matches(currentTree.Mappings[uNode.ToString()]) &&
                                !string.IsNullOrEmpty(currentTree.JoinKey))
                            {
                                sqlNode.SelectColumns.Add($"{currentTree.Name}.\"{currentTree.JoinKey}\"");
                                sqlNode.SelectColumns.Insert(0, $"{currentTree.Name}.\"Id\"");
                            }

                            sqlNode.NodeTree = currentTree;
                            sqlNodes.Add(sqlNode);
                        }
                    }
                }
            }
        }

        if (queryType.Matches("Edges"))
        {
            //Generate query statement
            var index = sqlNodes.FindIndex(s => s.NodeTree.Name.Matches(currentTree.Name));

            if (index < 0)
            {
                return;
            }

            if (sqlNodes[index] != null && sqlNodes[index].SelectColumns.Count > 0 &&
                !generatedEntities.Any(e => e.Matches(currentTree.Name)))
            {
                var currentSqlWhereStatement = sqlWhereStatement.GetValueOrDefault(currentTree.Name);

                if (!string.IsNullOrEmpty(currentSqlWhereStatement))
                {
                    currentSqlWhereStatement = currentSqlWhereStatement.Replace("~", currentTree.Name);

                    foreach (var field in currentSqlWhereStatement.Split("\""))
                    {
                        if (currentTree.Mappings.ContainsKey(field))
                        {
                            currentSqlWhereStatement =
                                currentSqlWhereStatement.Replace(field, $"{field.ToSnakeCase(currentTree.Id)}");
                        }
                    }

                    currentSqlWhereStatement = $" WHERE {currentSqlWhereStatement} ";
                }
                else
                {
                    currentSqlWhereStatement = string.Empty;
                }

                if (index > 0 && index - 1 < sqlNodes.Count)
                {
                    sql.Insert(0, $" {(sqlNodes[index].SqlNodeType == SqlNodeType.Edge ? " JOIN " : " LEFT JOIN ")} " +
                                  $"( SELECT {currentTree.Name}.\"Id\" AS \"{currentTree.Name}_{"Id".ToSnakeCase(currentTree.Id)}\", " +
                                  $" {string.Join(",", sqlNodes[index].SelectColumns.Where(c => !c.Split('.')[1]
                                          .Matches("\"Id\""))
                                      .Select(s => $"{currentTree.Name}.\"{s.Split('.')[1].Sanitize()}\" AS " +
                                                   $"\"{s.Split('.')[1].Sanitize().ToSnakeCase(currentTree.Id)}\""))}" +
                                  $"{(generatedEntities.Count(s => currentTree.Children.Any(c =>
                                      c.Name.Matches(s))) > 0 ? "," : "")} " +
                                  $"FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name} ) " +
                                  $" {currentTree.Name} " +
                                  $" ON {trees[currentTree.ParentName].Name}.\"Id\" = " +
                                  $"{currentTree.Name}.\"{currentTree.JoinKey.ToSnakeCase(currentTree.Id)}\" {currentSqlWhereStatement} ");
                }
                else
                {
                    var auxChildrenColumns = new List<string>();
                    var auxCurrentColumns = new List<string>();

                    foreach (var sqlNodeSelectColumn in sqlNodes)
                    {
                        foreach (var column in sqlNodeSelectColumn.SelectColumns)
                        {
                            foreach (var child in currentTree.Children)
                            {
                                var childName = column.Split('.');
                                if (childName[0].Matches(child.Name))
                                {
                                    if (childName[1].StartsWith("\"Id"))
                                    {
                                        auxChildrenColumns.Add(
                                            $"{childName[0]}.\"{childName[0]}_{childName[1].Sanitize().ToSnakeCase(trees[childName[0]].Id)}\" AS \"{childName[0]}_{childName[1].Sanitize().ToSnakeCase(trees[childName[0]].Id)}\"");
                                        auxChildrenColumns.Add(
                                            $"{childName[0]}.\"{childName[0]}_{childName[1].Sanitize().ToSnakeCase(trees[childName[0]].Id)}\" AS \"_{childName[1].Sanitize().ToSnakeCase(trees[childName[0]].Id)}\"");
                                    }
                                    else
                                    {
                                        auxChildrenColumns.Add(
                                            $"{childName[0]}.\"{childName[1].Sanitize().ToSnakeCase(trees[childName[0]].Id)}\" AS \"{childName[1].Sanitize().ToSnakeCase(trees[childName[0]].Id)}\"");
                                    }
                                }
                                else if (childName[0].Matches(currentTree.Name) &&
                                         !auxCurrentColumns.Contains($"{currentTree.Name}.{childName[1]}"))
                                {
                                    auxCurrentColumns.Add($"{currentTree.Name}.{childName[1]}");
                                }
                            }
                        }
                    }


                    sql.Insert(0,
                        $" SELECT {currentTree.Name}.\"{"Id"}\" AS \"{currentTree.Name}_{"Id".ToSnakeCase(currentTree.Id)}\", " +
                        $"{currentTree.Name}.\"{"Id"}\" AS \"_{"Id".ToSnakeCase(currentTree.Id)}\", " +
                        $"{string.Join(",", auxCurrentColumns
                            .Select(s => $"{s.Split('.')[0].Sanitize()}.\"{s.Split('.')[1].Sanitize()}\" AS " +
                                         $"\"{s.Split('.')[1].Sanitize()}\""))}" +
                        $", {string.Join(",", auxChildrenColumns
                            .Select(s => s))}" +
                        $" FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name} ");
                }

                generatedEntities.Add(currentTree.Name);
                childrenColumns.AddRange(sqlNodes[index].SelectColumns);
            }
        }
    }

    private static void AddToDictionary(Dictionary<string, string> dictionary, string key, List<string> values)
    {
        foreach (var value in values)
        {
            if (!dictionary.TryGetValue(key, out var _))
            {
                dictionary.Add(key, value);
            }
            else
            {
                dictionary[key] = value;
            }
        }
    }

    public static string GetFieldsOrdering(Dictionary<string, NodeTree> trees, ISyntaxNode orderNode, string entity)
    {
        var orderString = string.Empty;
        foreach (var oNode in orderNode.GetNodes())
        {
            var currentEntity = entity;
            if (oNode.ToString().Contains("{") && oNode.ToString()[0] != '{' && oNode.ToString().Contains(":"))
            {
                currentEntity = oNode.ToString().Split(":")[0];
            }

            if (!oNode.ToString().Contains("{") && oNode.ToString().Contains(":"))
            {
                var column = oNode.ToString().Split(":");
                if ((column[1].Contains("DESC") || column[1].Contains("ASC")) &&
                    trees.ContainsKey(currentEntity))
                {
                    var currentNodeTree = trees[currentEntity];
                    orderString += SqlGraphQLHelper.HandleSort(currentNodeTree, column[0], column[1]);
                }
            }

            orderString += $", {GetFieldsOrdering(trees, oNode, currentEntity)}";
        }

        return orderString;
    }

    public static void GetFieldsWhere(Dictionary<string, NodeTree> trees, List<string> whereFields,
        Dictionary<string, string> sqlWhereStatement,
        ISyntaxNode whereNode, string entityName, string rootEntityName, List<string> clauseType,
        Dictionary<string, List<string>> permission = null)
    {
        foreach (var wNode in whereNode.GetNodes())
        {
            var currentEntity = entityName;
            var clauseCondition = string.Empty;

            currentEntity = trees.Keys.FirstOrDefault(e => e.ToString().Matches(wNode.ToString().Split(":")[0]));

            if (string.IsNullOrEmpty(currentEntity) || currentEntity.Matches(rootEntityName))
            {
                if (currentEntity.Matches(rootEntityName))
                {
                    currentEntity = trees.Last().Key;
                }
                else
                {
                    currentEntity = entityName;
                }
            }

            if (whereNode.ToString().TrimStart(' ').StartsWith("and:") ||
                whereNode.ToString().TrimStart(' ').StartsWith("or:"))
            {
                clauseCondition = whereNode.ToString().Split("{")[0];
            }

            if (wNode.ToString().Contains("{") && wNode.ToString().Contains(":") &&
                wNode.ToString().Split(":").Length == 3)
            {
                var column = wNode.ToString().Split(":")[0];

                if (!column.Contains("{"))
                {
                    whereFields.Add($"{currentEntity}.{column}");
                }
            }

            foreach (var node in wNode.GetNodes().ToList())
            {
                if (!node.ToString().Contains("{") && node.ToString().Contains(":") &&
                    node.ToString().Split(":").Length == 2)
                {
                    var column = node.ToString().Split(":");
                    if (!column[1].Contains("DESC") && !column[1].Contains("ASC") && clauseType.Contains(column[0]))
                    {
                        var clauseValue = "";
                        var fieldParts = whereFields.Last().Split('.');
                        var currentNodeTree = trees[currentEntity];
                        var field = fieldParts[1];

                        switch (column[0])
                        {
                            case "eq":
                            {
                                var clause = SqlGraphQLHelper.ProcessFilter(currentNodeTree, field, "=",
                                    clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, currentEntity, clause);
                                break;
                            }
                            case "neq":
                            {
                                var clause = SqlGraphQLHelper.ProcessFilter(currentNodeTree, field, "<>",
                                    clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, currentEntity, clause);
                                break;
                            }
                            case "in":
                            {
                                clauseValue = "(" + string.Join(',',
                                    column[1].Replace("[", "").Replace("]", "").Split(',')
                                        .Select(v => $"'{v.Trim()}'")) + ")";
                                var clause = SqlGraphQLHelper.ProcessFilter(currentNodeTree, field, "in",
                                    clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, currentEntity, clause);
                                break;
                            }
                        }
                    }
                }
            }

            GetFieldsWhere(trees, whereFields, sqlWhereStatement, wNode, currentEntity, rootEntityName, clauseType, permission);
        }
    }
}