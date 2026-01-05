using System.Text;
using Dapper;
using CoffeeBeanery.GraphQL.Extension;
using HotChocolate.Language;
using CoffeeBeanery.GraphQL.Model;
using FASTER.core;
using HotChocolate.Execution.Processing;
using MoreLinq;

namespace CoffeeBeanery.GraphQL.Helper;

public static class SqlNodeResolverHelper
{
    /// <summary>
    /// Method to handle three Selection using recursion to visit each argument and nodes
    /// </summary>
    /// <param name="graphQlSelection"></param>
    /// <param name="entityTreeMap"></param>
    /// <param name="modelTreeMap"></param>
    /// <param name="rootEntityName"></param>
    /// <param name="wrapperEntityName"></param>
    /// <param name="cache"></param>
    /// <param name="cacheKey"></param>
    /// <param name="permissions"></param>
    /// <typeparam name="D"></typeparam>
    /// <typeparam name="S"></typeparam>
    /// <returns></returns>
    public static SqlStructure HandleGraphQL<D, S>(ISelection graphQlSelection,
        IEntityTreeMap<D, S> entityTreeMap,
        IModelTreeMap<D, S> modelTreeMap, string rootEntityName, string wrapperEntityName,
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
        var sqlSelectStatement = string.Empty;
        var sqlUpsertStatement = string.Empty;
        var models = modelTreeMap.ModelNames;
        var transformedToParent = false;
        var tranformedModel = rootEntityName;
        var rootModelName = rootEntityName;

        for (int i = 0; i < entityTreeMap.EntityNames.Count; i++)
        {
            if (entityTreeMap.DictionaryTree.ContainsKey(rootEntityName))
            {
                rootEntityName = entityTreeMap.DictionaryTree[rootEntityName].ParentName;
                transformedToParent = true;
                break;
            }
        }

        if (!rootEntityName.Matches(wrapperEntityName) && !transformedToParent)
        {
            //It can only be one single model within wrapper model
            rootEntityName = entityTreeMap.DictionaryTree[wrapperEntityName].Children.Last().Name;
            transformedToParent = true;
        }
        
        //Where conditions
        GetFieldsWhere(modelTreeMap.DictionaryTree, entityTreeMap.LinkDictionaryTreeNode,
            modelTreeMap.LinkDictionaryTreeNode,
            whereFields, sqlWhereStatement, graphQlSelection.SyntaxNode.Arguments
                .FirstOrDefault(a => a.Name.Value.Matches("where")),
            modelTreeMap.DictionaryTree.Last().Value.Name, rootModelName, wrapperEntityName,
            string.Empty, Entity.ClauseTypes, permissions);

        //Arguments
        foreach (var argument in graphQlSelection.SyntaxNode.Arguments
                     .Where(a => !a.Name.Value.Matches("where")))
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
                    sqlOrderStatement = GetFieldsOrdering(modelTreeMap.DictionaryTree, orderNode,
                        rootEntityName,
                        wrapperEntityName, rootEntityName, modelTreeMap.LinkDictionaryTreeNode);
                }
            }

            var sqlUpsertStatementNodes = new Dictionary<string, SqlNode>();
            var visitedModels = new List<string>();

            if (argument.Name.Value.Matches(wrapperEntityName))
            {
                var nodeTreeRoot = new NodeTree();
                nodeTreeRoot.Name = string.Empty;
                var mutationNodeToProcess = argument.Value.GetNodes()
                    .First(a => !a.ToString().Contains("cache") && !a.ToString().Contains("model"));

                var generatedQuery = new Dictionary<string, string>();

                if (mutationNodeToProcess.GetNodes().ToList()[1].ToString().StartsWith("["))
                {
                    foreach (var mutationNode in mutationNodeToProcess.GetNodes().ToList()[1].GetNodes())
                    {
                        GetMutations(modelTreeMap.DictionaryTree, mutationNode,
                            entityTreeMap.LinkDictionaryTreeMutation, modelTreeMap.LinkDictionaryTreeMutation,
                            sqlUpsertStatementNodes, entityTreeMap.DictionaryTree
                                .First(t =>
                                    t.Key.Matches(rootEntityName)).Value, string.Empty,
                            new NodeTree(), models, modelTreeMap.EntityNames, visitedModels);
                        
                        SqlHelper.GenerateUpsertStatements(entityTreeMap.DictionaryTree, entityTreeMap.LinkDictionaryTreeMutation, rootEntityName,
                            wrapperEntityName, generatedQuery, sqlUpsertStatementNodes, entityTreeMap.DictionaryTree[rootEntityName], entityTreeMap.EntityNames,
                            sqlWhereStatement, new List<string>());
                    }
                }
                else
                {
                    GetMutations(modelTreeMap.DictionaryTree, mutationNodeToProcess,
                        entityTreeMap.LinkDictionaryTreeMutation, modelTreeMap.LinkDictionaryTreeMutation,
                        sqlUpsertStatementNodes, entityTreeMap.DictionaryTree
                            .First(t =>
                                t.Key.Matches(rootEntityName)).Value, string.Empty,
                        new NodeTree(), models, modelTreeMap.EntityNames, visitedModels);
                    
                    SqlHelper.GenerateUpsertStatements(entityTreeMap.DictionaryTree, entityTreeMap.LinkDictionaryTreeMutation, rootEntityName,
                        wrapperEntityName, generatedQuery, sqlUpsertStatementNodes, entityTreeMap.DictionaryTree[rootEntityName], entityTreeMap.EntityNames,
                        sqlWhereStatement, new List<string>());
                }

                var statement = generatedQuery.Values.Order();
                sqlUpsertStatement = @"LOAD 'age';
                    SET search_path = ag_catalog, ""$user"", public; " + string.Join(";", statement);
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

        var sqlStatementNodes = new Dictionary<string, SqlNode>();
        var visitedFieldModel = new List<string>();

        if (graphQlSelection.SelectionSet?.Selections!
                .FirstOrDefault(s => s.ToString().StartsWith("edges")) != null)
        {
            GetFields(modelTreeMap.DictionaryTree, edgeNode.GetNodes().ToList()[1].GetNodes()
                    .ToList()[0],
                entityTreeMap.LinkDictionaryTreeEdge, modelTreeMap.LinkDictionaryTreeEdge,
                sqlStatementNodes,
                entityTreeMap.DictionaryTree.First(t =>
                    t.Key.Matches(rootEntityName)).Value,
                new NodeTree(), visitedFieldModel, models, rootModelName, modelTreeMap.EntityNames,
                true);
        }

        if (graphQlSelection.SelectionSet?.Selections!
                .FirstOrDefault(s => s.ToString().StartsWith("nodes")) != null)
        {
            GetFields(modelTreeMap.DictionaryTree, node,
                entityTreeMap.LinkDictionaryTreeNode, modelTreeMap.LinkDictionaryTreeNode,
                sqlStatementNodes,
                entityTreeMap.DictionaryTree.First(t =>
                    t.Key.Matches(rootEntityName)).Value,
                new NodeTree(), visitedFieldModel, models, rootModelName, modelTreeMap.EntityNames,
                false);
        }

        var sqlQueryStatement = new StringBuilder();
        var sqlQueryStructures = new Dictionary<string, SqlQueryStructure>(
            StringComparer.OrdinalIgnoreCase);
        var splitOnDapper = new Dictionary<string, Type>();
        var entityOrder = new List<string>();

        if (string.IsNullOrEmpty(sqlSelectStatement))
        {
            var childrenSqlStatement = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            var entityTypes = entityTreeMap.EntityTypes.Select(a => a as Type).ToList();

            GenerateQuery(entityTreeMap.DictionaryTree,
                entityTypes,
                entityTreeMap.LinkDictionaryTreeNode,
                sqlQueryStatement, sqlStatementNodes, sqlWhereStatement,
                entityTreeMap.DictionaryTree[rootEntityName], modelTreeMap.DictionaryTree[rootModelName],
                childrenSqlStatement, entityTreeMap.EntityNames, entityTreeMap.ModelNames, sqlQueryStructures,
                splitOnDapper, entityOrder, rootEntityName, rootModelName);
            
            //if transformedToParent then will be used the first matching child, TODO: Support multiple child queries for complex entities

            var queryStructure = sqlQueryStructures.FirstOrDefault();

            if (transformedToParent && !rootEntityName.Matches(wrapperEntityName))
            {
                splitOnDapper.Remove(splitOnDapper.FirstOrDefault(a => a.Value.Name == rootEntityName).Key);
                
                foreach (var childName in entityTreeMap.DictionaryTree[rootEntityName].ChildrenName)
                {
                    if (modelTreeMap.LinkDictionaryTreeNode.FirstOrDefault(a => a.Key.Split('~')[0].Matches(tranformedModel)).Value.RelationshipKey.Split('~')[1]
                        .Matches(childName))
                    {
                        queryStructure = sqlQueryStructures.FirstOrDefault(s => s.Key.Matches(childName));
                        if (queryStructure.Value != null)
                        {
                            break;
                        }    
                    }
                    // else
                    // {
                    //     splitOnDapper.Remove(splitOnDapper.First(a => a.Value.Name == childName).Key);
                    // }
                }
            }

            splitOnDapper = splitOnDapper.Where(a => a.Value != null)
                .ToDictionary(a => a.Key, a => a.Value);

            sqlSelectStatement = queryStructure.Value.Query;

            if (splitOnDapper.Count == 0)
            {
                splitOnDapper.Add(queryStructure.Value.JoinOneKey, entityTypes
                    .First(a => a.Name.Matches(queryStructure.Key)));
            }

            //Update cache
            // if (!isCached)
            // {
            //     cacheReadSession.Upsert(ref cacheKey, ref sqlStament);    
            // }
        }

        var splitOnDapperOrdered = new Dictionary<string, Type>();

        foreach (var key in entityOrder)
        {
            var kv = splitOnDapper.FirstOrDefault(t => t.Value.Name.Matches(key));

            if (kv.Value != null && !splitOnDapperOrdered.ContainsKey(kv.Key))
            {
                splitOnDapperOrdered.Add(kv.Key, kv.Value);
            }
        }

        if (splitOnDapperOrdered.Count == 0)
        {
            var entity = entityTreeMap.EntityTypes[0] as Type;
            splitOnDapperOrdered.Add(entity.Name, entity);
        }

        if (string.IsNullOrEmpty(sqlSelectStatement))
        {
            return default;
        }

        var hasTotalCount = false;

        if (hasPagination || hasSorting)
        {
            rootNodeTree = entityTreeMap.DictionaryTree[rootEntityName];
            // Query Where, Sort, and Pagination
            sqlSelectStatement = SqlHelper.HandleQueryClause(rootNodeTree, sqlSelectStatement,
                sqlOrderStatement, pagination, hasTotalCount);
        }

        var sqlStructure = new SqlStructure()
        {
            SqlQuery = sqlSelectStatement,
            Parameters = parameters,
            SqlUpsert = sqlUpsertStatement,
            SplitOnDapper = splitOnDapperOrdered,
            Pagination = pagination,
            HasTotalCount = false
        };

        return sqlStructure;
    }
    
    /// Recursive method to visit every entity that needs to be added into the SQL query statement
    /// </summary>
    /// <param name="entityTrees"></param>
    /// <param name="entityTypes"></param>
    /// <param name="linkEntityDictionaryTree"></param>
    /// <param name="sqlQueryStatement"></param>
    /// <param name="sqlStatementNodes"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <param name="currentTree"></param>
    /// <param name="childrenSqlStatement"></param>
    /// <param name="entityNames"></param>
    /// <param name="sqlQueryStructures"></param>
    /// <param name="splitOnDapper"></param>
    private static bool
        GenerateQuery(Dictionary<string, NodeTree> entityTrees,
            List<Type> entityTypes,
            Dictionary<string, SqlNode> linkEntityDictionaryTreeNode,
            StringBuilder sqlQueryStatement, Dictionary<string, SqlNode> sqlStatementNodes,
            Dictionary<string, string> sqlWhereStatement,
            NodeTree currentEntityTree, NodeTree currentModelTree, Dictionary<string, string> childrenSqlStatement,
            List<string> entityNames, List<string> modelNames,
            Dictionary<string, SqlQueryStructure> sqlQueryStructures,
            Dictionary<string, Type> splitOnDapper, List<string> entityOrder, string rootEntityName, string rootModelName)
    {
        var hasChildren = false;
        var currentEntityStructure = GenerateEntityQuery(entityTrees,
            linkEntityDictionaryTreeNode,
            sqlStatementNodes, currentEntityTree, currentModelTree, entityNames, modelNames, sqlQueryStatement,
            sqlQueryStructures, sqlWhereStatement, childrenSqlStatement, rootEntityName, rootModelName, hasChildren);

        if (currentEntityStructure == null || currentEntityStructure.Columns == null || currentEntityStructure.Columns.Count == 0)
        {
            return hasChildren;
        }

        var queryBuilder = currentEntityStructure.Query;
        currentEntityStructure.SelectColumns.AddRange(currentEntityStructure.Columns);
        currentEntityStructure.SelectColumns = currentEntityStructure.SelectColumns.Distinct().ToList();

        currentEntityTree = entityTrees[currentEntityTree.Name];
        entityOrder.Add(currentEntityTree.Name);
        
        foreach (var child in currentEntityTree.Children)
        {
            if (currentEntityTree.ChildrenName.Any(k => k.Matches(child.Name)) &&
                sqlQueryStructures.TryGetValue(child.Name, out var childStructure))
            {
                GenerateQuery(entityTrees, entityTypes, linkEntityDictionaryTreeNode,
                    sqlQueryStatement, sqlStatementNodes, sqlWhereStatement,
                    child, null, childrenSqlStatement, entityNames, modelNames, sqlQueryStructures,
                    splitOnDapper, entityOrder, rootEntityName, rootModelName);

                queryBuilder += childStructure.SqlNodeType == SqlNodeType.Edge ? " JOIN " : " LEFT JOIN ";
                queryBuilder +=
                    $" ( {childStructure.Query} ) {child.Name} ON {currentEntityTree.Name}.\"{(string.IsNullOrEmpty(childStructure.JoinOneKey) ? "Id" : childStructure.JoinOneKey)}\" = {child.Name}.\"{"Id".ToSnakeCase(child.Id)}\"";

                currentEntityStructure.SelectColumns.AddRange(
                    childStructure.ParentColumns.Select(s => s.Replace("~", child.Name)));
                currentEntityStructure.ParentColumns.AddRange(childStructure.ParentColumns);

                currentEntityStructure.SelectColumns = currentEntityStructure.SelectColumns.Distinct().ToList();
                currentEntityStructure.ParentColumns = currentEntityStructure.ParentColumns.Distinct().ToList();

                if (!splitOnDapper.ContainsKey("Id".ToSnakeCase(child.Id)))
                {
                    splitOnDapper.Add("Id".ToSnakeCase(child.Id),
                        entityTypes.FirstOrDefault(e => e.Name.Matches(child.Name)));
                }
            }
        }

        var select = string.Join(",", currentEntityStructure.SelectColumns.DistinctBy(a => a.Split(" AS ")[1]));

        queryBuilder = queryBuilder.Replace("%", select);
        queryBuilder += " " + currentEntityStructure.WhereClause;

        currentEntityStructure.Query = queryBuilder;
        var currentNode = linkEntityDictionaryTreeNode.FirstOrDefault(a =>
            a.Key.Contains(currentEntityTree.Name));
        currentEntityStructure.Id = currentEntityTree.Id;
        currentEntityStructure.SqlNodeType = currentNode.Value.SqlNodeType;
        currentEntityStructure.SqlNode = currentNode.Value;

        if (sqlQueryStructures.TryGetValue(currentEntityTree.Name, out var sqlQueryStructure))
        {
            sqlQueryStructures[currentEntityTree.Name] = currentEntityStructure;
        }
        else
        {
            sqlQueryStructures.Add(currentEntityTree.Name, currentEntityStructure);
        }

        if (!splitOnDapper.ContainsKey("Id".ToSnakeCase(currentEntityTree.Id)))
        {
            splitOnDapper.Add("Id".ToSnakeCase(currentEntityTree.Id),
                entityTypes.FirstOrDefault(e => e.Name.Matches(currentEntityTree.Name)));
        }

        return true;
    }


    /// <summary>
    /// Map each entity node into raw query SQL statement
    /// </summary>
    /// <param name="entityTrees"></param>
    /// <param name="linkEntityDictionaryTree"></param>
    /// <param name="sqlStatementNodes"></param>
    /// <param name="currentTree"></param>
    /// <param name="entityNames"></param>
    /// <param name="sqlQueryStatement"></param>
    /// <param name="sqlQueryStructures"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <param name="childrenSqlStatement"></param>
    /// <returns></returns>
    private static SqlQueryStructure GenerateEntityQuery(Dictionary<string, NodeTree> entityTrees,
        Dictionary<string, SqlNode> linkEntityDictionaryTreeNode,
        Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentEntityTree, NodeTree currentModelTree, List<string> entityNames, List<string> modelNames,
        StringBuilder sqlQueryStatement, Dictionary<string, SqlQueryStructure> sqlQueryStructures,
        Dictionary<string, string> sqlWhereStatement, Dictionary<string, string> childrenSqlStatement,
        string rootEntityName, string rootModelName, bool hasChildren)
    {
        var currentColumns = new List<KeyValuePair<string, SqlNode>>();
        var childrenJoinColumns = new Dictionary<string, string>();
        var entitySql = string.Empty;
        var graphSql = string.Empty;
        var graphColumns = new List<KeyValuePair<string, SqlNode>>();
        var upsertColumn = new KeyValuePair<string, SqlNode>();

        var columnToAdd = linkEntityDictionaryTreeNode
            .FirstOrDefault(k => k.Key
                .Matches($"{currentEntityTree.Name}~Id"));

        if (columnToAdd.Value != null)
        {
            currentColumns.Add(columnToAdd);
        }

        currentColumns.AddRange(sqlStatementNodes
            .Where(k =>
                        (currentEntityTree.Mapping.Any(m => m.FieldDestinationName
                             .Matches(k.Key.Split('~')[1])) &&
                         !k.Key.Matches($"{currentEntityTree.Name}~Id"))).ToList());

        if (currentColumns.Count == 0)
        {
            return null;
        }

        currentColumns = currentColumns.Distinct().ToList();

        var queryBuilder = string.Empty;
        var queryColumns = new List<string>();
        var parentQueryColumns = new List<string>();

        foreach (var tableColumn in currentColumns)
        {
            var tableFieldParts = tableColumn.Key.Split('~');
            var fieldName = tableFieldParts.Length > 1 ? tableFieldParts[1] : tableFieldParts[0];

            if (!queryColumns.Contains($"\"{fieldName
                .ToSnakeCase(currentEntityTree.Id)}\""))
            {
                queryColumns.Add(
                    $"{currentEntityTree.Name}.\"{fieldName}\" AS \"{fieldName
                        .ToSnakeCase(currentEntityTree.Id)}\"");
            }

            if (!parentQueryColumns.Contains($"\"{fieldName.ToSnakeCase(currentEntityTree.Id)}\""))
            {
                parentQueryColumns.Add(
                    $"~.\"{fieldName.ToSnakeCase(currentEntityTree.Id)}\" AS \"{fieldName.ToSnakeCase(currentEntityTree.Id)}\"");
            }
        }

        foreach (var childQuery in sqlQueryStructures
                     .Where(c =>
                         currentEntityTree.Children
                         .Any(b => b.Name.Matches(c.Key))))
        {
            queryBuilder += $" {(childQuery.Value.SqlNodeType == SqlNodeType.Edge ?
                " JOIN ( " : " LEFT JOIN  ( ")} {childQuery.Value.Query}";
            
            var joinChildKey = $"\"{"Id"
                .ToSnakeCase(childQuery.Value.Id)}\"";

            if (currentColumns.Count > 0)
            {
                var linkKeys = currentColumns[0].Value.JoinKeys
                    .Where(k => k.To.Matches(childQuery.Key)).ToList();

                if (linkKeys.Count == 0)
                {
                    for (var i = 0; i < linkKeys.Count; i++)
                    {
                        if (i == 0)
                        {
                            queryBuilder +=
                                $" ) {childQuery.Key} ON {currentEntityTree.Name}.\"Id\" = {childQuery.Key}.{joinChildKey}";
                        }
                        else
                        {
                            queryBuilder +=
                                $" AND {childQuery.Key} ON {currentEntityTree.Name}.\"Id\" = {childQuery.Key}.{joinChildKey}";
                        }
                    }
                }
            }
        }

        var entitySqlWhereStatement = string.Empty;

        if (currentColumns.Count <= 2 && childrenSqlStatement.Count > 0)
        {
            var newRootNodeTree = entityTrees[childrenSqlStatement.First().Key];
            sqlWhereStatement.TryGetValue(newRootNodeTree.Name, out var
                currentSqlWhereStatementNewRoot);
            var oldWhereStatement = currentSqlWhereStatementNewRoot;

            if (!string.IsNullOrEmpty(oldWhereStatement))
            {
                oldWhereStatement = oldWhereStatement.Replace("~", newRootNodeTree.Name);

                foreach (var field in oldWhereStatement.Split("\""))
                {
                    oldWhereStatement =
                        oldWhereStatement.Replace(field, $"{field}");
                }

                oldWhereStatement = $" WHERE {oldWhereStatement} ";
            }
            else
            {
                oldWhereStatement = string.Empty;
            }

            currentSqlWhereStatementNewRoot = string.IsNullOrEmpty(currentSqlWhereStatementNewRoot)
                ? string.Empty
                : currentSqlWhereStatementNewRoot;

            if (childrenSqlStatement.Count > 0 && childrenSqlStatement.Count > 0 &&
                !string.IsNullOrEmpty(currentSqlWhereStatementNewRoot))
            {
                var cutoff = childrenSqlStatement.First().Value.IndexOf('(') + 1;
                var sqlStatement =
                    $"{childrenSqlStatement.First().Value.Substring(cutoff,
                        childrenSqlStatement.First()
                        .Value.Length - cutoff)}";
                sqlStatement = sqlStatement.Replace(oldWhereStatement,
                    $" WHERE {currentSqlWhereStatementNewRoot.Replace("~", newRootNodeTree.Name)}");

                sqlQueryStatement.Append(sqlStatement);
            }
        }
        else
        {
            graphColumns = currentColumns
                .Where(a => a.Value.IsColumnGraph).ToList();
            
            sqlQueryStatement.Append(queryBuilder);
            queryBuilder = "";

            if (graphColumns.Any())
            {
                upsertColumn = graphColumns.First(a => 
                    a.Value.UpsertKeys.First().Split('~')[1].Matches(a.Value.Column));
                
                graphSql = $"WITH graph_data AS ( SELECT {(string.Join(",", currentColumns.Where(a =>  a.Value.UpsertKeys.Count > 0)
                    .Select(a => $"{a.Value.Column}::TEXT::UUID AS {a.Value.Column}").ToList()))} FROM cypher('{currentColumns.First(a => !string.IsNullOrEmpty(a.Value.Graph)).Value.Graph}', $$ MATCH " +
                           $"(p:{currentEntityTree.Name} ) RETURN {
                               $"{(string.Join(",", currentColumns.Where(a =>  a.Value.UpsertKeys.Count > 0)
                                   .Select(a => $"(p.{a.Value.Column}) AS {a.Value.Column}").ToList()))}"} $$) AS ({$"{
                                       (string.Join(",", currentColumns.Where(a =>  a.Value.UpsertKeys.Count > 0)
                                           .Select(a => $"{a.Value.Column} agtype").ToList()))} )) "}" + 
                           $"SELECT * FROM (SELECT {(string.Join(",", currentColumns.Where(a =>  a.Value.UpsertKeys.Count > 0)
                                   .Select(a => $" (a.{a.Value.Column})::UUID AS {a.Value.Column} ").ToList()))
                           } FROM graph_data a ";
                
                var graphColumnTree = entityTrees[upsertColumn.Value.Entity];
                
                graphSql += $" JOIN \"{graphColumnTree.Schema}\".\"{graphColumnTree.Name}\" {
                    upsertColumn.Value.Entity}{upsertColumn.Value.Column} ON a.{upsertColumn.Value.Column} = {
                        upsertColumn.Value.Entity}{upsertColumn.Value.Column}.\"{upsertColumn.Value.Column}\" ";

                var parentFieldGraph = graphColumns.First();
                var parentGraph = parentFieldGraph.Value.JoinKeys.First(a => !a.From.Matches(parentFieldGraph.Key));
                
                foreach (var column in graphColumns)
                {
                    foreach (var joinKey in column.Value.JoinKeys)
                    {
                        if (!joinKey.To.Split('~')[1].Matches(column.Value.Column))
                        {
                            continue;
                        }

                        graphColumnTree = entityTrees[joinKey.From.Split('~')[0]];
                        var upsertKey = linkEntityDictionaryTreeNode.First(a => 
                            a.Value.Entity.Matches(graphColumnTree.Name));

                        graphSql += $" JOIN \"{graphColumnTree.Schema}\".\"{graphColumnTree.Name}\" {
                            parentGraph.From.Split('~')[0]}{joinKey.To.Split('~')[1]} ON a.{
                                joinKey.To.Split('~')[1]} = {
                                    parentGraph.From.Split('~')[0]}{joinKey.To.Split('~')[1]}.\"{parentGraph.From.Split('~')[1]}\" ";
                    }
                }
            }
            
            entitySql += " SELECT % ";
            entitySql += $" FROM \"{currentEntityTree.Schema}\".\"{currentEntityTree.Name}\" {currentEntityTree.Name}";

            var model = linkEntityDictionaryTreeNode
                .FirstOrDefault(e =>
                e.Key.Split('~')[0].Matches(currentEntityTree.Name));

            var modelValue = string.Empty;
            if (model.Value != null)
            {
                modelValue = model.Value?.Mapping?.FirstOrDefault(m =>
                    m.DestinationEntity.Matches(currentEntityTree.Name))?.SourceModel ?? string.Empty;
            }

            if (string.IsNullOrEmpty(modelValue))
            {
                modelValue = currentEntityTree.Name;
            }

            if (sqlWhereStatement.TryGetValue(modelValue, out var currentSqlWhereStatement))
            {
                currentSqlWhereStatement = currentSqlWhereStatement.Replace("~", currentEntityTree.Name);
                entitySqlWhereStatement = $" WHERE {currentSqlWhereStatement} ";
            }
            else
            {
                entitySqlWhereStatement = string.Empty;
            }
        }

        var joinOneKey = string.Empty;
        var onJoinKey = string.Empty;
        var currentJoinOneKeys =
            currentColumns.FirstOrDefault(a => a.Key.Split('~')[0].Matches(currentEntityTree.Name)).Value;

        if (currentJoinOneKeys != null && currentJoinOneKeys.JoinOneKeys != null && currentJoinOneKeys.JoinOneKeys.Count() > 0
            &&
            currentJoinOneKeys.JoinOneKeys[0].From.Split('~')[0].Matches(currentEntityTree.Name))
        {
            var oneKey = currentJoinOneKeys.JoinOneKeys.FirstOrDefault();
            
            joinOneKey = $"{currentEntityTree.Name}.\"{oneKey.To.Split('~')[1]}\" AS \"{oneKey.To.Split('~')[0]}{oneKey
                .To.Split('~')[1].ToSnakeCase(currentEntityTree.Id)}\"";
            var joinOneKeyParent = $"~.\"{oneKey.To.Split('~')[1].ToSnakeCase(currentEntityTree.Id)}\" AS \"{oneKey
                .To.Split('~')[0]}{oneKey.To.Split('~')[1].ToSnakeCase(currentEntityTree.Id)}\"";
            queryColumns.Add(joinOneKey);
            if (!parentQueryColumns.Contains($"\"{oneKey.To.Split('~')[0]}{oneKey.To.Split('~')[1]
                .ToSnakeCase(currentEntityTree.Id)}\""))
            {
                parentQueryColumns.Add(joinOneKeyParent);
            }

            onJoinKey = $"{currentEntityTree.Name}{oneKey.To.Split('~')[1]}";
        }

        queryColumns = queryColumns.Distinct().ToList();

        if (!string.IsNullOrEmpty(graphSql))
        {
            var parentFieldGraph = graphColumns.First();
            var parentGraph = parentFieldGraph.Value.JoinKeys.First(a => !a.From.Matches(parentFieldGraph.Key));

            entitySql = $" ;{graphSql}) a";
            //         $"ON {parentGraph.To.Replace("~","")}.\"{
            // currentColumns.Last().Value.UpsertKeys.First().Split('~')[1]}\" = a.{
            //     currentColumns.Last().Value.UpsertKeys.First().Split('~')[1]} ";
        }
        queryBuilder += entitySql;
        
        var sqlStructure = new SqlQueryStructure()
        {
            Id = currentEntityTree.Id,
            SqlNodeType = currentColumns.Count > 0 ? currentColumns.Last().Value.SqlNodeType :
                SqlNodeType.Node,
            SqlNode = currentColumns.Count > 0 ? currentColumns.Last().Value :
                new SqlNode(),
            Query = queryBuilder,
            Columns = queryColumns,
            ParentColumns = parentQueryColumns,
            ChildrenJoinColumns = childrenJoinColumns,
            WhereClause = entitySqlWhereStatement,
            JoinOneKey = onJoinKey
        };

        if (!sqlQueryStructures.Any(a => a.Key
                .Matches(currentEntityTree.Name)))
        {
            sqlQueryStructures.Add(currentEntityTree.Name, sqlStructure);
        }

        if (currentEntityTree.Name.Matches(rootEntityName))
        {
            var addingMissingUpsertKeys = linkEntityDictionaryTreeNode
                .First(c => c.Key.Split('~')[0].Matches(currentEntityTree.Name))
                .Value.UpsertKeys.Where(u => !sqlStructure.Columns.Any(a => a
                    .Matches($"{currentEntityTree.Name}.\"Id\" AS \"{u.Split('~')[1].ToSnakeCase(currentEntityTree.Id)}\"")))
                    .Select(a => $"{currentEntityTree.Name}.\"{a.Split('~')[1]}\" AS \"{a.Split('~')[1].ToSnakeCase(currentEntityTree.Id)}\"");

            var addingMissingUpsertKeysParent = linkEntityDictionaryTreeNode
                .First(c => c.Key.Split('~')[0].Matches(currentEntityTree.Name))
                .Value.UpsertKeys.Where(u => !sqlStructure.Columns.Any(a => a
                    .Matches($"{currentEntityTree.Name}.\"Id\" AS \"{u.Split('~')[1].ToSnakeCase(entityTrees[currentEntityTree.Name].Id)}\"")))
                .Select(a => $"{currentEntityTree.Name}.\"{a.Split('~')[1].ToSnakeCase(entityTrees[currentEntityTree.Name]
                    .Id)}\" AS \"{a.Split('~')[1].ToSnakeCase(entityTrees[currentEntityTree.Name].Id)}\"");

            if (addingMissingUpsertKeys != null && addingMissingUpsertKeys.Count() > 0)
            {
                sqlStructure.Columns.AddRange(addingMissingUpsertKeys);

                foreach (var key in addingMissingUpsertKeysParent)
                {
                    if (!sqlStructure.ParentColumns.Contains(key))
                    {
                        sqlStructure.ParentColumns.Add(key);
                    }
                }
            }

            sqlStructure.Columns = sqlStructure.Columns.Distinct().ToList();
        }

        return sqlStructure;
    }

    /// <summary>
    /// Method for getting upsert field information used for the SQL Statement
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="node"></param>
    /// <param name="linkEntityDictionaryTree"></param>
    /// <param name="linkModelDictionaryTree"></param>
    /// <param name="sqlStatementNodes"></param>
    /// <param name="currentTree"></param>
    /// <param name="previousNode"></param>
    /// <param name="parentTree"></param>
    /// <param name="models"></param>
    /// <param name="entities"></param>
    /// <param name="visitedModels"></param>
    public static void GetMutations(Dictionary<string, NodeTree> trees, ISyntaxNode node,
        Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> linkModelDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree,
        string previousNode, NodeTree parentTree, List<string> models, List<string> entities,
        List<string> visitedModels)
    {
        if (node != null && node.GetNodes()?.Count() == 0)
        {
            if (linkEntityDictionaryTree.TryGetValue($"{currentTree.Name}~{previousNode.Split(':')[0]}",
                    out var sqlNodeFrom)
                )
            {
                if (linkModelDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey,
                        out var sqlNodeTo))
                {
                    sqlNodeTo.SqlNodeType = SqlNodeType.Mutation;

                    if (previousNode.Split(':').Length == 2)
                    {
                        if (sqlNodeTo.FromEnumeration.TryGetValue(
                                previousNode.Split(':')[1].Sanitize().Replace("_", ""),
                                out var enumValue))
                        {
                            var toEnum = sqlNodeTo.FromEnumeration
                                .FirstOrDefault(e =>
                                e.Value.Matches(enumValue)).Value;
                            sqlNodeTo.Value = toEnum;
                        }
                        else
                        {
                            sqlNodeTo.Value = previousNode.Split(':')[1].Sanitize();
                        }
                    }

                    AddEntity(linkEntityDictionaryTree, sqlStatementNodes, trees,
                        sqlNodeTo);

                    if (!visitedModels.Contains(currentTree.Name))
                    {
                        visitedModels.Add(currentTree.Name);
                    }
                }

                if (previousNode.Split(':').Length == 2)
                {
                    if (sqlNodeFrom.ToEnumeration.TryGetValue(previousNode.Split(':')[1]
                                .Sanitize().Replace("_", ""),
                            out var enumValue))
                    {
                        sqlNodeFrom.Value = enumValue;
                    }
                    else
                    {
                        sqlNodeFrom.Value = previousNode.Split(':')[1].Sanitize();
                    }
                }

                AddEntity(linkEntityDictionaryTree, sqlStatementNodes, trees,
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
            if (models.Any(e => e.Matches(childNode.ToString().Split('{')[0])) ||
                node.ToString().Matches("nodes") ||
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

            GetMutations(trees, childNode, linkEntityDictionaryTree, linkModelDictionaryTree,
                sqlStatementNodes, currentTree, node.ToString(),
                parentTree, models, entities, visitedModels);
        }
    }

    private static void AddEntity(Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes, Dictionary<string, NodeTree> trees,
        SqlNode? sqlNode)
    {
        foreach (var entity in linkEntityDictionaryTree
                   .Where(v => sqlNode.Column.Matches(v.Key.Split('~')[1])))
        {
            if (sqlNode.Column.Matches("CustomerKey"))
            {
                var a = true;
            }
            
            entity.Value.Value = sqlNode.Value;
            // entity.Value.Column = entity.Key.Split('~')[1];
            // entity.Value.Entity = entity.Key.Split('~')[0];
            
            if (!sqlStatementNodes.ContainsKey(entity.Key))
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
    /// <param name="linkEntityDictionaryTree"></param>
    /// <param name="linkModelDictionaryTree"></param>
    /// <param name="sqlStatementNodes"></param>
    /// <param name="currentTree"></param>
    /// <param name="parentTree"></param>
    /// <param name="visitedModels"></param>
    /// <param name="models"></param>
    /// <param name="entities"></param>
    /// <param name="isEdge"></param>
    public static void GetFields(Dictionary<string, NodeTree> trees, ISyntaxNode node,
        Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> linkModelDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree,
        NodeTree parentTree, List<string> visitedModels, List<string> models,
        string rootEntity, List<string> entities, bool isEdge)
    {
        if (node != null && node.GetNodes()?.Count() == 0)
        {
            var currentModel = visitedModels.FirstOrDefault();

            if (linkEntityDictionaryTree.TryGetValue($"{currentTree.Name}~{node.ToString()}",
                    out var sqlNodeFrom) ||
                linkEntityDictionaryTree.TryGetValue($"{currentModel}~{node.ToString()}",
                    out sqlNodeFrom)
               )
            {
                if (linkModelDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey,
                        out var sqlNodeTo))
                {
                    
                    AddField(linkEntityDictionaryTree, sqlStatementNodes, entities,
                        sqlNodeTo, isEdge);

                    if (!visitedModels.Contains(currentTree.Name))
                    {
                        visitedModels.Add(currentTree.Name);
                    }
                }

                AddField(linkEntityDictionaryTree, sqlStatementNodes, entities,
                        sqlNodeFrom, isEdge);
            }
        }

        foreach (var childNode in node.GetNodes())
        {
            if (models.Any(e => e.Matches(childNode.ToString().Split('{')[0])) ||
                childNode.ToString().Matches("nodes") ||
                childNode.ToString().Matches("node"))
            {
                if (childNode.ToString().Matches("nodes") ||
                    childNode.ToString().Matches("node"))
                {
                    currentTree = trees[rootEntity];
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

            GetFields(trees, childNode, linkEntityDictionaryTree, linkModelDictionaryTree,
                sqlStatementNodes,
                currentTree,
                parentTree, visitedModels, models, rootEntity, entities, isEdge);
        }
    }

    private static void AddField(Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes, List<string> entities, SqlNode? sqlNode,
        bool isEdge)
    {
        foreach (var entity in linkEntityDictionaryTree
                     .Where(v => (sqlNode.Column.Matches(v.Value.Column) || 
                            sqlNode.UpsertKeys.Any(y => v.Key.Split('~')[1].Matches(y.Split("~")[1])) ||
                                 sqlNode.JoinOneKeys.Any(y => v.Key.Split('~')[1].Matches(y.From.Split("~")[1]))) ||
                               sqlNode.JoinOneKeys.Any(y => v.Key.Split('~')[1].Matches(y.To.Split("~")[1]))))
        {
            entity.Value.Value = sqlNode.Value;
            entity.Value.SqlNodeType = SqlNodeType.Mutation;
            if (sqlStatementNodes.ContainsKey(entity.Value.RelationshipKey) &&
                entities.Contains(entity.Value.RelationshipKey.Split("~")[0]))
            {
                sqlStatementNodes[entity.Value.RelationshipKey] = entity.Value;
            }

            if (!sqlStatementNodes.ContainsKey(entity.Value.RelationshipKey) &&
                entities.Contains(entity.Value.RelationshipKey.Split("~")[0]))
            {
                sqlStatementNodes.Add(entity.Value.RelationshipKey, entity.Value);
            }
        }
    }

    /// <summary>
    /// Method for adding a value into a dictionary
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="values"></param>
    private static void AddToDictionary(Dictionary<string, string> dictionary,
        List<string> values, string field, Dictionary<string, NodeTree> trees)
    {
        var entitiesWithColumn = trees.Values.Where(a => a.Mapping.Any(b => b.FieldDestinationName.Matches(field))).ToList();

        foreach (var entity in entitiesWithColumn)
        {
            foreach (var value in values)
            {
                if (!dictionary.TryGetValue(entity.Name, out var _))
                {
                    dictionary.Add(entity.Name, value);
                }
                else
                {
                    dictionary[entity.Name] += " " + value;
                }
            }
        }
    }

    /// <summary>
    /// Method for creating order clause based on fields
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="orderNode"></param>
    /// <param name="entity"></param>
    /// <param name="wrapperEntity"></param>
    /// <param name="rootEntity"></param>
    /// <param name="linkModelDictionaryTree"></param>
    /// <returns></returns>
    public static string GetFieldsOrdering(Dictionary<string, NodeTree> trees,
        ISyntaxNode orderNode, string entity,
        string wrapperEntity, string rootEntity, Dictionary<string, SqlNode> linkModelDictionaryTree)
    {
        var orderString = string.Empty;
        foreach (var oNode in orderNode.GetNodes())
        {
            var currentEntity = entity;
            if (oNode.ToString().Contains("{") && oNode.ToString()[0] != '{' &&
                oNode.ToString().Contains(":"))
            {
                currentEntity = oNode.ToString().Split(":")[0];
            }

            if (!oNode.ToString().Contains("{") && oNode.ToString().Contains(":"))
            {
                var column = oNode.ToString().Split(":");
                if ((column[1].Contains("DESC") || column[1].Contains("ASC")) &&
                    trees.ContainsKey(currentEntity))
                {
                    currentEntity = currentEntity.Matches(wrapperEntity) ? rootEntity :
                        currentEntity;
                    var currentNodeTree = trees[currentEntity];
                    orderString +=
                        SqlGraphQLHelper.HandleSort(currentNodeTree, column[0],
                            column[1], linkModelDictionaryTree);
                }
            }

            orderString +=
                $", {GetFieldsOrdering(trees, oNode, wrapperEntity, rootEntity, currentEntity,
                    linkModelDictionaryTree)}";
        }

        return orderString;
    }

    /// <summary>
    /// Method for creating where clause based on fields
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="linkEntityDictionaryTree"></param>
    /// <param name="linkModelDictionaryTree"></param>
    /// <param name="whereFields"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <param name="whereNode"></param>
    /// <param name="entityName"></param>
    /// <param name="rootEntityName"></param>
    /// <param name="wrapperEntityName"></param>
    /// <param name="clauseType"></param>
    /// <param name="permission"></param>
    public static void GetFieldsWhere(Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode> linkEntityDictionaryTreeNode,
        Dictionary<string, SqlNode> linkModelDictionaryTreeNode, List<string> whereFields,
        Dictionary<string, string> sqlWhereStatement,
        ISyntaxNode whereNode, string entityName, string rootEntityName, string wrapperEntityName,
        string clauseCondition,
        List<string> clauseType,
        Dictionary<string, List<string>> permission = null)
    {
        if (whereNode == null || string.IsNullOrWhiteSpace(entityName))
        {
            return;
        }

        foreach (var wNode in whereNode.GetNodes())
        {
            if (wrapperEntityName.Matches(entityName))
            {
                entityName = rootEntityName;
            }

            var currentEntity = entityName;

            currentEntity = trees.Keys.FirstOrDefault(e => e.ToString()
                .Matches(wNode.ToString().Split(":")[0]));

            if (string.IsNullOrEmpty(currentEntity) || currentEntity.Matches(rootEntityName))
            {
                currentEntity = entityName;
            }

            if (whereNode.ToString().TrimStart(' ').StartsWith("and:") ||
                whereNode.ToString().TrimStart(' ').StartsWith("or:"))
            {
                clauseCondition = whereNode.ToString().Split("{")[0].Replace(":", "").ToUpper();
            }

            if (wNode.ToString().Contains("{") && wNode.ToString().Contains(":") &&
                wNode.ToString().Split(":").Length == 3)
            {
                var column = wNode.ToString().Split(":")[0];

                if (!column.Contains("{"))
                {
                    if (linkModelDictionaryTreeNode.TryGetValue($"{currentEntity}~{column}",
                            out var currentKeyValueNode))
                    {
                        var fieldValue = currentKeyValueNode.RelationshipKey.Replace('~', '.');
                        currentEntity = $"{currentKeyValueNode.RelationshipKey.Split('~')[0]}";
                        whereFields.Add(fieldValue);
                    }
                }
            }

            foreach (var node in wNode.GetNodes().ToList())
            {
                if (!node.ToString().Contains("{") && node.ToString().Contains(":") &&
                    node.ToString().Split(":").Length == 2)
                {
                    var column = node.ToString().Split(":");
                    if (!column[1].Contains("DESC") && !column[1].Contains("ASC") &&
                        clauseType.Contains(column[0]))
                    {
                        if (whereFields.Count == 0)
                        {
                            continue;
                        }

                        var clauseValue = column[1].Trim().Trim('"');
                        var fieldParts = whereFields.Last().Split('.');
                        var currentNodeTree = trees[currentEntity];
                        var field = fieldParts[1];

                        switch (column[0])
                        {
                            case "eq":
                                {
                                    var clause = SqlGraphQLHelper
                                        .ProcessFilter(currentNodeTree, linkEntityDictionaryTreeNode,
                                        field, "=",
                                        clauseValue, clauseCondition);
                                    AddToDictionary(sqlWhereStatement, clause, field, trees);
                                    break;
                                }
                            case "neq":
                                {
                                    var clause = SqlGraphQLHelper
                                        .ProcessFilter(currentNodeTree, linkEntityDictionaryTreeNode, field, "<>", clauseValue, clauseCondition);
                                    AddToDictionary(sqlWhereStatement, clause, field, trees);
                                    break;
                                }
                            case "in":
                                {
                                    clauseValue = "(" + string.Join(',',
                                        column[1].Replace("[", "").Replace("]", "").Split(',')
                                            .Select(v => $"'{v.Trim()}'")) + ")";
                                    var clause = SqlGraphQLHelper
                                        .ProcessFilter(currentNodeTree, linkEntityDictionaryTreeNode,
                                        field, "in", clauseValue, clauseCondition);
                                    AddToDictionary(sqlWhereStatement, clause, field, trees);
                                    break;
                                }
                        }

                        clauseCondition = string.Empty;
                    }
                }
            }

            GetFieldsWhere(trees, linkEntityDictionaryTreeNode, linkModelDictionaryTreeNode,
                whereFields,
                sqlWhereStatement,
                wNode,
                currentEntity, rootEntityName, wrapperEntityName, clauseCondition, clauseType, permission);
        }
    }
}