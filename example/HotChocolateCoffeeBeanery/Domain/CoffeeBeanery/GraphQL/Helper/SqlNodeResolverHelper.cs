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
        IModelTreeMap<D, S> modelTreeMap, string rootModelName, string wrapperEntityName,
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
        var tranformedModel = rootModelName;
        var rootEntityName = rootModelName;
        
        while (!entityTreeMap.EntityNames.Contains(rootModelName) || rootModelName.Matches(wrapperEntityName))
        {
            rootEntityName = modelTreeMap.DictionaryTree[rootModelName].ParentName;
            transformedToParent = true;
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
                            sqlUpsertStatementNodes, modelTreeMap.DictionaryTree[rootModelName], string.Empty,
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
                        sqlUpsertStatementNodes, modelTreeMap.DictionaryTree[rootModelName], string.Empty,
                        new NodeTree(), models, modelTreeMap.EntityNames, visitedModels);
                    
                    SqlHelper.GenerateUpsertStatements(entityTreeMap.DictionaryTree, entityTreeMap.LinkDictionaryTreeMutation, rootEntityName,
                        wrapperEntityName, generatedQuery, sqlUpsertStatementNodes, entityTreeMap.DictionaryTree[rootEntityName], entityTreeMap.EntityNames,
                        sqlWhereStatement, new List<string>());
                }

                var statement = generatedQuery.Values.Order().ToList();
                sqlUpsertStatement = @"LOAD 'age';
                    SET search_path = ag_catalog, ""$user"", public; " + string.Join(";", statement);
            }
        }

        // Query Select
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
                entityTreeMap.DictionaryTree[rootEntityName],
                new NodeTree(), visitedFieldModel, models, rootModelName, modelTreeMap.EntityNames,
                true);
        }

        if (graphQlSelection.SelectionSet?.Selections!
                .FirstOrDefault(s => s.ToString().StartsWith("nodes")) != null)
        {
            GetFields(modelTreeMap.DictionaryTree, node,
                entityTreeMap.LinkDictionaryTreeNode, modelTreeMap.LinkDictionaryTreeNode,
                sqlStatementNodes,
                entityTreeMap.DictionaryTree[rootEntityName],
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
                modelTreeMap.LinkDictionaryTreeNode,
                sqlQueryStatement, sqlStatementNodes, sqlWhereStatement,
                entityTreeMap.DictionaryTree[rootEntityName], entityTreeMap.EntityNames,
                childrenSqlStatement, sqlQueryStructures,
                splitOnDapper, entityOrder, rootEntityName, new List<string>());
            
            //if transformedToParent then will be used the first matching child, TODO: Support multiple child queries for complex entities

            var queryStructure = sqlQueryStructures.FirstOrDefault();

            if (queryStructure.Value == null)
            {
                return new SqlStructure();
            }
            
            if (splitOnDapper.Count == 0)
            {
                splitOnDapper.Add(queryStructure.Key, entityTypes
                    .First(a => a.Name.Matches(queryStructure.Key)));
            }
            else
            {
                if (splitOnDapper.Count > 0 && transformedToParent && !rootEntityName.Matches(wrapperEntityName))
                {
                    var key = splitOnDapper.FirstOrDefault(a => a.Value.Name == rootEntityName).Key;

                    if (!string.IsNullOrEmpty(key))
                    {
                        splitOnDapper.Remove(key);
                    }
                
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
                    }
                }

                splitOnDapper = splitOnDapper.Where(a => a.Value != null)
                    .ToDictionary(a => a.Key, a => a.Value);
            }

            sqlSelectStatement = queryStructure.Value.Query;
            
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
    
    /// <summary>
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
    private static void
        GenerateQuery(Dictionary<string, NodeTree> entityTrees,
            List<Type> entityTypes,
            Dictionary<string, SqlNode> linkEntityDictionaryTreeNode,
            Dictionary<string, SqlNode> linkModelDictionaryTreeNode,
            StringBuilder sqlQueryStatement, Dictionary<string, SqlNode> sqlStatementNodes,
            Dictionary<string, string> sqlWhereStatement,
            NodeTree currentTree, List<string> entityNames, Dictionary<string, string> childrenSqlStatement,
            Dictionary<string, SqlQueryStructure> sqlQueryStructures,
            Dictionary<string, Type> splitOnDapper, List<string> entityOrder, string rootEntityName,
            List<string> processedGraph)
    {
        var hasChildren = false;
        var currentColumns = new List<KeyValuePair<string,SqlNode>>();
            
        currentColumns.AddRange(sqlStatementNodes
            .Where(k =>
                !entityNames.Any(a => a.Matches(k.Key.Split('~')[1])) &&
                (currentTree.Mapping.Any(m => m.FieldDestinationName
                     .Matches(k.Key.Split('~')[1])) && 
                 !k.Key.Matches($"{currentTree.Name}~Id"))).ToList());

        if (currentColumns.Count == 0)
        {
            foreach (var child in currentTree.Children)
            {
                currentColumns.AddRange(sqlStatementNodes
                    .Where(k =>
                        !entityNames.Any(a => a.Matches(k.Key.Split('~')[1])) &&
                        (child.Mapping.Any(m => m.FieldDestinationName
                             .Matches(k.Key.Split('~')[1])) && 
                         !k.Key.Matches($"{child.Name}~Id"))).ToList());

                if (currentColumns.Count > 0)
                {
                    currentTree = child;
                    break;
                }
            }
        }
        
        var currentEntityStructure = GenerateEntityQuery(entityTrees,
            linkEntityDictionaryTreeNode, linkModelDictionaryTreeNode,
            sqlStatementNodes, currentTree, entityNames, sqlQueryStatement,
            sqlQueryStructures, sqlWhereStatement, childrenSqlStatement, rootEntityName);

        var queryBuilder = $"SELECT % FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name} ";
        currentEntityStructure.SelectColumns.AddRange(currentEntityStructure.Columns);
        currentEntityStructure.SelectColumns = currentEntityStructure.SelectColumns.Distinct().ToList();

        currentTree = entityTrees[currentTree.Name];
        entityOrder.Add(currentTree.Name);
        
        var hasActiveChildren = false;
        int index = 0;
        
        foreach (var child in currentTree.Children)
        {
            if (currentTree.ChildrenName.Any(k => k.Matches(child.Name)))
            {
                if (currentEntityStructure.SqlNode == null)
                {
                    continue;
                }
                
                foreach (var linkKey in currentEntityStructure.SqlNode.LinkKeys.Where(a =>
                             a.To.Split('~')[0].Matches(child.Name)))
                {
                    hasActiveChildren = true;
                    GenerateQuery(entityTrees, entityTypes, linkEntityDictionaryTreeNode, linkModelDictionaryTreeNode,
                        sqlQueryStatement, sqlStatementNodes, sqlWhereStatement,
                        child, entityNames, childrenSqlStatement, sqlQueryStructures,
                        splitOnDapper, entityOrder, rootEntityName, processedGraph);
                
                    if (sqlQueryStructures.TryGetValue(child.Name, out var childStructure) && childStructure.Columns.Count > 0)
                    {
                        var linkKeyPart = currentEntityStructure.SqlNode.LinkKeys.First(a => a.From.Matches(linkKey.From));
                        
                        if (!string.IsNullOrEmpty(childStructure.GraphQuery) && !processedGraph.Contains(childStructure.GraphQuery))
                        {
                            processedGraph.Add(childStructure.GraphQuery);
                            queryBuilder += childStructure.SqlNodeType == SqlNodeType.Edge ? " JOIN " : " LEFT JOIN ";
                            queryBuilder +=
                                $" ( {childStructure.GraphQuery} )) {child.Name}{currentTree.Id}{index} ON {currentTree.Name}.\"{
                                    linkKeyPart.FromId.Split('~')[1]}\" = {child.Name}{currentTree.Id}{index}.{linkKeyPart.FromId.Split('~')[1]}";
                            index++;
                        }
                        
                        queryBuilder += childStructure.SqlNodeType == SqlNodeType.Edge ? " JOIN " : " LEFT JOIN ";
                        
                        queryBuilder +=
                        $" ( {childStructure.Query} ) {child.Name}{currentTree.Id}{index} ON {currentTree.Name}.\"{
                            linkKeyPart.FromId.Split('~')[1]}\" = {
                        child.Name}{currentTree.Id}{index}.\"{linkKeyPart.FromId.Split('~')[1].ToSnakeCase(child.Id)}\"";  

                        currentEntityStructure.SelectColumns.AddRange(
                            childStructure.ParentColumns.Select(s => s.Replace("~", $"{child.Name}{currentTree.Id}{index}")));
                        currentEntityStructure.ParentColumns.AddRange(childStructure.ParentColumns);

                        currentEntityStructure.SelectColumns = currentEntityStructure.SelectColumns.Distinct().ToList();
                        currentEntityStructure.ParentColumns = currentEntityStructure.ParentColumns.Distinct().ToList();

                        if (!splitOnDapper.ContainsKey(linkKeyPart.To.Split('~')[1].ToSnakeCase(child.Id)))
                        {
                            splitOnDapper.Add(linkKeyPart.To.Split('~')[1].ToSnakeCase(child.Id),
                                entityTypes.FirstOrDefault(e => e.Name.Matches(child.Name)));
                        }
                        index++;
                    }
                }
                
                // foreach (var joinOneKey in currentEntityStructure.SqlNode.JoinOneKeys.Where(a =>
                //              a.To.Split('~')[0].Matches(child.Name)))
                // {
                //     hasActiveChildren = true;
                //     var isQueryGenerated = GenerateQuery(entityTrees, entityTypes, linkEntityDictionaryTreeNode,
                //         sqlQueryStatement, sqlStatementNodes, sqlWhereStatement,
                //         child, entityNames, childrenSqlStatement, sqlQueryStructures,
                //         splitOnDapper, entityOrder, rootEntityName);
                //
                //     if (isQueryGenerated && sqlQueryStructures.TryGetValue(child.Name, out var childStructure) && childStructure.Columns.Count > 0)
                //     {
                //         queryBuilder += childStructure.SqlNodeType == SqlNodeType.Edge ? " JOIN " : " LEFT JOIN ";
                //
                //         var joinOneKeyPart = currentEntityStructure.SqlNode.JoinOneKeys.First(a => a.From.Matches(joinOneKey.From));
                //         var childOneKeyPart = childStructure.SqlNode.JoinOneKeys.First(a => a.To.Matches(joinOneKey.From));
                //         queryBuilder +=
                //             $" ( {childStructure.Query} ) {child.Name} ON {currentTree.Name}.\"{joinOneKeyPart.To.Split('~')[1]}\" = {child.Name}.\"{childOneKeyPart.From.Split('~')[1]}\"";    
                //
                //         currentEntityStructure.SelectColumns.AddRange(
                //             childStructure.ParentColumns.Select(s => s.Replace("~", child.Name)));
                //         currentEntityStructure.ParentColumns.AddRange(childStructure.ParentColumns);
                //
                //         currentEntityStructure.SelectColumns = currentEntityStructure.SelectColumns.Distinct().ToList();
                //         currentEntityStructure.ParentColumns = currentEntityStructure.ParentColumns.Distinct().ToList();
                //
                //         if (!splitOnDapper.ContainsKey("Id".ToSnakeCase(child.Id)))
                //         {
                //             splitOnDapper.Add("Id".ToSnakeCase(child.Id),
                //                 entityTypes.FirstOrDefault(e => e.Name.Matches(child.Name)));
                //         }    
                //     }
                // }
            }
        }

        if (hasActiveChildren)
        {
            var select = string.Join(",", currentEntityStructure.SelectColumns.DistinctBy(a => a.Split(" AS ")[1]));

            queryBuilder = queryBuilder.Replace("%", select);
            queryBuilder += " " + currentEntityStructure.WhereClause;

            currentEntityStructure.Query = queryBuilder;
            var currentNode = linkEntityDictionaryTreeNode.FirstOrDefault(a =>
                a.Key.Contains(currentTree.Name));
            currentEntityStructure.Id = currentTree.Id;
            currentEntityStructure.SqlNodeType = currentNode.Value.SqlNodeType;
            currentEntityStructure.SqlNode = currentNode.Value;

            if (sqlQueryStructures.TryGetValue(currentTree.Name, out var sqlQueryStructure))
            {
                sqlQueryStructures[currentTree.Name] = currentEntityStructure;
            }
            else
            {
                sqlQueryStructures.Add(currentTree.Name, currentEntityStructure);
            }

            if (!splitOnDapper.ContainsKey("Id".ToSnakeCase(currentTree.Id)))
            {
                splitOnDapper.Add("Id".ToSnakeCase(currentTree.Id),
                    entityTypes.FirstOrDefault(e => e.Name.Matches(currentTree.Name)));
            }
        }
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
        Dictionary<string, SqlNode> linkModelDictionaryTreeNode,
        Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree, List<string> entityNames,
        StringBuilder sqlQueryStatement, Dictionary<string, SqlQueryStructure> sqlQueryStructures,
        Dictionary<string, string> sqlWhereStatement, Dictionary<string, string> childrenSqlStatement,
        string rootEntity)
    {
        var currentColumns = new List<KeyValuePair<string, SqlNode>>();
        var childrenJoinColumns = new Dictionary<string, string>();
        var entitySql = string.Empty;
        var graphSql = string.Empty;
        var graphColumns = new List<KeyValuePair<string, SqlNode>>();
        var upsertColumn = new KeyValuePair<string, SqlNode>();

        currentColumns.AddRange(sqlStatementNodes
            .Where(k =>
                        !entityNames.Any(a => a.Matches(k.Key.Split('~')[1])) &&
                        (currentTree.Mapping.Any(m => m.FieldDestinationName
                             .Matches(k.Key.Split('~')[1])) && 
                         !k.Key.Matches($"{currentTree.Name}~Id"))).ToList());

        if (currentColumns.Count == 0)
        {
            foreach (var child in currentTree.Children)
            {
                currentColumns.AddRange(sqlStatementNodes
                    .Where(k =>
                        !entityNames.Any(a => a.Matches(k.Key.Split('~')[1])) &&
                        (child.Mapping.Any(m => m.FieldDestinationName
                             .Matches(k.Key.Split('~')[1])) && 
                         !k.Key.Matches($"{child.Name}~Id"))).ToList());

                if (currentColumns.Count > 0)
                {
                    currentTree = child;
                    break;
                }
            }
        }

        if (currentColumns.Count == 0)
        {
            return new SqlQueryStructure();
        }
        
        var sqlNodeId = currentColumns.Last().Value;
        sqlNodeId.Column = "Id";
        var columnToAdd = new KeyValuePair<string, SqlNode>($"{currentTree.Name}~Id", sqlNodeId);
        currentColumns.Insert(0, columnToAdd);
        currentColumns = currentColumns.Distinct().ToList();

        var queryBuilder = string.Empty;
        var queryColumns = new List<string>();
        var parentQueryColumns = new List<string>();
        var upsertKey = linkEntityDictionaryTreeNode.FirstOrDefault(a =>
            a.Value.Entity.Matches(currentTree.Name) &&
            a.Value.UpsertKeys.First().Split('~')[1].Matches(a.Value.Column));

        foreach (var tableColumn in currentColumns)
        {
            var column = tableColumn.Key.Split('~')[1];
            
            var linkKey = tableColumn.Value.LinkKeys.FirstOrDefault(a => tableColumn.Value.Column.Matches(a.To.Split('~')[1]));
            
            if (!string.IsNullOrEmpty(linkKey?.From))
            {
                column = linkKey.FromId.Split('~')[1];
            }
            else
            {
                column = tableColumn.Value.Column;
            }
            
            queryColumns.Add(
                $"{currentTree.Name}.\"{column}\" AS \"{column
                    .ToSnakeCase(currentTree.Id)}\"");

            parentQueryColumns.Add(
                    $"~.\"{column.ToSnakeCase(currentTree.Id)}\" AS \"{column.ToSnakeCase(currentTree.Id)}\"");
        }

        foreach (var childQuery in sqlQueryStructures
                     .Where(c =>
                     currentTree.Children
                         .Any(b => b.Name.Matches(c.Key))))
        {
            queryBuilder += $" {(childQuery.Value.SqlNodeType == SqlNodeType.Edge ?
                " JOIN ( " : " LEFT JOIN  ( ")} {childQuery.Value.Query}";
            
            var joinChildKey = $"\"{"Id"
                .ToSnakeCase(childQuery.Value.Id)}\"";

            // if (currentColumns.Count > 0)
            // {
            //     var linkKeys = currentColumns[0].Value.JoinKeys
            //         .Where(k => k.To.Matches(childQuery.Key)).ToList();
            //
            //     if (linkKeys.Count == 0)
            //     {
            //         for (var i = 0; i < linkKeys.Count; i++)
            //         {
            //             if (i == 0)
            //             {
            //                 queryBuilder +=
            //                     $" ) {childQuery.Key} ON {currentTree.Name}.\"Id\" = {childQuery.Key}.{joinChildKey}";
            //             }
            //             else
            //             {
            //                 queryBuilder +=
            //                     $" AND {childQuery.Key} ON {currentTree.Name}.\"Id\" = {childQuery.Key}.{joinChildKey}";
            //             }
            //         }
            //     }
            // }
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
            
            upsertColumn = currentColumns.FirstOrDefault(a => 
                a.Key.Split('~')[0].Matches(currentTree.Name));
            
            if (currentColumns.Any() && graphColumns.Any(a => a.Value.IsGraph) && 
                graphColumns.Count == linkEntityDictionaryTreeNode.Count(a => a.Value.Graph
                    .Matches(graphColumns.First().Value.Graph)))
            {
                graphSql = $"WITH graph_data AS ( SELECT {(string.Join(",", currentColumns.Where(a =>  a.Value.UpsertKeys.Count > 0)
                    .Select(a => $"{a.Value.Column}::TEXT::{GetColumnType(a.Value.ColumnType)} AS {a.Value.Column}").Distinct().ToList()))} FROM cypher('{currentColumns.First(a => !string.IsNullOrEmpty(a.Value.Graph)).Value.Graph}', $$ MATCH " +
                           $"(p:{currentTree.Name} ) RETURN {
                               $"{(string.Join(",", currentColumns.Where(a =>  a.Value.UpsertKeys.Count > 0)
                                   .Select(a => $"(p.{a.Value.Column}) AS {a.Value.Column}").Distinct().ToList()))}"} $$) AS ({$"{
                                       (string.Join(",", currentColumns.Where(a =>  a.Value.UpsertKeys.Count > 0)
                                           .Select(a => $"{a.Value.Column} agtype").Distinct().ToList()))} )) "}" + 
                           $"SELECT * FROM (SELECT {(string.Join(",", currentColumns.Where(a =>  a.Value.UpsertKeys.Count > 0)
                                   .Select(a => $" (a.{a.Value.Column})::{GetColumnType(a.Value.ColumnType)} AS {a.Value.Column} ").Distinct().ToList()))
                           } FROM graph_data a ";
                
                var graphColumnTree = entityTrees[upsertColumn.Value.Entity];
                
                graphSql += $" JOIN \"{graphColumnTree.Schema}\".\"{graphColumnTree.Name}\" {
                    upsertColumn.Value.Entity}{upsertColumn.Value.Column} ON a.{upsertColumn.Value.Column} = {
                        upsertColumn.Value.Entity}{upsertColumn.Value.Column}.\"{upsertColumn.Value.Column}\" ";

                var parentFieldGraph = graphColumns.First();
                var parentGraph = parentFieldGraph.Value.LinkKeys.First(a => !a.From.Matches(parentFieldGraph.Key));
                
                foreach (var column in graphColumns)
                {
                    foreach (var joinKey in column.Value.LinkKeys)
                    {
                        if (!joinKey.To.Split('~')[1].Matches(column.Value.Column))
                        {
                            continue;
                        }

                        graphColumnTree = entityTrees[joinKey.From.Split('~')[0]];
                        upsertKey = linkEntityDictionaryTreeNode.First(a => 
                            a.Value.Entity.Matches(graphColumnTree.Name));

                        graphSql += $" JOIN \"{graphColumnTree.Schema}\".\"{graphColumnTree.Name}\" {
                            parentGraph.From.Split('~')[0]}{joinKey.To.Split('~')[1]} ON a.{
                                joinKey.To.Split('~')[1]} = {
                                    parentGraph.From.Split('~')[0]}{joinKey.To.Split('~')[1]}.\"{parentGraph.From.Split('~')[1]}\" ";
                    }
                }
            }
            
            sqlQueryStatement.Append(queryBuilder);
            queryBuilder = "";
            queryBuilder += " SELECT % ";
            queryBuilder += $" FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name}";

            var model = linkEntityDictionaryTreeNode
                .FirstOrDefault(e =>
                e.Key.Split('~')[0].Matches(currentTree.Name));

            var modelValue = string.Empty;
            if (model.Value != null)
            {
                modelValue = model.Value?.Mapping?.FirstOrDefault(m =>
                    m.DestinationEntity.Matches(currentTree.Name))?.SourceModel ?? string.Empty;
            }

            if (string.IsNullOrEmpty(modelValue))
            {
                modelValue = currentTree.Name;
            }

            if (sqlWhereStatement.TryGetValue(modelValue, out var currentSqlWhereStatement))
            {
                currentSqlWhereStatement = currentSqlWhereStatement.Replace("~", currentTree.Name);
                entitySqlWhereStatement = $" WHERE {currentSqlWhereStatement} ";
            }
            else
            {
                entitySqlWhereStatement = string.Empty;
            }
        }

        var joinOneKey = new JoinOneKey();
        // var currentJoinOneKeys = currentColumns.FirstOrDefault(a => a.Key.Split('~')[0].Matches(currentTree.Name)).Value
        //     .JoinOneKeys;

        // if (currentJoinOneKeys.Count() > 0
        //     &&
        //     currentJoinOneKeys[0].From.Split('~')[0].Matches(currentTree.Name))
        // {
        //     joinOneKey = currentJoinOneKeys.FirstOrDefault();
        //     
        //     var oneKey = $"{currentTree.Name}.\"{joinOneKey.To.Split('~')[1]}\" AS \"{joinOneKey.To.Split('~')[0]}{joinOneKey
        //         .To.Split('~')[1].ToSnakeCase(currentTree.Id)}\"";
        //     var joinOneKeyParent = $"~.\"{joinOneKey.To.Split('~')[1].ToSnakeCase(currentTree.Id)}\" AS \"{joinOneKey
        //         .To.Split('~')[0]}{joinOneKey.To.Split('~')[1].ToSnakeCase(currentTree.Id)}\"";
        //     queryColumns.Add(oneKey);
        //     if (!parentQueryColumns.Contains($"\"{joinOneKey.To.Split('~')[0]}{joinOneKey.To.Split('~')[1]
        //         .ToSnakeCase(currentTree.Id)}\""))
        //     {
        //         parentQueryColumns.Add(joinOneKeyParent);
        //     }
        // }

        queryColumns = queryColumns.Distinct().ToList();
        parentQueryColumns = parentQueryColumns.Distinct().ToList();
        var select = string.Join(",", queryColumns);
        queryBuilder = queryBuilder.Replace("%", select);
        
        // if (!string.IsNullOrEmpty(graphSql))
        // {
        //     var parentFieldGraph = graphColumns.First();
        //     var parentGraph = parentFieldGraph.Value.Graph;
        //
        //     // entitySql = $" ;{graphSql}) a";
        //     //         $"ON {parentGraph.To.Replace("~","")}.\"{
        //     // currentColumns.Last().Value.UpsertKeys.First().Split('~')[1]}\" = a.{
        //     //     currentColumns.Last().Value.UpsertKeys.First().Split('~')[1]} ";
        // }
        queryBuilder += entitySql;

        var sqlStructure = new SqlQueryStructure()
        {
            Id = currentTree.Id,
            SqlNodeType = currentColumns.Count > 0 ? currentColumns.Last().Value.SqlNodeType :
                SqlNodeType.Node,
            SqlNode = upsertKey.Value,
            GraphQuery = graphSql,
            Query = queryBuilder,
            Columns = queryColumns,
            ParentColumns = parentQueryColumns,
            ChildrenJoinColumns = childrenJoinColumns,
            WhereClause = entitySqlWhereStatement
        };

        if (!sqlQueryStructures.Any(a => a.Key
                .Matches(currentTree.Name)))
        {
            sqlQueryStructures.Add(currentTree.Name, sqlStructure);
        }

        if (currentTree.Name.Matches(rootEntity))
        {
            var addingMissingUpsertKeys = linkEntityDictionaryTreeNode
                .First(c => c.Key.Split('~')[0].Matches(currentTree.Name))
                .Value.UpsertKeys.Where(u => !sqlStructure.Columns.Any(a => a
                    .Matches($"{currentTree.Name}.\"Id\" AS \"{u.Split('~')[1].ToSnakeCase(currentTree.Id)}\"")))
                    .Select(a => $"{currentTree.Name}.\"{a.Split('~')[1]}\" AS \"{a.Split('~')[1].ToSnakeCase(currentTree.Id)}\"");

            var addingMissingUpsertKeysParent = linkEntityDictionaryTreeNode
                .First(c => c.Key.Split('~')[0].Matches(currentTree.Name))
                .Value.UpsertKeys.Where(u => !sqlStructure.Columns.Any(a => a
                    .Matches($"{currentTree.Name}.\"Id\" AS \"{u.Split('~')[1].ToSnakeCase(entityTrees[currentTree.Name].Id)}\"")))
                .Select(a => $"{currentTree.Name}.\"{a.Split('~')[1].ToSnakeCase(entityTrees[currentTree.Name]
                    .Id)}\" AS \"{a.Split('~')[1].ToSnakeCase(entityTrees[currentTree.Name].Id)}\"");

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

    public static string GetColumnType(string propertyType)
    {
        switch (propertyType.ToLower())
        {
            case "int":
                return "INT";
            case "guid":
                return "UUID";
        }

        return "INT";
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
            if (linkModelDictionaryTree.TryGetValue($"{currentTree.Name}~{previousNode.Split(':')[0]}",
                    out var sqlNodeFrom)
                )
            {
                if (linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey,
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
                //
                // if (previousNode.Split(':').Length == 2)
                // {
                //     if (sqlNodeFrom.ToEnumeration.TryGetValue(previousNode.Split(':')[1]
                //                 .Sanitize().Replace("_", ""),
                //             out var enumValue))
                //     {
                //         sqlNodeFrom.Value = enumValue;
                //     }
                //     else
                //     {
                //         sqlNodeFrom.Value = previousNode.Split(':')[1].Sanitize();
                //     }
                // }
                //
                // AddEntity(linkEntityDictionaryTree, sqlStatementNodes, trees,
                //     sqlNodeFrom);
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
            entity.Value.Value = sqlNode.Value;
            
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

            if (linkModelDictionaryTree.TryGetValue($"{currentTree.Name}~{node.ToString()}",
                    out var sqlNodeFrom) ||
                linkModelDictionaryTree.TryGetValue($"{currentModel}~{node.ToString()}",
                    out sqlNodeFrom)
               )
            {
                if (linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey,
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
                            sqlNode.UpsertKeys.Any(y => v.Key.Split('~')[1].Matches(y.Split("~")[1]))))) 
                            // ||
                            //      sqlNode.JoinOneKeys.Any(y => v.Key.Split('~')[1].Matches(y.From.Split("~")[1]))) ||
                            //    sqlNode.JoinOneKeys.Any(y => v.Key.Split('~')[1].Matches(y.To.Split("~")[1]))))
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