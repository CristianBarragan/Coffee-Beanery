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
        var entityMap = new Dictionary<string, List<GraphElement>>(StringComparer.OrdinalIgnoreCase);
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
        var models = modelTreeMap.ModelNames.Where(m => !m.Matches(wrapperName)).ToList();
        var rootEntityName = models?.Last();

        //Where conditions
        GetFieldsWhere(modelTreeMap.DictionaryTree, whereFields, sqlWhereStatement, graphQlSelection.SyntaxNode.Arguments
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

            if (argument.Name.Value.Matches(rootEntityName))
            {
                var nodeTreeRoot = new NodeTree();
                nodeTreeRoot.Name = string.Empty;
                GetMutations(modelTreeMap.DictionaryTree, argument.Value.GetNodes().ToList()[0], nodeTreeRoot, new NodeTree(), modelTreeMap.EntityNames);
                sqlUpsertStatement = GenerateUpsertStatements(modelTreeMap.DictionaryTree, modelTreeMap.EntityNames, sqlWhereStatement);
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
            var setQuery = GenerateQuery(modelTreeMap.DictionaryTree, entityTreeMap.DictionaryTree, [], modelTreeMap.ModelNames,
                modelTreeMap.EntityNames, sqlWhereStatement, rootNodeTree,
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
            HandleQueryClause(rootNodeTree, sqlQuery,
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

    private static string GenerateUpsertStatements(Dictionary<string, NodeTree> trees, List<string> entityNames,
        Dictionary<string, string> sqlWhereStatement)
    {
        var sqlUpsertStatement = string.Empty;
        
        foreach (var entity in entityNames)
        {
            var processingTree = trees[entity];
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
                entityNames.Any(e => e.Matches(processingTree.Name) &&
                processingTree.GraphElements.Any(e => processingTree.UpsertKeys.All(u => u.FieldDestinationName.Matches(e.FieldName))))
            )
            {
                sqlUpsertStatement += GenerateSelectUpsert(processingTree, trees[processingTree.ParentName],
                    whereCurrentClause);
            }
            else if (entityNames.Any(e => e.Matches(processingTree.Name)) &&
                     processingTree.GraphElements.Any(e => processingTree.UpsertKeys.All(u => u.FieldDestinationName.Matches(e.FieldName))))
            {
                sqlUpsertStatement += GenerateUpsert(processingTree, whereCurrentClause);
            }
        }
        return sqlUpsertStatement;
    }

    private static string GenerateUpsert(NodeTree currentTree, string whereClause)
    {
        var sqlUpsertAux = string.Empty;
        var insert = new List<string>();
        insert.AddRange(currentTree.GraphElements.Select(m => m.FieldName));
        
        sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                        $" {string.Join(",", insert.Select(s => $"\"{s}\"").ToList())}) VALUES ({
                            string.Join(",", currentTree.GraphElements.Select(m => m.FieldValue).ToList())}) " +
                        $" ON CONFLICT" +
                        $" ({string.Join(",", currentTree.UpsertKeys.Select(s => $"\"{s.FieldDestinationName}\"").ToList())}) ";
        
        var exclude = new List<string>();
        exclude.AddRange(
            currentTree.GraphElements.Where(e => currentTree.UpsertKeys.Any(u => u
                .FieldDestinationName.Matches(e.FieldName))).Select(e => $"\"{e.FieldName}\" = EXCLUDED.\"{e.FieldName}\"")
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

    private static string GenerateSelectUpsert(NodeTree currentTree, NodeTree parentTree, 
        string whereClause)
    {
        var sqlUpsertAux = string.Empty;
        var insert = new List<string>();
        insert.AddRange(currentTree.GraphElements.Select(m => m.FieldName));
        insert.Add($"{currentTree.ParentName}Id");

        sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                        $" {string.Join(",", insert.Select(s => $"\"{s}\"").ToList())} ) " +
                        $" ( SELECT {
                            string.Join(",", currentTree.GraphElements.Select(m => $"{m.FieldValue} AS \"{m.FieldName}\"").ToList())}," +
                        $" {parentTree.Name}.\"Id\" AS \"{parentTree.Name}Id\" " +
                        $" FROM \"{parentTree.Schema}\".\"{parentTree.Name}\" {parentTree.Name} WHERE ";

        var whereUpsertKeys = string.Empty;

        for (var i = 0; i < parentTree.UpsertKeys.Count; i++)
        {
            whereUpsertKeys +=
                $" {parentTree.Name}.\"{parentTree.UpsertKeys[i].FieldDestinationName}\" = {currentTree.GraphElements.FirstOrDefault(p =>
                    p.FieldName.Matches(parentTree.UpsertKeys[i].FieldDestinationName)).FieldValue} ";
            whereUpsertKeys += i == parentTree.UpsertKeys.Count - 1 ? " ) " : " AND ";
        }
        
        var exclude = new List<string>();
        exclude.AddRange(
            currentTree.GraphElements.Where(e => currentTree.UpsertKeys.Any(u => u
                .FieldDestinationName.Matches(e.FieldName))).Select(e => $"\"{e.FieldName}\" = EXCLUDED.\"{e.FieldName}\"")
            );

        sqlUpsertAux += $" {whereUpsertKeys} ";
        sqlUpsertAux += $" ON CONFLICT" +
                        $" ({string.Join(",", currentTree.UpsertKeys.Select(s => $"\"{s.FieldDestinationName}\""))}) ";

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
    /// Recursive method to visit every entity that needs to be added into the SQL query statement
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="entityMap"></param>
    /// <param name="childrenFields"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <param name="currentTree"></param>
    /// <param name="isRootEntity"></param>
    /// <returns></returns>
    /// <summary>
    /// Recursive method to visit every entity that needs to be added into the SQL query statement
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="entityMap"></param>
    /// <param name="childrenFields"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <param name="currentTree"></param>
    /// <param name="isRootEntity"></param>
    /// <returns></returns>
    private static (string sqlStatement, NodeTree rootNodeTree) GenerateQuery(Dictionary<string, NodeTree> entityTrees, Dictionary<string, NodeTree> modelTrees, 
        List<string> childrenFields, List<string> models, List<string> entities, Dictionary<string, string> sqlWhereStatement, NodeTree currentTree,
        Dictionary<string, string> childrenSqlStatement,
        string rootEntityName)
    {;
        var childrenOrder = new List<string>();

        foreach (var child in currentTree.Children)
        {
            if (currentTree.ChildrenNames.Any(k => k.Matches(child.Name)))
            {
                var childrenSqlStatementAux = GenerateQuery(entityTrees, modelTrees, childrenFields, models, entities, sqlWhereStatement, 
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
        
        return GenerateEntityQuery(entityTrees, modelTrees, currentTree, currentTree.GraphElements, childrenFields, entities, sqlWhereStatement,
            childrenSqlStatement, rootEntityName, childrenOrder);
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
        Dictionary<string, NodeTree> modelTrees, NodeTree currentTree, List<GraphElement> tableFields, List<string> childrenFields, List<string> entities,
        Dictionary<string, string> sqlWhereStatement, Dictionary<string, string> childrenSqlStatement, string rootEntityName,
        List<string> childrenOrder)
    {
        var sqlChildren = string.Empty;
        var sqlQueryStatement =
            $" {(rootEntityName.Matches(currentTree.Name) ? "" : tableFields.Any(f => f.GraphElementType == GraphElementType.Edge) ? " JOIN ( " : " LEFT JOIN  ( ")} SELECT ";
        var childrenFieldAux = new List<string>();
        
        foreach (var element in currentTree.GraphElements)
        {
            if (element.TableName.Matches(currentTree.Name) || currentTree.Children.Any(c => c.Name.Matches(element.TableName)))
            {
                var fieldName = $"{currentTree.Name}.\"{element.FieldName}\" AS \"{element.FieldName}\"";
                childrenFieldAux.Add(fieldName);
            }
        }

        if (currentTree.Name.Matches("Customer"))
        {
            var a = true;
        }

        var joinKeyToAdd = tableFields.Any(f => currentTree.Mappings.Where(m => m.IsJoinKey)
            .Any(mf => f.FieldName.Matches(mf.FieldDestinationName)));

        if (childrenSqlStatement.Count > 0 && !childrenSqlStatement.ContainsKey(currentTree.Name) &&
            tableFields.Count > 0 && tableFields.Count <= 2 && 
            !tableFields.Any(f => !string.IsNullOrEmpty(f.Field) && !f.Field.Contains("Id")))
        {
            var newRootNodeTree = modelTrees[childrenSqlStatement.First().Key];
            sqlWhereStatement.TryGetValue(newRootNodeTree.Name, out var currentSqlWhereStatementNewRoot);
            var oldWhereStatement = currentSqlWhereStatementNewRoot;

            if (!string.IsNullOrEmpty(oldWhereStatement))
            {
                oldWhereStatement = oldWhereStatement.Replace("~", newRootNodeTree.Name);

                foreach (var field in oldWhereStatement.Split("\""))
                {
                    if (newRootNodeTree.Mappings.Any(m => m.FieldDestinationName.Matches(field)))
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

        if (!joinKeyToAdd)
        {
            foreach (var fieldMap in currentTree.Mappings.Where(m => m.IsJoinKey && m.DestinationEntity.Matches(currentTree.Name)))
            {
                tableFields.Add(
                    new GraphElement()
                    {
                        EntityId = currentTree.Id,
                        GraphElementType = tableFields.Any(f => f.TableName.Matches(currentTree.Name) &&
                                                                f.GraphElementType == GraphElementType.Edge)
                            ? GraphElementType.Edge
                            : GraphElementType.Node,
                        TableName = fieldMap.DestinationEntity,
                        FieldName = fieldMap.FieldDestinationName,
                        Field =
                            $"{fieldMap.DestinationEntity}.\"{fieldMap.FieldDestinationName.ToUpperCamelCase()}\" AS \"{fieldMap.FieldDestinationName.ToUpperCamelCase().ToSnakeCase(currentTree.Id)}\""
                    });
            }
        }

        var idToAdd = tableFields.Any(f => f.FieldName.Matches("Id"));
        
        if (!idToAdd)
        {
            tableFields.Add(
                new GraphElement()
                {
                    EntityId = currentTree.Id,
                    GraphElementType = tableFields.Any(f => f.TableName.Matches(currentTree.Name) &&
                                                            f.GraphElementType == GraphElementType.Edge)
                        ? GraphElementType.Edge
                        : GraphElementType.Node,
                    TableName = currentTree.Name,
                    FieldName = "Id",
                    Field = $"{currentTree.Name}.\"Id\" AS \"{"Id".ToSnakeCase(currentTree.Id)}\""
                });
        }
        
        foreach (var tableField in tableFields)
        {
            if (string.IsNullOrEmpty(tableField.Field))
            {
                continue;
            }
            
            var nodeTreeName = tableField.Field.Split('.')[0];

            if (nodeTreeName.Matches(currentTree.Name) || currentTree.Children.Any(c => c.Name.Matches(nodeTreeName)))
            {
                var tableFieldParts = tableField.Field.Split(".");
                var childField = tableFieldParts[1].Sanitize().Split(" AS ")[1];
                var fieldName = $"{currentTree.Name}.\"{childField}\" AS \"{childField}\"";
                childrenFieldAux.Add(fieldName);
            }
        }

        var childrenColumns = string.Join(",", childrenFields.Where(c => currentTree.Children.Any(cc => cc.Name
            .Matches(c.Split('.')[0].Sanitize()))));
        
        sqlQueryStatement += string.Join(",", tableFields.Select(s => s.Field)) +
                             $"{(!string.IsNullOrEmpty(childrenColumns) ? "," : "")}" + childrenColumns;
        sqlQueryStatement += $" FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name}";

        childrenFields.AddRange(childrenFieldAux);
        
        for (var i = 0; i < childrenSqlStatement.Count; i++)
        {
            if (childrenOrder.Count > i && childrenOrder.Count > 0 && !currentTree.GraphElements.Any(e => childrenSqlStatement.ContainsKey(e.TableName)))
            {
                var childTree = modelTrees[childrenOrder[i]];
                if (childrenSqlStatement.TryGetValue(childTree.Name, out var childStatement))
                {
                    sqlChildren +=
                        $" {childStatement} ) {childTree.Name} ON {currentTree.Name}.\"{"Id"}\" = {childTree.Name}.\"{childTree.JoinKey[0].FieldDestinationName.ToSnakeCase(childTree.Id)}\"";
                }
            }
        }

        sqlQueryStatement += $" {sqlChildren}";

        sqlWhereStatement.TryGetValue(currentTree.Name, out var currentSqlWhereStatement);

        if (!string.IsNullOrEmpty(currentSqlWhereStatement))
        {
            currentSqlWhereStatement = currentSqlWhereStatement.Replace("~", currentTree.Name);

            foreach (var field in currentSqlWhereStatement.Split("\""))
            {
                if (currentTree.Mappings.Any(m => m.FieldDestinationName.Matches(field)))
                {
                    currentSqlWhereStatement =
                        currentSqlWhereStatement.Replace(field,
                            $"{(rootEntityName.Matches(currentTree.Name) ? field : field.ToSnakeCase(currentTree.Id))}");
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

    /// <summary>
    /// Generates upsert SQL statement while recursively iteration the tree nodes
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="sqlUpsert"></param>
    /// <param name="upsertNode"></param>
    /// <param name="currentTree"></param>
    /// <param name="parentTree"></param>
    /// <param name="sqlNodes"></param>
    /// <param name="child"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <param name="permissions"></param>
    public static void GetMutations(Dictionary<string, NodeTree> trees, ISyntaxNode upsertNode, NodeTree currentTree,
        NodeTree parentTree, List<string> entityNames)
    {
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
                GetMutations(trees, uNode, currentTree, parentTree, entityNames);
            }
            else
            {
                if (uNode.ToString().Split(':').Length == 2)
                {
                    var fieldMap = currentTree.Mappings.FirstOrDefault(m =>
                        m.FieldSourceName.Matches(uNode.ToString().Split(':')[0].Trim()));

                    if (fieldMap == null)
                    {
                        continue;
                    }

                    var valueMutation = uNode.ToString().Split(':')[1].Sanitize();
                    
                    var enumerationMutation = fieldMap.DestinationEnumerationValues.FirstOrDefault(e => e.Key.Matches(valueMutation.Replace("_",""))).Value;

                    if (!string.IsNullOrEmpty(enumerationMutation))
                    {
                        valueMutation = enumerationMutation;
                    }
                    
                    var element = new GraphElement()
                    {
                        GraphElementType = GraphElementType.Mutation,
                        FieldName = fieldMap.FieldDestinationName,
                        FieldValue = $"'{valueMutation}'"
                    };
                    ProcessField(trees, currentTree, element, entityNames);
                    continue;
                }

                if (uNode.ToString().Split(':').Length == 4 && uNode.ToString().Split('.')[1].Length == 5 &&
                    uNode.ToString().Split('.')[1][3] == 'Z')
                {
                    var fieldMap = currentTree.Mappings.FirstOrDefault(m =>
                        m.FieldSourceName.Matches(uNode.ToString().Split(':')[0].Trim()));

                    if (fieldMap == null)
                    {
                        continue;
                    }
                    
                    var element = new GraphElement()
                    {
                        GraphElementType = GraphElementType.Mutation,
                        FieldName = fieldMap.FieldDestinationName,
                        FieldValue = string.Join('`', uNode.ToString().Split(':').Skip(1).ToList()).Replace('"', '\'')
                    };
                    ProcessField(trees, currentTree, element, entityNames);
                }
            }
        }
    }

    private static void ProcessField(Dictionary<string, NodeTree> trees, NodeTree currentTree, GraphElement element, List<string> entityNames)
    {
        if (entityNames.Any(e => e.Matches(currentTree.Name)))
        {
            UpsertElement(trees, currentTree, element);
        }
        foreach (var childTree in currentTree.Children)
        {
            ProcessField(trees, childTree, element, entityNames);
        }
    }

    private static void UpsertElement(Dictionary<string, NodeTree> trees, NodeTree currentTree, GraphElement element)
    {
        if (!currentTree.Mappings.Any(m => m.FieldSourceName.Matches(element.FieldName)))
        {
            return;
        }
        
        var index = trees[currentTree.Name].GraphElements.FindIndex(e => e.FieldName.Matches(element.FieldName));
        element.TableName = currentTree.Name;
        element.EntityId = currentTree.Id;
        if (index < 0)
        {
            trees[currentTree.Name].GraphElements.Add(element);
        }
        else
        {
            trees[currentTree.Name].GraphElements[index] = element;
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
                
                // if (entityNames.Any(e => currentTree.ParentName.Matches(e)))
                // // if (!string.IsNullOrWhiteSpace(currentTree.ParentName))
                // {
                //     
                // }
                // else
                // {
                //     
                // }
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

            GetFieldsWhere(trees, whereFields, sqlWhereStatement, wNode, currentEntity, rootEntityName, clauseType,
                permission);
        }
    }
}