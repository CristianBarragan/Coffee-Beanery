using System.Text;
using Dapper;
using CoffeeBeanery.GraphQL.Extension;
using HotChocolate.Language;
using CoffeeBeanery.GraphQL.Model;
using FASTER.core;
using HotChocolate.Execution.Processing;
using MoreLinq.Extensions;

namespace CoffeeBeanery.GraphQL.Helper;

public static class SqlNodeResolverHelper
{
    /// <summary>
    /// Method to handle three Selection using recursion to visit each argument and nodes
    /// </summary>
    /// <param name="graphQlSelection"></param>
    /// <param name="trees"></param>
    /// <param name="rootEntityName"></param>
    /// <param name="entityNames"></param>
    /// <param name="cache"></param>
    /// <param name="permissions"></param>
    /// <returns></returns>
    public static SqlStructure HandleGraphQL<D,S>(ISelection graphQlSelection, IEntityTreeMap<D,S> entityTreeMap, 
        IModelTreeMap<D,S> modelTreeMap, string rootEntityName, string wrapperName,
        IFasterKV<string, string> cache, string cacheKey,
        Dictionary<string, List<string>> permissions = null)
        where D : class where S : class
    {
        var sqlWhereStatement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sqlOrderStatement = string.Empty;
        var parameters = new DynamicParameters();
        var whereFields = new List<string>();
        var pagination = new Pagination();
        var hasPagination = false;
        var hasSorting = false;
        var isCached = false;
        var sqlQuery = new StringBuilder();
        var sqlSelectStatement = string.Empty;
        var sqlUpsertStatement = string.Empty;
        var models = modelTreeMap.ModelNames;

        //Where conditions
        GetFieldsWhere(modelTreeMap.DictionaryTree, modelTreeMap.LinkDictionaryTree, modelTreeMap.LinkDictionaryTree, 
            whereFields, sqlWhereStatement, graphQlSelection.SyntaxNode.Arguments
                .FirstOrDefault(a => a.Name.Value.Matches("where")), 
            modelTreeMap.DictionaryTree.Last().Value.Name, rootEntityName,
            Entity.ClauseTypes, permissions);

        //Arguments
        foreach (var argument in graphQlSelection.SyntaxNode.Arguments.Where(a => !a.Name.Value.Matches("where")))
        {
            switch (argument.Name.ToString())
            {
                case "first":
                    pagination.First = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                        ? 0
                        : int.Parse(argument.Value?.Value.ToString());
                    hasPagination = true;
                    break;
                case "last":
                    pagination.Last = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                        ? 0
                        : int.Parse(argument.Value?.Value.ToString());
                    hasPagination = true;
                    break;
                case "before":
                    pagination.Before = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                        ? ""
                        : argument.Value?.Value.ToString();
                    hasPagination = true;
                    break;
                case "after":
                    pagination.After = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                        ? ""
                        : argument.Value?.Value.ToString();
                    hasPagination = true;
                    break;
            }

            if (argument.Name.ToString().Contains("order"))
            {
                foreach (var orderNode in argument.GetNodes())
                {
                    hasSorting = true;
                    sqlOrderStatement = GetFieldsOrdering(modelTreeMap.DictionaryTree, orderNode, rootEntityName);
                }
            }

            var sqlUpsertStatementNodes = new Dictionary<string,SqlNode>();
            var visitedModels = new List<string>();
            
            if (argument.Name.Value.Matches(wrapperName))
            {
                var nodeTreeRoot = new NodeTree();
                nodeTreeRoot.Name = string.Empty;
                
                GetMutations(modelTreeMap.DictionaryTree, argument.Value.GetNodes().ToList()[0],
                    entityTreeMap.LinkDictionaryTree, modelTreeMap.LinkDictionaryTree, 
                    sqlUpsertStatementNodes, modelTreeMap.DictionaryTree.First(t => 
                    t.Key.Matches(rootEntityName)).Value, string.Empty,
                    new NodeTree(), models, modelTreeMap.EntityNames, visitedModels);

                sqlUpsertStatement = SqlHelper.GenerateUpsertStatements(entityTreeMap.LinkDictionaryTree, entityTreeMap.DictionaryTree, 
                    sqlUpsertStatementNodes, modelTreeMap.EntityNames, sqlWhereStatement);
            }
        }

        // Query Select
        var level = 1;
        var rootNodeTree = new NodeTree();
        
        //Generate cache level 1
        var edgeNode = graphQlSelection.SelectionSet?.Selections!
            .FirstOrDefault(s => s.ToString().StartsWith("edges"));
        var node = graphQlSelection.SelectionSet?.Selections!
            .FirstOrDefault(s => s.ToString().StartsWith("nodes"));

        //Read cache
        // using var cacheReadSession = cache.NewSession(new SimpleFunctions<string, string>());
        // cacheReadSession.Read(ref cacheKey, ref sqlSelectStatement);
        rootNodeTree = modelTreeMap.DictionaryTree.Last().Value;
        var sqlStatementNodes = new Dictionary<string,SqlNode>();
        var visitedFieldModel = new List<string>();
        
        if (graphQlSelection.SelectionSet?.Selections!
                .FirstOrDefault(s => s.ToString().StartsWith("edges")) != null)
        {
            GetFields(modelTreeMap.DictionaryTree, edgeNode.GetNodes().ToList()[1].GetNodes().ToList()[0],
                entityTreeMap.LinkDictionaryTree, modelTreeMap.LinkDictionaryTree,
                sqlStatementNodes,
                modelTreeMap.DictionaryTree.First(t => 
                    t.Key.Matches(rootEntityName)).Value,
                new NodeTree(), visitedFieldModel, models, modelTreeMap.EntityNames, true);
        }

        if (graphQlSelection.SelectionSet?.Selections!
                .FirstOrDefault(s => s.ToString().StartsWith("nodes")) != null)
        {
            GetFields(modelTreeMap.DictionaryTree, node,
                entityTreeMap.LinkDictionaryTree, modelTreeMap.LinkDictionaryTree,
                sqlStatementNodes,
                modelTreeMap.DictionaryTree.First(t => 
                    t.Key.Matches(rootEntityName)).Value,
                new NodeTree(), visitedFieldModel, models, modelTreeMap.EntityNames, false);
        }

        var sqlQueryStatement = new StringBuilder();
        var sqlQueryStructures = new Dictionary<string, SqlQueryStructure>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(sqlSelectStatement))
        {
            var childrenSqlStatement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            GenerateQuery(entityTreeMap.DictionaryTree, entityTreeMap.LinkDictionaryTree, sqlQueryStatement,
                sqlStatementNodes, [], entityTreeMap.NodeTree.Children[0],
                childrenSqlStatement, rootEntityName, sqlQueryStructures);
            sqlSelectStatement = sqlQueryStatement.ToString();
            
            //Update cache
            // if (!isCached)
            // {
            //     cacheReadSession.Upsert(ref cacheKey, ref sqlStament);    
            // }
        }
        else
        {
            rootNodeTree = entityTreeMap.DictionaryTree.Last().Value;
        }
        
        if (string.IsNullOrEmpty(sqlSelectStatement))
        {
            return default;
        }
        
        sqlQuery.Append(sqlSelectStatement);

        var splitOn = sqlSelectStatement.Split(" FROM ")[0].Split(',').Where(c => c.Split(" AS ")[1]
            .Contains("_Id_") && c.Split(" AS ")[1].Replace("_", "").Sanitize()
            .Length > 2).Select(c => c.Split(" AS ")[1].Sanitize()).ToList();

        var hasTotalCount = false;

        if (hasPagination || hasSorting)
        {
            // Query Where, Sort, and Pagination
            SqlHelper.HandleQueryClause(rootNodeTree, sqlQuery,
                sqlOrderStatement, pagination, hasTotalCount);    
        }

        var sqlStructure = new SqlStructure()
        {
            SqlQuery = sqlQuery.ToString(),
            Parameters = parameters,
            SqlUpsert = sqlUpsertStatement,
            SplitOnDapper = splitOn,
            Pagination = pagination,
            HasTotalCount = false
        };

        return sqlStructure;
    }
    
    /// Recursive method to visit every entity that needs to be added into the SQL query statement
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="entityMap"></param>
    /// <param name="childrenFields"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <param name="currentTree"></param>
    /// <param name="isRootEntity"></param>
    /// <returns></returns>
    private static void 
        GenerateQuery(Dictionary<string, NodeTree> entityTrees, Dictionary<string,SqlNode> linkEntityDictionaryTree, 
            StringBuilder sqlQueryStatement, Dictionary<string, SqlNode> sqlStatementNodes, Dictionary<string, string> sqlWhereStatement,
            NodeTree currentTree, Dictionary<string, string> childrenSqlStatement, string rootEntityName,
            Dictionary<string, SqlQueryStructure> sqlQueryStructures)
    {
        var childrenOrder = new List<string>();
        
        sqlStatementNodes.Add($"{currentTree.Name}~Id", new SqlNode()
        {
            Column = "Id"
        });

        foreach (var child in currentTree.Children)
        {
            if (currentTree.ChildrenName.Any(k => k.Matches(child.Name)))
            {
                GenerateQuery(entityTrees, linkEntityDictionaryTree, sqlQueryStatement, sqlStatementNodes, sqlWhereStatement,
                    child, childrenSqlStatement, rootEntityName, sqlQueryStructures);
            }
            childrenOrder.Add(child.Name);
        }

        var currentEntityQuery = GenerateEntityQuery(entityTrees, linkEntityDictionaryTree, sqlStatementNodes, currentTree,
            sqlQueryStatement, sqlQueryStructures, sqlWhereStatement, childrenSqlStatement, rootEntityName);

        var queryBuilder = $"SELECT % FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name} ";
        // queryBuilder = currentEntityQuery.Query;

        var selectChildren = string.Empty;
        
        foreach (var child in currentTree.Children)
        {
            if (!string.IsNullOrEmpty(child.Name) && sqlQueryStructures.ContainsKey(child.Name))
            {
                var childStructure = sqlQueryStructures[child.Name];
                if (childStructure.SqlNode.JoinKeys.Count > 0)
                {
                    queryBuilder += childStructure.SqlNodeType == SqlNodeType.Edge ? " JOIN " : " LEFT JOIN ";
                    queryBuilder +=
                        $" ( {childStructure.Query} ) {child.Name} ON {currentTree.Name}.{( childStructure.SqlNode.JoinKeys.Count > 0 ? 
                            "\"" + childStructure.SqlNode.JoinKeys[0].To.Split('~')[1] + "\"" : "" )} = { 
                            child.Name}.{ (childStructure.SqlNode.JoinKeys.Count > 0 ?
                                "\"" + childStructure.SqlNode.JoinKeys[0].From.Split('~')[1].ToSnakeCase(child.Id) + "\"" : "" )} ";
                
                    // var joinKey = $"{child.Name}.\"{childStructure.SqlNode.JoinKeys[0].From.Split('~')[1]}\" AS \"{
                    //     childStructure.SqlNode.JoinKeys[0].From.Split('~')[1].ToSnakeCase(child.Id)}\"";
                    // childStructure.Columns.Insert(0, $"{ child.Name}.{ (!string.IsNullOrEmpty(joinKey) ?
                    //     joinKey : "" )}");
                    // selectChildren += string.Join(",",childStructure.Columns);
                    // joinKey = $"{child.Name}.\"{childStructure.SqlNode.JoinKeys[0].To.Split('~')[1]}\" AS \"{
                    //     childStructure.SqlNode.JoinKeys[0].To.Split('~')[1]}\"";
                    // currentEntityQuery.Columns.Add($"{( !string.IsNullOrEmpty(joinKey) ? 
                    //     joinKey : "" )}");
                
                    currentEntityQuery.Columns.AddRange(childStructure.ParentColumns);
                }
            }

            
            
            // if (childStructure.SqlNode.LinkKeys.Count > 0)
            // {
            //     var linkKey = $"~.\"{childStructure.SqlNode.LinkKeys[0].From.Split('~')[1]}\" AS \"{
            //         childStructure.SqlNode.LinkKeys[0].From.Split('~')[1].ToSnakeCase(child.Id)}\"";
            //     childStructure.Columns.Insert(0, $"{ child.Name}.{ (!string.IsNullOrEmpty(linkKey) ?
            //         linkKey : "" )}");
            //     selectChildren += string.Join(",",childStructure.Columns);
            //     linkKey = $"~.\"{childStructure.SqlNode.LinkKeys[0].To.Split('~')[1]}\" AS \"{
            //         childStructure.SqlNode.LinkKeys[0].To.Split('~')[1]}\"";
            //     currentEntityQuery.Columns.Insert(0, $"{( !string.IsNullOrEmpty(linkKey) ? 
            //         linkKey : "" )}");
            //     
            //     currentEntityQuery.Columns.AddRange(childStructure.Columns);
            // }
        }

        // var key = sqlStatementNodes.Keys.FirstOrDefault(k => k.Contains(currentTree.Name));
        //
        // if (!string.IsNullOrEmpty(key))
        // {
        //     var currentStructure = sqlStatementNodes[key];
        //
        //     if (currentStructure.JoinKeys.Count > 0)
        //     {
        //         var joinKey = $"\"{currentStructure.JoinKeys[0].From.Split('~')[1]}\" AS \"{
        //             currentStructure.JoinKeys[0].From.Split('~')[1].ToSnakeCase(currentTree.Id)}\"";
        //         currentEntityQuery.Columns.Insert(0, $"~.{ (!string.IsNullOrEmpty(joinKey) ?
        //             joinKey : "" )}");
        //     }
        //
        //     if (currentStructure.LinkKeys.Count > 0)
        //     {
        //         var linkKey = $"\"{currentStructure.LinkKeys[0].From.Split('~')[1]}\" AS \"{
        //             currentStructure.LinkKeys[0].From.Split('~')[1].ToSnakeCase(currentTree.Id)}\"";
        //         currentEntityQuery.Columns.Insert(0, $"~.{ (!string.IsNullOrEmpty(linkKey) ?
        //             linkKey : "" )}");
        //     }
        // }
        
        var select = string.Join(",", currentEntityQuery.Columns);
        select = select.Replace("~", currentTree.Name);

        // if (!string.IsNullOrEmpty(selectChildren))
        // {
        //     select += ", " + selectChildren;
        // }
        
        // queryBuilder = queryBuilder.Replace("%", select.Replace("~", currentTree.Name));
        queryBuilder = queryBuilder.Replace("%", select);
        
        currentEntityQuery.Query = queryBuilder;

        if (sqlQueryStructures.TryGetValue(currentTree.Name, out var sqlQueryStructure))
        {
            sqlQueryStructures[currentTree.Name] = currentEntityQuery;
        }
        else
        {
            sqlQueryStructures.Add(currentTree.Name, currentEntityQuery);
        }
    }


    /// <summary>
    /// Map each entity node into raw query SQL statement
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="currentTree"></param>
    /// <param name="tableFields"></param>
    /// <param name="childrenFields"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <param name="childrenSqlStatement"></param>
    /// <param name="isRootEntity"></param>
    /// <param name="childrenOrder"></param>
    /// <returns></returns>
    private static SqlQueryStructure GenerateEntityQuery(Dictionary<string, NodeTree> entityTrees,
        Dictionary<string,SqlNode> linkEntityDictionaryTree, 
        Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree, StringBuilder sqlQueryStatement,
        Dictionary<string, SqlQueryStructure> sqlQueryStructures, Dictionary<string, string> sqlWhereStatement, 
        Dictionary<string, string> childrenSqlStatement, string rootEntityName)
    {
        var currentColumns = sqlStatementNodes
            .Where(k => k.Key.Split('~')[0].Matches(currentTree.Name) && !k.Key.Split('~')[1].Contains(currentTree.Name)).ToList();
        var queryBuilder = string.Empty;
        var queryColumns = new List<string>();
        var parentQueryColumns = new List<string>();
        var localQueryColumns = new List<string>();

        if (currentTree.Name.Matches("Customer"))
        {
            var a = true;
        }
        
        foreach (var tableColumn in currentColumns)
        {
            var nodeTreeName = tableColumn.Key.Split('~')[0];

            if (nodeTreeName.Matches(currentTree.Name) || currentTree.Children.Any(c => c.Name.Matches(nodeTreeName)))
            {
                var tableFieldParts = tableColumn.Key.Split('~');
                queryColumns.Add($"~.\"{tableFieldParts[1]}\" AS \"{tableFieldParts[1].ToSnakeCase(currentTree.Id)}\"");
                parentQueryColumns.Add($"{currentTree.Name}.\"{tableFieldParts[1].ToSnakeCase(currentTree.Id)}\" AS \"{tableFieldParts[1].ToSnakeCase(currentTree.Id)}\"");
                localQueryColumns.Add(tableFieldParts[1]);
            }
        }
        
        var sqlNode = sqlStatementNodes.FirstOrDefault(k => k.Key.Contains(currentTree.Name));
        
        if (!string.IsNullOrEmpty(sqlNode.Key))
        {
            var currentStructure = sqlStatementNodes[sqlNode.Key];
        
            if (currentStructure.JoinKeys.Count > 0 && !currentStructure.JoinKeys[0].From.Split('~')[1].Contains(currentTree.Name))
            {
                // var joinKey = $"\"{currentStructure.JoinKeys[0].From.Split('~')[1]}\" AS \"{
                //     currentStructure.JoinKeys[0].From.Split('~')[1].ToSnakeCase(currentTree.Id)}\"";
                // queryColumns.Insert(0, $"~.{ (!string.IsNullOrEmpty(joinKey) ?
                //     joinKey : "" )}");
                queryColumns.Add($"~.{ (!string.IsNullOrEmpty(currentStructure.JoinKeys[0].From.Split('~')[1]) ?
                    $"\"{currentStructure.JoinKeys[0].From.Split('~')[1]}\" AS \"{
                        currentStructure.JoinKeys[0].From.Split('~')[1].ToSnakeCase(currentTree.Id)}\"" : "" )}");
                parentQueryColumns.Add($"{currentTree.Name}.{ (!string.IsNullOrEmpty(currentStructure.JoinKeys[0].From.Split('~')[1]) ?
                    $"\"{currentStructure.JoinKeys[0].From.Split('~')[1].ToSnakeCase(currentTree.Id)}\" AS \"{
                        currentStructure.JoinKeys[0].From.Split('~')[1].ToSnakeCase(currentTree.Id)}\"" : "" )}");
                // localQueryColumns.Add($"{ (!string.IsNullOrEmpty(currentStructure.JoinKeys[0].From.Split('~')[1]) ?
                //     currentStructure.JoinKeys[0].From.Split('~')[1] : "" )}");
                currentColumns.Add(new KeyValuePair<string, SqlNode>(currentStructure.JoinKeys[0].To, sqlNode.Value));
            }
        
            // if (currentStructure.LinkKeys.Count > 0)
            // {
            //     // var linkKey = $"\"{currentStructure.LinkKeys[0].From.Split('~')[1]}\" AS \"{
            //     //     currentStructure.LinkKeys[0].From.Split('~')[1].ToSnakeCase(currentTree.Id)}\"";
            //     // queryColumns.Insert(0, $"~.{ (!string.IsNullOrEmpty(linkKey) ?
            //     //     linkKey : "" )}");
            //     queryColumns.Insert(0, $"~.{ (!string.IsNullOrEmpty(currentStructure.LinkKeys[0].From.Split('~')[1]) ?
            //         $"\"{currentStructure.LinkKeys[0].From.Split('~')[1].ToSnakeCase(currentTree.Id)}\" AS \"{
            //             currentStructure.LinkKeys[0].From.Split('~')[1].ToSnakeCase(currentTree.Id)}\"" : "" )}");
            //     localQueryColumns.Add($"~.{ (!string.IsNullOrEmpty(currentStructure.LinkKeys[0].From.Split('~')[1]) ?
            //         currentStructure.LinkKeys[0].From.Split('~')[1] : "" )}");
            //     currentColumns.Add(new KeyValuePair<string, SqlNode>(currentStructure.LinkKeys[0].To, sqlNode.Value));
            // }
        }
        
        // var key = sqlStatementNodes.Keys.FirstOrDefault(k => k.Contains(currentTree.Name));
        //
        // if (sqlStatementNodes.ContainsKey(key))
        // {
        //     foreach (var joinKey in sqlStatementNodes[key].JoinKeys)
        //     {
        //         
        //         var tableFieldParts = joinKey.From.Split("~");
        //         queryColumns.Add($"~.\"{tableFieldParts[1]}\" AS \"{tableFieldParts[1].ToSnakeCase(currentTree.Id)}\"");
        //         localQueryColumns.Add(tableFieldParts[1]);
        //         tableFieldParts = joinKey.To.Split("~");
        //         queryColumns.Add($"~.\"{tableFieldParts[1]}\" AS \"{tableFieldParts[1].ToSnakeCase(currentTree.Id)}\"");
        //         localQueryColumns.Add(tableFieldParts[1]);
        //     }
        //     
        //     foreach (var linkKey in sqlStatementNodes[key].LinkKeys)
        //     {
        //         var tableFieldParts = linkKey.From.Split("~");
        //         queryColumns.Add($"~.\"{tableFieldParts[1]}\" AS \"{tableFieldParts[1].ToSnakeCase(currentTree.Id)}\"");
        //         localQueryColumns.Add(tableFieldParts[1]);
        //         tableFieldParts = linkKey.To.Split("~");
        //         queryColumns.Add($"~.\"{tableFieldParts[1]}\" AS \"{tableFieldParts[1].ToSnakeCase(currentTree.Id)}\"");
        //         localQueryColumns.Add(tableFieldParts[1]);
        //     }
        // }
        
        foreach (var childQuery in sqlQueryStructures.Where(c => 
                     currentTree.Children
                     .Any(b => b.Name.Matches(c.Key))))
        {
            queryBuilder +=$" {(childQuery.Value.SqlNodeType == SqlNodeType.Edge ? " JOIN ( " : " LEFT JOIN  ( ") } {
                childQuery.Value.Query
            }";
            
            var joinKeys = childQuery.Value.SqlNode.JoinKeys.Where(j => j.From.Matches(currentTree.Name)).ToList();
            
            for (var i = 0; i < joinKeys.Count; i++)
            {
                if (i == 0)
                {
                    queryBuilder +=
                        $" ) {childQuery.Key} ON {currentTree.Name}.\"{joinKeys[i].From}\" = {
                            childQuery.Key}.\"{joinKeys[i].To}\"";
                }
                else
                {
                    queryBuilder +=
                        $" AND {currentTree.Name}.\"{joinKeys[i].From}\" = {
                            childQuery.Key}.\"{joinKeys[i].To}\"";
                }
                queryColumns.Add($"~.\"{joinKeys[i].From}\" AS \"{joinKeys[i].From}\"");
                queryColumns.Add($"~.\"{joinKeys[i].To}\" AS \"{joinKeys[i].To}\"");
                // queryColumns.Add($"~.\"{joinKeys[i].From.ToSnakeCase(currentTree.Id)}\" AS \"{joinKeys[i].From.ToSnakeCase(currentTree.Id)}\"");
                // queryColumns.Add($"~.\"{joinKeys[i].To.ToSnakeCase(currentTree.Id)}\" AS \"{joinKeys[i].To.ToSnakeCase(currentTree.Id)}\"");
                localQueryColumns.Add(joinKeys[i].From.Split("~")[1]);
                localQueryColumns.Add(joinKeys[i].To.Split("~")[1]);
                currentColumns.Add(new KeyValuePair<string, SqlNode>(joinKeys[i].To, childQuery.Value.SqlNode));
                currentColumns.Add(new KeyValuePair<string, SqlNode>(joinKeys[i].From, childQuery.Value.SqlNode));
            }
            
            

            if (currentColumns.Count > 0)
            {
                var linkKeys = currentColumns[0].Value.JoinKeys.Where(k => k.To.Matches(childQuery.Key)).ToList();
                
                if (linkKeys.Count == 0)
                {
                    for (var i = 0; i < linkKeys.Count; i++)
                    {
                        if (i == 0)
                        {
                            queryBuilder +=
                                $" ) {childQuery.Key} ON {currentTree.Name}.\"{linkKeys[i].From}\" = {
                                    childQuery.Key}.\"{linkKeys[i].To}\"";
                        }
                        else
                        {
                            queryBuilder +=
                                $" AND {currentTree.Name}.\"{linkKeys[i].From}\" = {
                                    childQuery.Key}.\"{linkKeys[i].To}\"";
                        }
                        // queryColumns.Add($"~.\"{linkKeys[i].From.ToSnakeCase(currentTree.Id)}\" AS \"{linkKeys[i].From.ToSnakeCase(currentTree.Id)}\"");
                        // queryColumns.Add($"~.\"{linkKeys[i].To.ToSnakeCase(currentTree.Id)}\" AS \"{linkKeys[i].To.ToSnakeCase(currentTree.Id)}\"");
                        // queryColumns.Add($"~.\"{linkKeys[i].From}\" AS \"{linkKeys[i].From}\"");
                        // queryColumns.Add($"~.\"{linkKeys[i].To}\" AS \"{linkKeys[i].To}\"");
                        // localQueryColumns.Add(linkKeys[i].From.Split("~")[1]);
                        // localQueryColumns.Add(linkKeys[i].To.Split("~")[1]);
                        // currentColumns.Add(new KeyValuePair<string, SqlNode>(linkKeys[i].To, childQuery.Value.SqlNode));
                        // currentColumns.Add(new KeyValuePair<string, SqlNode>(linkKeys[i].From, childQuery.Value.SqlNode));
                    }
                }
            }
        }

        if (currentColumns.Count <= 2 && childrenSqlStatement.Count > 0)
        {
            var newRootNodeTree = entityTrees[childrenSqlStatement.First().Key];
            sqlWhereStatement.TryGetValue(newRootNodeTree.Name, out var currentSqlWhereStatementNewRoot);
            var oldWhereStatement = currentSqlWhereStatementNewRoot;

            if (!string.IsNullOrEmpty(oldWhereStatement))
            {
                oldWhereStatement = oldWhereStatement.Replace("~", newRootNodeTree.Name);

                foreach (var field in oldWhereStatement.Split("\""))
                {
                    if (currentColumns.Any(c => c.Value.Column.Matches(field)))
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
            
            currentSqlWhereStatementNewRoot = string.IsNullOrEmpty(currentSqlWhereStatementNewRoot) ? string.Empty : currentSqlWhereStatementNewRoot;

            if (childrenSqlStatement.Count > 0 && childrenSqlStatement.Count > 0 &&
                !string.IsNullOrEmpty(currentSqlWhereStatementNewRoot))
            {
                var cutoff = childrenSqlStatement.First().Value.IndexOf('(') + 1;
                var sqlStatement =
                    $"{childrenSqlStatement.First().Value.Substring(cutoff, childrenSqlStatement.First()
                        .Value.Length - cutoff)}";
                sqlStatement = sqlStatement.Replace(oldWhereStatement,
                    $" WHERE {currentSqlWhereStatementNewRoot.Replace("~", newRootNodeTree.Name)}");
                
                sqlQueryStatement.Append(sqlStatement);
            }
        }
        else
        {
            sqlQueryStatement.Append(queryBuilder);
            queryBuilder = "";
            queryBuilder += " SELECT % ";
            queryBuilder += $" FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name}";

            sqlWhereStatement.TryGetValue(currentTree.Name, out var currentSqlWhereStatement);

            if (!string.IsNullOrEmpty(currentSqlWhereStatement))
            {
                currentSqlWhereStatement = currentSqlWhereStatement.Replace("~", currentTree.Name);

                foreach (var field in currentSqlWhereStatement.Split("\""))
                {
                    if (currentColumns.Any(c => c.Value.Column.Matches(field)))
                    {
                        currentSqlWhereStatement =
                            currentSqlWhereStatement.Replace(field,
                                $"{(currentTree.Name.Matches(rootEntityName) ? field : field.ToSnakeCase(currentTree.Id))}");
                    }
                }

                currentSqlWhereStatement = $" WHERE {currentSqlWhereStatement} ";
            }
            else
            {
                currentSqlWhereStatement = string.Empty;
            }

            queryBuilder += $" {currentSqlWhereStatement}";
            queryBuilder.Insert(0, queryBuilder);
        }
        
        var select = string.Join(",", localQueryColumns.Select(s => $"{currentTree.Name}.\"{s}\" AS \"{s.ToSnakeCase(currentTree.Id)}\""));
        // select = select.Replace("~.", "");
        queryBuilder = queryBuilder.Replace("%", select);

        var sqlStructure = new SqlQueryStructure()
        {
            Id = currentTree.Id,
            SqlNodeType = currentColumns.Count > 0 ? currentColumns[0].Value.SqlNodeType : SqlNodeType.Node,
            SqlNode = currentColumns.Count > 0 ? currentColumns[0].Value : new SqlNode(),
            Query = queryBuilder,
            Columns = queryColumns,
            ParentColumns = parentQueryColumns
        };
        
        sqlQueryStructures.Add(currentTree.Name ,sqlStructure);
        
        return sqlStructure;
    }

    /// <summary>
    /// Method for getting upsert field information used for the SQL Statement
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="node"></param>
    /// <param name="currentTree"></param>
    /// <param name="parentTree"></param>
    /// <param name="entityNames"></param>
    /// <param name="entityMap"></param>
    public static void GetMutations(Dictionary<string, NodeTree> trees, ISyntaxNode node, 
        Dictionary<string,SqlNode> linkEntityDictionaryTree, Dictionary<string,SqlNode> linkModelDictionaryTree, 
        Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree,
        string previousNode, NodeTree parentTree, List<string> models, List<string> entities, List<string> visitedModels)
    {
        if (node != null && node.GetNodes()?.Count() == 0)
        {
            var currentModel = visitedModels.LastOrDefault();
            
            if (linkModelDictionaryTree.TryGetValue($"{currentTree.Name}~{node.ToString()}", out var sqlNodeFrom) ||
                linkModelDictionaryTree.TryGetValue($"{currentModel}~{node.ToString()}", out sqlNodeFrom))
            {
                if (linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey, out var sqlNodeTo))
                {
                    sqlNodeTo.SqlNodeType = SqlNodeType.Mutation;

                    if (previousNode.Split(':').Length == 2)
                    {
                        if (sqlNodeFrom.FromEnumeration.TryGetValue(previousNode.Split(':')[1].Sanitize().Replace("_", ""),
                                out var enumValue))
                        {
                            var toEnum = sqlNodeTo.ToEnumeration.FirstOrDefault(e => 
                                e.Value.Matches(enumValue)).Value;
                            sqlNodeTo.Value =  toEnum;
                        }
                        else
                        {
                            sqlNodeTo.Value = previousNode.Split(':')[1].Sanitize();
                        }
                    }
                    
                    AddEntity(linkEntityDictionaryTree, sqlStatementNodes, models, entities,
                        sqlNodeTo);
                    
                    if (!visitedModels.Contains(currentTree.Name))
                    {
                        visitedModels.Add(currentTree.Name);
                    }
                }
                
                if (previousNode.Split(':').Length == 2)
                {
                    if (sqlNodeFrom.FromEnumeration.TryGetValue(previousNode.Split(':')[1].Sanitize().Replace("_", ""),
                            out var enumValue))
                    {
                        sqlNodeFrom.Value = enumValue;
                    }
                    else
                    {
                        sqlNodeFrom.Value = previousNode.Split(':')[1].Sanitize();
                    }
                }
                
                AddEntity(linkEntityDictionaryTree, sqlStatementNodes, models, entities,
                    sqlNodeFrom);
            }
            
            return;
        }

        if (node == null)
        {
            return;
        }
        
        foreach (var childNode in node.GetNodes())
        {
            if (models.Any(e => e.Matches(childNode.ToString().Split('{')[0])) || node.ToString().Matches("nodes") || 
                node.ToString().Matches("node"))
            {
                if (node.ToString().Matches("nodes") || 
                    node.ToString().Matches("node"))
                {
                    currentTree = trees[models.Last()];
                }
                else
                {
                    currentTree = trees[childNode.ToString().Split('{')[0]];
                }

                if (string.IsNullOrWhiteSpace(currentTree.ParentName))
                {
                    parentTree = currentTree;
                }
                else
                {
                    parentTree = trees[currentTree.ParentName];
                }
            }
            
            GetMutations(trees, childNode, linkEntityDictionaryTree, linkModelDictionaryTree, sqlStatementNodes, currentTree, node.ToString(), 
                parentTree, models, entities, visitedModels);
        }
    }

    private static void AddEntity(Dictionary<string,SqlNode> linkEntityDictionaryTree,
        Dictionary<string,SqlNode> sqlStatementNodes, List<string> models, List<string> entities, SqlNode? sqlNode)
    {
        foreach (var entity in linkEntityDictionaryTree
                    .Where(v => sqlNode.Column.Matches(v.Value.Column)))
        {
            entity.Value.Value = sqlNode.Value;
            entity.Value.SqlNodeType = SqlNodeType.Mutation;
            if (sqlStatementNodes.ContainsKey(entity.Key) &&
                entities.Contains(entity.Key.Split("~")[0]))
            {
                sqlStatementNodes[entity.Key] = entity.Value;    
            }
                
            if (!sqlStatementNodes.ContainsKey(entity.Key) &&
                entities.Contains(entity.Key.Split("~")[0]))
            {
                sqlStatementNodes.Add(entity.Key, entity.Value);
            }
        }
    } 

    /// <summary>
    /// Method for getting upsert field information used for the SQL Statement
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="node"></param>
    /// <param name="currentTree"></param>
    /// <param name="parentTree"></param>
    /// <param name="entityNames"></param>
    /// <param name="entityMap"></param>
    /// <param name="isEdge"></param>
    public static void GetFields(Dictionary<string, NodeTree> trees, ISyntaxNode node, 
        Dictionary<string,SqlNode> linkEntityDictionaryTree, Dictionary<string,SqlNode> linkModelDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree,
        NodeTree parentTree, List<string> visitedModels, List<string> models, List<string> entities, bool isEdge)
    {
        if (node != null && node.GetNodes()?.Count() == 0)
        {
            var currentModel = visitedModels.LastOrDefault();
            
            if (linkModelDictionaryTree.TryGetValue($"{currentTree.Name}~{node.ToString()}", out var sqlNodeFrom) ||
                linkModelDictionaryTree.TryGetValue($"{currentModel}~{node.ToString()}", out sqlNodeFrom))
            {
                if (linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey, out var sqlNodeTo))
                {
                    AddField(linkEntityDictionaryTree, sqlStatementNodes, models, entities,
                        sqlNodeTo, isEdge);
                    
                    if (!visitedModels.Contains(currentTree.Name))
                    {
                        visitedModels.Add(currentTree.Name);
                    }
                }
                
                AddField(linkEntityDictionaryTree, sqlStatementNodes, models, entities,
                    sqlNodeFrom, isEdge);
            }
            
            return;
        }

        if (node == null)
        {
            return;
        }
        
        foreach (var childNode in node.GetNodes())
        {
            if (models.Any(e => e.Matches(childNode.ToString().Split('{')[0])) || node.ToString().Matches("nodes") || 
                node.ToString().Matches("node"))
            {
                if (node.ToString().Matches("nodes") || 
                    node.ToString().Matches("node"))
                {
                    currentTree = trees[models.Last()];
                }
                else
                {
                    currentTree = trees[childNode.ToString().Split('{')[0]];
                }

                if (string.IsNullOrWhiteSpace(currentTree.ParentName))
                {
                    parentTree = currentTree;
                }
                else
                {
                    parentTree = trees[currentTree.ParentName];
                }
            }
            
            GetFields(trees, childNode, linkEntityDictionaryTree, linkModelDictionaryTree, sqlStatementNodes, currentTree, 
                parentTree, visitedModels, models, entities, isEdge);
        }
    }
    
    private static void AddField(Dictionary<string,SqlNode> linkEntityDictionaryTree,
        Dictionary<string,SqlNode> sqlStatementNodes, List<string> models, List<string> entities, SqlNode? sqlNode, bool isEdge)
    {
        foreach (var entity in linkEntityDictionaryTree
                     .Where(v => sqlNode.Column.Matches(v.Value.Column)))
        {
            if (sqlStatementNodes.ContainsKey(entity.Key) &&
                entities.Contains(entity.Key.Split("~")[0]) && entity.Value.SqlNodeType == SqlNodeType.Edge)
            {
                sqlStatementNodes[entity.Key] = entity.Value;    
            }
                
            if (!sqlStatementNodes.ContainsKey(entity.Key) &&
                entities.Contains(entity.Key.Split("~")[0]))
            {
                entity.Value.SqlNodeType = isEdge ? SqlNodeType.Edge : SqlNodeType.Node;
                sqlStatementNodes.Add(entity.Key, entity.Value);
            }
        }
    } 

    /// <summary>
    /// Method for adding a value into a dictionary
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="values"></param>
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

    /// <summary>
    /// Method for creating order clause based on fields
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="orderNode"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Method for creating where clause based on fields
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="whereFields"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <param name="whereNode"></param>
    /// <param name="entityName"></param>
    /// <param name="rootEntityName"></param>
    /// <param name="clauseType"></param>
    /// <param name="permission"></param>
    public static void GetFieldsWhere(Dictionary<string, NodeTree> trees, Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> linkModelDictionaryTree, List<string> whereFields, Dictionary<string, string> sqlWhereStatement,
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
                                var clause = SqlGraphQLHelper.ProcessFilter(currentNodeTree, linkEntityDictionaryTree, 
                                    linkModelDictionaryTree, field, "=", clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, currentEntity, clause);
                                break;
                            }
                            case "neq":
                            {
                                var clause = SqlGraphQLHelper.ProcessFilter(currentNodeTree, linkEntityDictionaryTree, 
                                    linkModelDictionaryTree, field, "<>", clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, currentEntity, clause);
                                break;
                            }
                            case "in":
                            {
                                clauseValue = "(" + string.Join(',',
                                    column[1].Replace("[", "").Replace("]", "").Split(',')
                                        .Select(v => $"'{v.Trim()}'")) + ")";
                                var clause = SqlGraphQLHelper.ProcessFilter(currentNodeTree, linkEntityDictionaryTree, 
                                    linkModelDictionaryTree, field, "in", clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, currentEntity, clause);
                                break;
                            }
                        }
                    }
                }
            }

            GetFieldsWhere(trees, linkEntityDictionaryTree, linkModelDictionaryTree, whereFields, sqlWhereStatement, wNode, 
                currentEntity, rootEntityName, clauseType, permission);
        }
    }
}