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
        IModelTreeMap<D,S> modelTreeMap, string wrapperName,
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
        var rootEntityName = modelTreeMap.ModelNames.Last();

        //Where conditions
        GetFieldsWhere(modelTreeMap.DictionaryTree, entityTreeMap.LinkEntityDictionaryTree, whereFields, sqlWhereStatement, graphQlSelection.SyntaxNode.Arguments
                .FirstOrDefault(a => a.Name.Value.Matches("where")), modelTreeMap.DictionaryTree.Last().Value.Name, rootEntityName,
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
                    entityTreeMap.LinkEntityDictionaryTree, sqlUpsertStatementNodes, modelTreeMap.DictionaryTree.First(t => 
                    t.Key.Matches(rootEntityName)).Value, string.Empty,
                    new NodeTree(), models, modelTreeMap.EntityNames, visitedModels);

                sqlUpsertStatement = SqlHelper.GenerateUpsertStatements(entityTreeMap.LinkEntityDictionaryTree, entityTreeMap.DictionaryTree, 
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
        
        if (graphQlSelection.SelectionSet?.Selections!
                .FirstOrDefault(s => s.ToString().StartsWith("edges")) != null)
        {
            GetFields(modelTreeMap.DictionaryTree, edgeNode,
                entityTreeMap.LinkEntityDictionaryTree,
                sqlStatementNodes,
                modelTreeMap.DictionaryTree.First(t => 
                    t.Key.Matches(rootEntityName)).Value,
                new NodeTree(), models, modelTreeMap.EntityNames, true);
        }

        if (graphQlSelection.SelectionSet?.Selections!
                .FirstOrDefault(s => s.ToString().StartsWith("nodes")) != null)
        {
            GetFields(modelTreeMap.DictionaryTree, node,
                entityTreeMap.LinkEntityDictionaryTree,
                sqlStatementNodes,
                modelTreeMap.DictionaryTree.First(t => 
                    t.Key.Matches(rootEntityName)).Value,
                new NodeTree(), models, modelTreeMap.EntityNames, false);
        }

        if (string.IsNullOrEmpty(sqlSelectStatement))
        {
            var childrenSqlStatement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var setQuery = GenerateQuery(entityTreeMap.DictionaryTree, 
                sqlStatementNodes, [], entityTreeMap.EntityNames, sqlWhereStatement, rootNodeTree,
                childrenSqlStatement, rootEntityName);
            sqlSelectStatement = setQuery.sqlStatement;
            rootNodeTree = setQuery.rootNodeTree;
            
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
    private static (string sqlStatement, NodeTree rootNodeTree) 
        GenerateQuery(Dictionary<string, NodeTree> entityTrees, 
        Dictionary<string, SqlNode> sqlStatementNodes, List<string> childrenFields, List<string> entities, 
        Dictionary<string, string> sqlWhereStatement, NodeTree currentTree,
        Dictionary<string, string> childrenSqlStatement, string rootEntityName)
    {
        var childrenOrder = new List<string>();

        foreach (var child in currentTree.Children)
        {
            if (currentTree.ChildrenNames.Any(k => k.Matches(child.Name)))
            {
                var childrenSqlStatementAux = GenerateQuery(entityTrees, sqlStatementNodes, childrenFields, 
                        entities, sqlWhereStatement, 
                        child, childrenSqlStatement, rootEntityName)
                    .sqlStatement;

                if (childrenSqlStatement.TryGetValue(child.Name, out var previousChildrenSqlStatement))
                {
                    previousChildrenSqlStatement += " " +  childrenSqlStatementAux;
                    childrenSqlStatement[child.Name] = (previousChildrenSqlStatement);
                }
                else if (!string.IsNullOrEmpty(childrenSqlStatementAux))
                {
                    childrenSqlStatement.Add(child.Name, childrenSqlStatementAux);
                }
            }

            childrenOrder.Add(child.Name);
        }
        
        return GenerateEntityQuery(entityTrees, sqlStatementNodes, currentTree, 
            childrenFields, entities, sqlWhereStatement, childrenSqlStatement, rootEntityName, childrenOrder);
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
    private static (string sqlStatement, NodeTree rootNode) GenerateEntityQuery(Dictionary<string, NodeTree> entityTrees,
        Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree, 
        List<string> childrenFields, List<string> entities, Dictionary<string, string> sqlWhereStatement, 
        Dictionary<string, string> childrenSqlStatement, string rootEntityName, List<string> childrenOrder)
    {
        var sqlChildren = string.Empty;
        var currentColumns = sqlStatementNodes
            .Where(k => k.Key.Split('~')[0].Sanitize().Matches(currentTree.Name)).ToList();
        var sqlQueryStatement =
            $" {(rootEntityName.Matches(currentTree.Name) ? "" : currentColumns
                .Any(f => f.Value.SqlNodeType == SqlNodeType.Edge) ? " JOIN ( " : " LEFT JOIN  ( ")} SELECT ";
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

            if (childrenSqlStatement.Count > 0 && childrenSqlStatement.Count > 0 &&
                !string.IsNullOrEmpty(currentSqlWhereStatementNewRoot))
            {
                var cutoff = childrenSqlStatement.First().Value.IndexOf('(') + 1;
                var sqlStatement =
                    $"{childrenSqlStatement.First().Value.Substring(cutoff, childrenSqlStatement.First()
                        .Value.Length - cutoff)}";
                sqlStatement = sqlStatement.Replace(oldWhereStatement,
                    $" WHERE {currentSqlWhereStatementNewRoot.Replace("~", newRootNodeTree.Name)}");

                return (sqlStatement, newRootNodeTree);
            }

            return (string.Empty, currentTree);
        }

        foreach (var tableColumn in currentColumns)
        {
            var nodeTreeName = tableColumn.Key.Split('~')[0];

            if (nodeTreeName.Matches(currentTree.Name) || currentTree.Children.Any(c => c.Name.Matches(nodeTreeName)))
            {
                var tableFieldParts = tableColumn.Key.Split("~");
                var fieldName = $"{currentTree.Name}.\"{tableFieldParts[1]}\" AS \"{tableFieldParts[1]}\"";
                childrenFieldAux.Add(fieldName);
            }
        }

        var childrenColumns = string.Join(",", childrenFields.Where(c => currentTree.Children.Any(cc => cc.Name
            .Matches(c.Split('.')[0].Sanitize()))));

        sqlQueryStatement += string.Join(",", currentColumns.Select(s => s.Value.Column)) +
                             $"{(!string.IsNullOrEmpty(childrenColumns) ? "," : "")}" + childrenColumns;
        sqlQueryStatement += $" FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name}";

        childrenFields.AddRange(childrenFieldAux);

        for (var i = 0; childrenOrder.Count > 0 && i < childrenSqlStatement.Count; i++)
        {
            var childTree = entityTrees[childrenOrder[i]];
            if (childrenSqlStatement.TryGetValue(childTree.Name, out var childStatement))
            {
                // foreach (var joinKey in childTree.JoinKey)
                // {
                    sqlChildren +=
                        $" {childStatement} ) {childTree.Name} ON {currentTree.Name}.\"{"Id"}\" = {childTree.Name}.\"{currentTree.Name}{"Id"}\"";
                // }
            }
        }

        sqlQueryStatement += $" {sqlChildren}";

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

        sqlQueryStatement += $" {currentSqlWhereStatement}";

        return (sqlQueryStatement, currentTree);
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
        Dictionary<string,SqlNode> linkEntityDictionaryTree, Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree,
        string previousNode, NodeTree parentTree, List<string> modelNames, List<string> entities, List<string> visitedModels)
    {
        if (node != null && node.GetNodes()?.Count() == 0)
        {
            var currentModel = visitedModels.LastOrDefault();

            if (currentModel == "Contract")
            {
                var a = false;
            }
            
            if (linkEntityDictionaryTree.TryGetValue($"{currentTree.Name}~{node.ToString()}", out var sqlNodeFrom) ||
                linkEntityDictionaryTree.TryGetValue($"{currentModel}~{node.ToString()}", out sqlNodeFrom))
            {
                if (linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey, out var sqlNodeTo))
                {
                    sqlNodeTo.SqlNodeType = SqlNodeType.Mutation;

                    if (previousNode.Split(':').Length == 2)
                    {
                        if (sqlNodeFrom.FromEnumeration.TryGetValue(previousNode.Split(':')[1].Replace("_", ""),
                                out var enumValue))
                        {
                            var toEnum = sqlNodeTo.ToEnumeration.FirstOrDefault(e => 
                                e.Value.Matches(enumValue)).Value;
                            sqlNodeTo.Value =  toEnum;
                        }
                        else
                        {
                            sqlNodeTo.Value = previousNode.Split(':')[1];
                        }
                    }
                    
                    AddEntity(linkEntityDictionaryTree, sqlStatementNodes, entities,
                        sqlNodeTo);

                    if (!visitedModels.Contains(currentTree.Name))
                    {
                        visitedModels.Add(currentTree.Name);
                    }
                }
            }
            
            return;
        }

        if (node == null)
        {
            return;
        }
        
        foreach (var childNode in node.GetNodes())
        {
            if (modelNames.Any(e => e.Matches(childNode.ToString().Split('{')[0])) || node.ToString().Matches("nodes") || 
                node.ToString().Matches("node"))
            {
                if (node.ToString().Matches("nodes") || 
                    node.ToString().Matches("node"))
                {
                    currentTree = trees[modelNames.Last()];
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
            
            GetMutations(trees, childNode, linkEntityDictionaryTree, sqlStatementNodes, currentTree, node.ToString(), 
                parentTree, modelNames, entities, visitedModels);
        }
    }

    private static void AddEntity(Dictionary<string,SqlNode> linkEntityDictionaryTree,
        Dictionary<string,SqlNode> sqlStatementNodes, List<string> entities, SqlNode? sqlNode)
    {
        foreach (var entity in linkEntityDictionaryTree
                     .Where(v => sqlNode.Column.Matches(v.Value.Column)))
        {
            if (linkEntityDictionaryTree.ContainsKey(entity.Key) &&
                entities.Contains(entity.Key.Split("~")[0]))
            {
                sqlStatementNodes[entity.Key] = sqlNode;    
            }
                
            if (!linkEntityDictionaryTree.ContainsKey(entity.Key) &&
                entities.Contains(entity.Key.Split("~")[0]))
            {
                sqlStatementNodes.Add(entity.Key, sqlNode);
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
        Dictionary<string,SqlNode> linkEntityDictionaryTree, Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree,
        NodeTree parentTree, List<string> modelNames, List<string> entityNames, bool isEdge)
    {
        if (node != null && node.GetNodes()?.Count() == 0)
        {
            if (linkEntityDictionaryTree.TryGetValue($"{currentTree.Name}~{node.ToString()}", out var sqlNodeFrom))
            {
                if (!sqlStatementNodes.ContainsKey(sqlNodeFrom.RelationshipKey) &&
                    linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey, out var sqlNodeTo))
                {
                    sqlNodeTo.SqlNodeType = isEdge ? SqlNodeType.Edge : SqlNodeType.Node;
                    sqlStatementNodes.Add(sqlNodeFrom.RelationshipKey, sqlNodeTo);
                }
            }
            
            return;
        }

        if (node == null)
        {
            return;
        }
        
        foreach (var childNode in node.GetNodes())
        {
            if (modelNames.Any(e => e.Matches(childNode.ToString().Split('{')[0])) || node.ToString().Matches("nodes") || 
                node.ToString().Matches("node"))
            {
                if (node.ToString().Matches("nodes") || 
                    node.ToString().Matches("node"))
                {
                    currentTree = trees[modelNames.Last()];
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
            
            GetFields(trees, childNode, linkEntityDictionaryTree, sqlStatementNodes, currentTree, 
                parentTree, modelNames, entityNames, isEdge);
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
    public static void GetFieldsWhere(Dictionary<string, NodeTree> trees, Dictionary<string, SqlNode> linkEntityDictionaryTree, List<string> whereFields,
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
                                var clause = SqlGraphQLHelper.ProcessFilter(currentNodeTree, linkEntityDictionaryTree, 
                                    field, "=", clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, currentEntity, clause);
                                break;
                            }
                            case "neq":
                            {
                                var clause = SqlGraphQLHelper.ProcessFilter(currentNodeTree, linkEntityDictionaryTree, 
                                    field, "<>", clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, currentEntity, clause);
                                break;
                            }
                            case "in":
                            {
                                clauseValue = "(" + string.Join(',',
                                    column[1].Replace("[", "").Replace("]", "").Split(',')
                                        .Select(v => $"'{v.Trim()}'")) + ")";
                                var clause = SqlGraphQLHelper.ProcessFilter(currentNodeTree, linkEntityDictionaryTree, 
                                    field, "in", clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, currentEntity, clause);
                                break;
                            }
                        }
                    }
                }
            }

            GetFieldsWhere(trees, linkEntityDictionaryTree, whereFields, sqlWhereStatement, wNode, currentEntity, rootEntityName, clauseType,
                permission);
        }
    }
}