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
        var entityMap = new Dictionary<string, List<GraphElement>>(StringComparer.OrdinalIgnoreCase);
        var sqlWhereStatement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sqlOrderStatement = string.Empty;
        var upsertSqlNodes = new List<SqlNode>();
        var parameters = new DynamicParameters();
        var sqlUpsert = new StringBuilder();
        var sqlQuery = new StringBuilder();
        var whereFields = new List<string>();
        var pagination = new Pagination();

        //Where conditions
        GetFieldsWhere(trees, whereFields, sqlWhereStatement, graphQlSelection.SyntaxNode.Arguments
                .FirstOrDefault(a => a.Name.Value.Matches("where")), trees.Last().Value.Name, rootEntityName, Entity.ClauseTypes,
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
        var level = 1;
        var rootNodeTree = new NodeTree();

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
                        .FirstOrDefault(s => s.ToString().StartsWith("edges")).GetNodes().Skip(1).First(), trees.Last().Value, 
                    new NodeTree(), entityNames, entityMap, true);
            }

            if (graphQlSelection.SelectionSet?.Selections!
                    .FirstOrDefault(s => s.ToString().StartsWith("nodes")) != null)
            {
                GetFields(trees, graphQlSelection.SelectionSet?.Selections!
                        .FirstOrDefault(s => s.ToString().StartsWith("nodes")), trees.Last().Value, 
                    new NodeTree(), entityNames, entityMap, false);
            }
            
            rootNodeTree = trees.Last().Value;
            
            var setQuery = GenerateQuery(trees, entityMap, [], sqlWhereStatement, rootNodeTree, 1);
            
            sqlStament = setQuery.sqlStatement;
            rootNodeTree = setQuery.rootNodeTree;

            //Update cache
            // var status = cacheReadSession.Upsert(ref cacheKey, ref sqlStament);
        }

        if (string.IsNullOrEmpty(sqlStament))
        {
            return default;
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
        HandleQueryClause(rootNodeTree, sqlQuery,
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

    private static (string sqlStatement, NodeTree rootNodeTree) GenerateQuery(Dictionary<string, NodeTree> trees, Dictionary<string, List<GraphElement>> entityMap,
        List<string> childrenFields, Dictionary<string, string> sqlWhereStatement, NodeTree currentTree, int isRootEntity)
    {
        var childrenSqlStatement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var childrenOrder = new List<string>();
        
        foreach (var child in currentTree.Children)
        {
            if (entityMap.Any(k => k.Key.Matches(child.Name)))
            {
                childrenSqlStatement.Add(child.Name, GenerateQuery(trees, entityMap, childrenFields, sqlWhereStatement, child, isRootEntity + 1).sqlStatement);
            }
            childrenOrder.Add(child.Name);
        }
        
        var tableFields = entityMap[currentTree.Name];
        
        return GenerateEntityQuery(trees, currentTree, tableFields, childrenFields, sqlWhereStatement, childrenSqlStatement, isRootEntity, childrenOrder);
    }

    private static (string sqlStatement, NodeTree rootNode) GenerateEntityQuery(Dictionary<string, NodeTree> trees, NodeTree currentTree, List<GraphElement> tableFields, List<string> childrenFields, 
        Dictionary<string, string> sqlWhereStatement, Dictionary<string,string> childrenSqlStatement, int isRootEntity, List<string> childrenOrder)
    {
        var sqlChildren = string.Empty;
        var sqlQueryStatement = $" {(isRootEntity == 1 ? "" : tableFields.Any(f => f.GraphElementType == GraphElementType.Edge) ? " JOIN ( " : " LEFT JOIN  ( ")} SELECT ";
        var childrenFieldAux = new List<string>();
        
        foreach (var tableField in childrenFields)
        {
            var nodeTreeName = tableField.Split('.')[0];
            if (nodeTreeName.Matches(currentTree.Name) || currentTree.Children.Any(c => c.Name.Matches(nodeTreeName)))
            {
                var fieldName = $"{currentTree.Name}.\"{tableField.Split(".")[1].Sanitize().Split("AS")[0]
                    .Sanitize()}\" AS \"{tableField.Split(".")[1].Sanitize().Split("AS")[0].Sanitize()}\"";
                childrenFieldAux.Add(fieldName);
            }
        }

        var joinKeyToAdd = tableFields.Any(f => f.FieldName.Matches(currentTree.JoinKey));

        if (tableFields.Count <= 2 && !tableFields.Any(f => !f.Field.Contains("Id")))
        {
            var newRootNodeTree = trees[childrenSqlStatement.First().Key];
            sqlWhereStatement.TryGetValue(newRootNodeTree.Name, out var currentSqlWhereStatementNewRoot);
            var oldWhereStatement = currentSqlWhereStatementNewRoot;
            
            if (!string.IsNullOrEmpty(oldWhereStatement))
            {
                oldWhereStatement = oldWhereStatement.Replace("~", newRootNodeTree.Name);

                foreach (var field in oldWhereStatement.Split("\""))
                {
                    if (newRootNodeTree.Mappings.ContainsKey(field))
                    {
                        oldWhereStatement =
                            oldWhereStatement.Replace(field, $"{field.ToSnakeCase(newRootNodeTree.Id)}");
                    }
                }
                oldWhereStatement = $" WHERE {oldWhereStatement} ";
            }
            else
            {
                oldWhereStatement = string.Empty;
            }
            
            if (childrenSqlStatement.Count > 0 && childrenSqlStatement.Count > 0)
            {
                var cutoff = childrenSqlStatement.First().Value.IndexOf('(') + 1;
                var sqlStatement =
                    $"{childrenSqlStatement.First().Value.Substring(cutoff, childrenSqlStatement.First()
                        .Value.Length - cutoff)}";
                sqlStatement =  sqlStatement.Replace(oldWhereStatement, $" WHERE {currentSqlWhereStatementNewRoot.Replace("~", newRootNodeTree.Name)}");
                
                return (sqlStatement, newRootNodeTree);
            }
            return (string.Empty, currentTree);
        }
        
        if (!joinKeyToAdd && !string.IsNullOrEmpty(currentTree.JoinKey))
        {
            tableFields.Add(
                new GraphElement()
                {
                    EntityId = currentTree.Id,
                    GraphElementType = tableFields.Any(f => f.TableName.Matches(currentTree.Name) &&
                        f.GraphElementType == GraphElementType.Edge) ? GraphElementType.Edge : GraphElementType.Node,
                    TableName = currentTree.Name,
                    FieldName = currentTree.JoinKey,
                    Field = $"{currentTree.Name}.\"{currentTree.JoinKey.ToUpperCamelCase()}\" AS \"{currentTree.JoinKey.ToUpperCamelCase().ToSnakeCase(currentTree.Id)}\""
                });
        }
        
        var idToAdd = tableFields.Any(f => f.FieldName.Matches("Id"));
        
        if (!idToAdd)
        {
            tableFields.Add(
                new GraphElement()
                {
                    EntityId = currentTree.Id,
                    GraphElementType = tableFields.Any(f => f.TableName.Matches(currentTree.Name) &&
                                                            f.GraphElementType == GraphElementType.Edge) ? GraphElementType.Edge : GraphElementType.Node,
                    TableName = currentTree.Name,
                    FieldName = "Id",
                    Field = $"{currentTree.Name}.\"Id\" AS \"{"Id".ToSnakeCase(currentTree.Id)}\""
                });
        }
        
        foreach (var tableField in tableFields)
        {
            var nodeTreeName = tableField.Field.Split('.')[0];
            
            if (nodeTreeName.Matches(currentTree.Name) || currentTree.Children.Any(c => c.Name.Matches(nodeTreeName)))
            {
                var tableFieldParts = tableField.Field.Split(".");
                var childField = tableFieldParts[1].Sanitize().Split(" AS ")[1];
                var fieldName = $"{currentTree.Name}.\"{childField}\" AS \"{childField}\"";
                childrenFieldAux.Add(fieldName);
            }
        }
        
        var childrenColumns = string.Join(",", childrenFields.Where(c=> currentTree.Children.Any(cc => cc.Name
            .Matches(c.Split('.')[0].Sanitize()))));
        
        sqlQueryStatement += string.Join(",", tableFields.Select(s => s.Field)) + $"{(!string.IsNullOrEmpty(childrenColumns) ? "," : "")}" + childrenColumns;
        sqlQueryStatement += $" FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name}";

        childrenFields.AddRange(childrenFieldAux);
        
        for (var i = 0; i < childrenSqlStatement.Count; i++)
        {
            var childTree = trees[childrenOrder[i]];
            if (childrenSqlStatement.TryGetValue(childTree.Name, out var childStatement))
            {
                sqlChildren +=
                    $" {childStatement} ) {childTree.Name} ON {currentTree.Name}.\"{"Id"}\" = {childTree.Name}.\"{childTree.JoinKey.ToSnakeCase(childTree.Id)}\"";
            }
        }
        sqlQueryStatement += $" {sqlChildren}";
        
        sqlWhereStatement.TryGetValue(currentTree.Name, out var currentSqlWhereStatement);
        
        if (!string.IsNullOrEmpty(currentSqlWhereStatement))
        {
            currentSqlWhereStatement = currentSqlWhereStatement.Replace("~", currentTree.Name);

            foreach (var field in currentSqlWhereStatement.Split("\""))
            {
                if (currentTree.Mappings.ContainsKey(field))
                {
                    currentSqlWhereStatement =
                        currentSqlWhereStatement.Replace(field, $"{(isRootEntity == 1 ? field : field.ToSnakeCase(currentTree.Id))}");
                }
            }
            currentSqlWhereStatement = $" WHERE {currentSqlWhereStatement} ";
        }
        else
        {
            currentSqlWhereStatement = string.Empty;
        }
        
        sqlQueryStatement += $" {currentSqlWhereStatement}";
        
        return (sqlQueryStatement, currentTree);
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

    public static void GetFields(Dictionary<string, NodeTree> trees, ISyntaxNode node, NodeTree currentTree, NodeTree parentTree,
        List<string> entityNames, Dictionary<string, List<GraphElement>> entityMap, bool isEdge)
    {
        if (node.GetNodes().Count() == 0)
        {
            if (string.IsNullOrEmpty(currentTree.Name))
            {
                currentTree = trees.Last().Value;    
            }

            var dbField = string.Empty;
            
            if (node.ToString().Matches("nodes") || node.ToString().Matches("node"))
            {
                dbField = node.ToString();
            }
            else if (entityNames.Any(e => e.Matches(node.ToString())))
            {
                dbField = currentTree.Name;
            }
            else
            {
                dbField = currentTree.Mappings[node.ToString()];    
            }
            
            var element = new GraphElement
            {
                EntityId = currentTree.Id,
                GraphElementType = isEdge ? GraphElementType.Edge : GraphElementType.Node,
                TableName = currentTree.Name
            };
            
            if (currentTree.Name.Matches(dbField))
            {
                element.FieldName = dbField;
                element.Field = $"{currentTree.Name}.\"Id\" AS \"{dbField.ToUpperCamelCase()}_{"Id".ToSnakeCase(currentTree.Id)}\"";
            }
            else if (string.IsNullOrEmpty(currentTree.Name) || dbField.Matches("nodes") || dbField.Matches("node"))
            {
                element.FieldName = currentTree.Name;
                element.Field = $"{currentTree.Name}.\"Id\" AS \"{currentTree.Name.ToUpperCamelCase()}_{"Id".ToSnakeCase(currentTree.Id)}\"";
            }
            else
            {
                if (!dbField.Matches(currentTree.Name))
                {
                    element.FieldName = dbField;
                    element.Field = $"{currentTree.Name}.\"{dbField.ToUpperCamelCase()}\" AS \"{dbField.ToUpperCamelCase().ToSnakeCase(currentTree.Id)}\"";
                }
            }
            
            AddToGraphElementDictionary(entityMap, currentTree.Name, element); 
            
            return;
        }
        
        foreach (var childNode in node.GetNodes())
        {
            if (entityNames.Any(e => e.Matches(childNode.ToString().Split('{')[0])))
            {
                currentTree = trees[childNode.ToString().Split('{')[0]];
                if (!string.IsNullOrWhiteSpace(currentTree.ParentName))
                {
                    parentTree = trees[currentTree.ParentName];    
                }
                else
                {
                    parentTree = currentTree;
                }
            }
            
            GetFields(trees, childNode, currentTree, parentTree, entityNames, entityMap, isEdge);
        }
    }
    
    private static void AddToGraphElementDictionary(Dictionary<string, List<GraphElement>> dictionary, string key, GraphElement value)
    {
        if (!dictionary.TryGetValue(key, out var currentList))
        {
            var newGraphList = new List<GraphElement>()
            {
                value
            };
            dictionary.Add(key, newGraphList);
        }
        else
        {
            var currentFieldIndex = currentList.FindIndex(f => f.Field.Matches(value.Field));

            if (currentFieldIndex < 0)
            {
                currentList.Add(value);
            }
            else
            {
                currentList[currentFieldIndex] = value;
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
        if (whereNode == null)
        {
            return;
        }
        
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