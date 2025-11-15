using System.Text;
using Dapper;
using CoffeeBeanery.GraphQL.Extension;
using HotChocolate.Language;
using CoffeeBeanery.GraphQL.Model;
using FASTER.core;
using HotChocolate.Execution.Processing;

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

        //Where conditions
        GetFieldsWhere(modelTreeMap.DictionaryTree, entityTreeMap.LinkDictionaryTree, 
            modelTreeMap.LinkDictionaryTree,
            whereFields, sqlWhereStatement, graphQlSelection.SyntaxNode.Arguments
                .FirstOrDefault(a => a.Name.Value.Matches("where")),
            modelTreeMap.DictionaryTree.Last().Value.Name, rootEntityName, wrapperEntityName,
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
                        wrapperEntityName, rootEntityName, modelTreeMap.LinkDictionaryTree);
                }
            }

            var sqlUpsertStatementNodes = new Dictionary<string, SqlNode>();
            var visitedModels = new List<string>();

            if (argument.Name.Value.Matches(wrapperEntityName))
            {
                var nodeTreeRoot = new NodeTree();
                nodeTreeRoot.Name = string.Empty;

                foreach (var mutationNode in argument.Value.GetNodes()
                             .First(a => !a.ToString().Contains("cache") && !a.ToString().Contains("model")).GetNodes().ToList().First(a => a.ToString().Contains("{")).GetNodes().ToList())
                {
                    GetMutations(modelTreeMap.DictionaryTree, mutationNode,
                        entityTreeMap.LinkDictionaryTree, modelTreeMap.LinkDictionaryTree,
                        sqlUpsertStatementNodes, modelTreeMap.DictionaryTree
                            .First(t =>
                                t.Key.Matches(rootEntityName)).Value, string.Empty,
                        new NodeTree(), models, modelTreeMap.EntityNames, visitedModels);

                    sqlUpsertStatement += " " + SqlHelper.GenerateUpsertStatements(entityTreeMap.DictionaryTree,
                        sqlUpsertStatementNodes, modelTreeMap.EntityNames, sqlWhereStatement);
                }
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
                entityTreeMap.LinkDictionaryTree, modelTreeMap.LinkDictionaryTree,
                sqlStatementNodes,
                modelTreeMap.DictionaryTree.First(t =>
                    t.Key.Matches(rootEntityName)).Value,
                new NodeTree(), visitedFieldModel, models, modelTreeMap.EntityNames,
                true);
        }

        if (graphQlSelection.SelectionSet?.Selections!
                .FirstOrDefault(s => s.ToString().StartsWith("nodes")) != null)
        {
            GetFields(modelTreeMap.DictionaryTree, node,
                entityTreeMap.LinkDictionaryTree, modelTreeMap.LinkDictionaryTree,
                sqlStatementNodes,
                modelTreeMap.DictionaryTree.First(t =>
                    t.Key.Matches(rootEntityName)).Value,
                new NodeTree(), visitedFieldModel, models, modelTreeMap.EntityNames, 
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
                entityTreeMap.LinkDictionaryTree, 
                sqlQueryStatement, sqlStatementNodes, sqlWhereStatement, 
                entityTreeMap.NodeTree.Children[0],
                childrenSqlStatement, entityTreeMap.EntityNames, sqlQueryStructures, 
                splitOnDapper, entityOrder, rootEntityName);

            var queryStructure = sqlQueryStructures.LastOrDefault();
        
            sqlSelectStatement = sqlQueryStructures.LastOrDefault().Value.Query;

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

            if (kv.Value != null)
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
    private static bool
        GenerateQuery(Dictionary<string, NodeTree> entityTrees, 
            List<Type> entityTypes,
            Dictionary<string, SqlNode> linkEntityDictionaryTree,
            StringBuilder sqlQueryStatement, Dictionary<string, SqlNode> sqlStatementNodes,
            Dictionary<string, string> sqlWhereStatement,
            NodeTree currentTree, Dictionary<string, string> childrenSqlStatement,
            List<string> entityNames,
            Dictionary<string, SqlQueryStructure> sqlQueryStructures, 
            Dictionary<string, Type> splitOnDapper, List<string> entityOrder, string rootNodeTree)
    {

        currentTree = entityTrees[currentTree.Name];
        entityOrder.Add(currentTree.Name);

        var hasChildren = false;
        foreach (var child in currentTree.Children)
        {
            if (currentTree.ChildrenName.Any(k => k.Matches(child.Name)))
            {
                hasChildren = GenerateQuery(entityTrees, entityTypes, linkEntityDictionaryTree,
                    sqlQueryStatement, sqlStatementNodes, sqlWhereStatement,
                    child, childrenSqlStatement, entityNames, sqlQueryStructures, 
                    splitOnDapper, entityOrder, rootNodeTree);
            }
        }

        var currentEntityStructure = GenerateEntityQuery(entityTrees, 
            linkEntityDictionaryTree,
            sqlStatementNodes, currentTree, entityNames, sqlQueryStatement,
            sqlQueryStructures, sqlWhereStatement, childrenSqlStatement, rootNodeTree, hasChildren);

        if (currentEntityStructure == null)
        {
            return false;
        }

        var queryBuilder = $"SELECT % FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {
            currentTree.Name} ";
        currentEntityStructure.SelectColumns.AddRange(currentEntityStructure.Columns);
        
        foreach (var child in currentTree.Children)
        {
            if (!string.IsNullOrEmpty(child.Name) && sqlQueryStructures.ContainsKey(child.Name))
            {
                var childStructure = sqlQueryStructures[child.Name];
                if (childStructure.SqlNode?.JoinKeys?.Count > 0)
                {
                    var joinChildKey = String.Empty;
                    var joinParentKey = "\"Id\"";

                    if (child.ChildrenName.Count > 0)
                    {
                        foreach (var childName in child.ChildrenName)
                        {
                            joinChildKey = childStructure.Columns.LastOrDefault(c => 
                                c.Contains($"\"{childName}Id\""));

                            if (string.IsNullOrEmpty(joinChildKey))
                            {
                                joinChildKey =
                                    childStructure.Columns.LastOrDefault(c => 
                                        c.Contains($"\"{currentTree.Name}Id\""));
                            }

                            if (string.IsNullOrEmpty(joinChildKey))
                            {
                                joinChildKey = childStructure.Columns.LastOrDefault(c => 
                                    c.Contains($"\"Id"));
                                joinParentKey = $"\"{child.Name}Id\"";
                            }

                            joinChildKey = joinChildKey.Split("AS").Last().Sanitize();

                            if (!string.IsNullOrEmpty(childStructure.JoinOneKey))
                            {
                                joinChildKey = childStructure.JoinOneKey;
                            }

                            queryBuilder += childStructure.SqlNodeType == SqlNodeType.Edge ? 
                                " JOIN " : " LEFT JOIN ";
                            queryBuilder +=
                                $" ( {childStructure.Query} ) {child.Name} ON {currentTree.Name}" +
                                $".{joinParentKey} = {child.Name}.\"{joinChildKey}\"";
                            currentEntityStructure.SelectColumns.AddRange(
                                childStructure.ParentColumns.Select(s => s
                                    .Replace("~", child.Name)));
                            currentEntityStructure.ParentColumns.AddRange(childStructure.ParentColumns);

                            if (!splitOnDapper.ContainsKey("Id".ToSnakeCase(child.Id)))
                            {
                                splitOnDapper.Add("Id".ToSnakeCase(child.Id),
                                    entityTypes.FirstOrDefault(e => e.Name.Matches(child.Name)));
                            }
                        }
                    }
                    else
                    {
                        queryBuilder += childStructure.SqlNodeType == SqlNodeType.Edge ? " JOIN " : " LEFT JOIN ";
                        queryBuilder +=
                            $" ( {childStructure.Query} ) {child.Name} ON {currentTree.Name}.\"Id\" = {
                                child.Name}.\"{currentTree.Name}{"Id".ToSnakeCase(child.Id)}\"";
                        currentEntityStructure.SelectColumns.AddRange(
                            childStructure.ParentColumns.Select(s => s.Replace("~", child.Name)));
                        currentEntityStructure.ParentColumns.AddRange(childStructure.ParentColumns);

                        if (!splitOnDapper.ContainsKey("Id".ToSnakeCase(child.Id)))
                        {
                            splitOnDapper.Add("Id".ToSnakeCase(child.Id),
                                entityTypes.FirstOrDefault(e => e.Name.Matches(child.Name)));
                        }
                    }
                }

                if (!splitOnDapper.ContainsKey("Id".ToSnakeCase(currentTree.Id)))
                {
                    splitOnDapper.Add("Id".ToSnakeCase(currentTree.Id),
                        entityTypes.FirstOrDefault(e => e.Name.Matches(currentTree.Name)));
                }
            }
        }

        var select = string.Join(",", currentEntityStructure.SelectColumns);

        queryBuilder = queryBuilder.Replace("%", select);
        queryBuilder += " " + currentEntityStructure.WhereClause;

        currentEntityStructure.Query = queryBuilder;
        var currentNode = linkEntityDictionaryTree.FirstOrDefault(a => a.Key.Contains(currentTree.Name));
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
        Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes, NodeTree currentTree, List<string> entityNames,
        StringBuilder sqlQueryStatement, Dictionary<string, SqlQueryStructure> sqlQueryStructures,
        Dictionary<string, string> sqlWhereStatement, Dictionary<string, string> childrenSqlStatement,
        string rootEntity, bool hasChildren)
    {
        var currentColumns = new List<KeyValuePair<string, SqlNode>>();
        var childrenJoinColumns = new Dictionary<string, string>();

        currentColumns.Add(linkEntityDictionaryTree
            .FirstOrDefault(k => k.Key
                .Matches($"{currentTree.Name}~Id")));

        currentColumns.AddRange(sqlStatementNodes
            .Where(k => !entityNames.Contains(k.Value.Column) && 
                        k.Key.Split('~')[0].Matches(currentTree.Name) &&
                        !k.Value.LinkBusinessKeys.Any(b => b.From.Matches(k.Key)) &&
                        (currentTree.Mapping.Any(m => m.FieldDestinationName
                             .Matches(k.Key.Split('~')[1])) &&
                         !k.Key.Matches($"{currentTree.Name}~Id"))).ToList());

        foreach (var joinKey in currentColumns.LastOrDefault().Value.JoinKeys)
        {
            if (currentColumns.Any(c => c.Key
                    .Matches($"{currentTree.Name}~{joinKey.To.Split('~')[0]}Id")) ||
                currentTree.Name.Matches($"{joinKey.To.Split('~')[0]}"))
            {
                continue;
            }

            var aux = currentColumns[0].Value;
            aux.Column = $"{joinKey.To.Split('~')[0]}Id";
            currentColumns.Add(
                new KeyValuePair<string, SqlNode>($"{currentTree.Name}~{joinKey.To
                    .Split('~')[0]}Id", aux));
        }

        var queryBuilder = string.Empty;
        var queryColumns = new List<string>();
        var parentQueryColumns = new List<string>();

        foreach (var tableColumn in currentColumns)
        {
            var tableFieldParts = tableColumn.Key.Split('~');
            var fieldName = tableFieldParts.Length > 1 ? tableFieldParts[1] : tableFieldParts[0];
            queryColumns.Add(
                $"{currentTree.Name}.\"{fieldName}\" AS \"{fieldName
                    .ToSnakeCase(currentTree.Id)}\"");
            parentQueryColumns.Add(
                $"~.\"{fieldName.ToSnakeCase(currentTree.Id)}\" AS \"{
                    fieldName.ToSnakeCase(currentTree.Id)}\"");
        }

        foreach (var childQuery in sqlQueryStructures
                     .Where(c =>
                     currentTree.Children
                         .Any(b => b.Name.Matches(c.Key))))
        {
            queryBuilder += $" {(childQuery.Value.SqlNodeType == SqlNodeType.Edge ? 
                " JOIN ( " : " LEFT JOIN  ( ")} {
                childQuery.Value.Query
            }";

            var joinChildKey = $"\"{currentTree.ParentName}{"Id"
                .ToSnakeCase(childQuery.Value.Id)}\"";
            if (!childrenJoinColumns.ContainsKey($"{childQuery.Key.Split('~')[0]}"))
            {
                childrenJoinColumns.Add($"{currentTree.Name}~{childQuery.Key.Split('~')[0]}",
                    $"\"{currentTree.ParentName}{"Id".ToSnakeCase(childQuery.Value.Id)}\"");
            }

            if (childQuery.Value.SqlNode?.JoinKeys != null)
            {
                var joinKeys = childQuery.Value.SqlNode.JoinKeys
                    .Where(j => j.From.Matches(currentTree.Name)).ToList();

                for (var i = 0; i < joinKeys.Count; i++)
                {
                    if (i == 0)
                    {
                        queryBuilder +=
                            $" ) {childQuery.Key} ON {currentTree.Name}.\"{currentTree.Name}Id\" = {
                                childQuery.Key}.{joinChildKey}";
                    }
                    else
                    {
                        queryBuilder +=
                            $" AND {childQuery.Key} ON {currentTree.Name}.\"{currentTree.Name}Id\" = {
                                childQuery.Key}.{joinChildKey}";
                    }

                    queryColumns.Add($"{currentTree.Name}.\"{joinKeys[i].From}\" AS \"{
                        joinKeys[i].From}\"");
                    queryColumns.Add($"{currentTree.Name}.\"{joinKeys[i].To}\" AS \"{
                        joinKeys[i].To}\"");
                    parentQueryColumns.Add($"~.\"{joinKeys[i].From}\" AS \"{joinKeys[i].From}\"");
                    parentQueryColumns.Add($"~.\"{joinKeys[i].To}\" AS \"{joinKeys[i].To}\"");
                    currentColumns.Add(new KeyValuePair<string, SqlNode>(joinKeys[i].To, 
                        childQuery.Value.SqlNode));
                    currentColumns.Add(new KeyValuePair<string, SqlNode>(joinKeys[i].From, 
                        childQuery.Value.SqlNode));
                }
            }

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
                                $" ) {childQuery.Key} ON {currentTree.Name}.\"Id\" = {
                                    childQuery.Key}.{joinChildKey}";
                        }
                        else
                        {
                            queryBuilder +=
                                $" AND {childQuery.Key} ON {currentTree.Name}.\"Id\" = {
                                    childQuery.Key}.{joinChildKey}";
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
            sqlQueryStatement.Append(queryBuilder);
            queryBuilder = "";
            queryBuilder += " SELECT % ";
            queryBuilder += $" FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Name}";

            var model = linkEntityDictionaryTree
                .LastOrDefault(e =>
                e.Key.Split('~')[0].Matches(currentTree.Name));

            var modelValue = string.Empty;
            if (model.Value != null)
            {
                modelValue = model.Value?.Mapping?.LastOrDefault(m =>
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

        var joinOneKey = string.Empty;
        
        if (currentColumns.LastOrDefault().Value
                .JoinOneKeys.Count() > 0 &&
            !currentColumns.LastOrDefault().Value
                .JoinOneKeys[0].From.Split('~')[0].Matches(currentTree.Name))
        {
            joinOneKey = $"{currentTree.Name}.\"{currentColumns.LastOrDefault().Value
                .JoinOneKeys[0].From.Split('~')[0]}Id\" AS \"{currentColumns.LastOrDefault().Value
                .JoinOneKeys[0].From.Split('~')[0]}{"Id".ToSnakeCase(currentTree.Id)}\"";
            
            var parentJoinKey = $"~.\"{currentColumns.LastOrDefault().Value
                .JoinOneKeys[0].From.Split('~')[0]}{"Id".ToSnakeCase(currentTree.Id)}\" AS \"{currentColumns.LastOrDefault().Value
                .JoinOneKeys[0].From.Split('~')[0]}{"Id".ToSnakeCase(currentTree.Id)}\"";
            
            var index = queryColumns.FindIndex(c => c.Matches(joinOneKey));

            if (index >= 0)
            {
                queryColumns[index] = $"{(
                    joinOneKey.Matches(queryColumns[index]) && currentColumns.LastOrDefault().Value.JoinOneKeys.Count > 0 ? 
                        $"{currentTree.Name}.\"Id\" AS \"{currentColumns.LastOrDefault().Value
                            .JoinOneKeys[0].From.Split('~')[0]+"Id".ToSnakeCase(currentTree.Id)}\"" : queryColumns[index])}";
                parentQueryColumns[index] = $"{(
                    parentJoinKey.Matches(parentQueryColumns[index]) && currentColumns.LastOrDefault().Value.JoinOneKeys.Count > 0 ? 
                        $"~.\"{"Id".ToSnakeCase(currentTree.Id)}\" AS \"{currentColumns.LastOrDefault().Value
                            .JoinOneKeys[0].From.Split('~')[0]+"Id".ToSnakeCase(currentTree.Id)}\"" : parentQueryColumns[index])}";
            }
        }

        var select = string.Join(",", queryColumns);
        queryBuilder = queryBuilder.Replace("%", select);

        if ((childrenSqlStatement.Count == 0 || !hasChildren) && currentTree.Name != rootEntity && currentColumns.Count <= 2)
        {
            return null;
        }

        var sqlStructure = new SqlQueryStructure()
        {
            Id = currentTree.Id,
            SqlNodeType = currentColumns.Count > 0 ? currentColumns.Last().Value.SqlNodeType : 
                SqlNodeType.Node,
            SqlNode = currentColumns.Count > 0 ? currentColumns.Last().Value : 
                new SqlNode(),
            Query = queryBuilder,
            Columns = queryColumns,
            ParentColumns = parentQueryColumns,
            ChildrenJoinColumns = childrenJoinColumns,
            WhereClause = entitySqlWhereStatement,
            JoinOneKey = "Id".ToSnakeCase(currentTree.Id)
        };

        if (!sqlQueryStructures.Any(a => a.Key
                .Matches(currentTree.Name)))
        {
            sqlQueryStructures.Add(currentTree.Name, sqlStructure);
        }

        if (currentTree.Name.Matches(rootEntity))
        {
            var addingMissingUpsertKeys = linkEntityDictionaryTree
                .First(c => c.Key.Split('~')[0].Matches(currentTree.Name))
                .Value.UpsertKeys.Where(u => !sqlStructure.Columns.Any(a => a
                    .Matches($"{currentTree.Name}.\"Id\" AS \"{u.Split('~')[1].ToSnakeCase(currentTree.Id)}\"")))
                    .Select(a => $"{currentTree.Name}.\"{a.Split('~')[1]}\" AS \"{a.Split('~')[1].ToSnakeCase(currentTree.Id)}\"");

            if (addingMissingUpsertKeys != null && addingMissingUpsertKeys.Count() > 0)
            {
                sqlStructure.Columns.AddRange(addingMissingUpsertKeys);
                sqlStructure.ParentColumns.AddRange(addingMissingUpsertKeys);
            }
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
            var currentModel = visitedModels.LastOrDefault();

            if (linkModelDictionaryTree.TryGetValue($"{currentTree.Name}~{node.ToString()}", 
                    out var sqlNodeFrom) ||
                linkModelDictionaryTree.TryGetValue($"{currentModel}~{node.ToString()}", 
                    out sqlNodeFrom))
            {
                if (linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey, 
                        out var sqlNodeTo))
                {
                    sqlNodeTo.SqlNodeType = SqlNodeType.Mutation;

                    if (previousNode.Split(':').Length == 2)
                    {
                        if (sqlNodeFrom.FromEnumeration.TryGetValue(
                                previousNode.Split(':')[1].Sanitize().Replace("_", ""),
                                out var enumValue))
                        {
                            var toEnum = sqlNodeTo.ToEnumeration
                                .FirstOrDefault(e =>
                                e.Value.Matches(enumValue)).Value;
                            sqlNodeTo.Value = toEnum;
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
        Dictionary<string, SqlNode> sqlStatementNodes, List<string> models, List<string> entities, 
        SqlNode? sqlNode)
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
        List<string> entities, bool isEdge)
    {
        if (node != null && node.GetNodes()?.Count() == 0)
        {
            var currentModel = visitedModels.LastOrDefault();

            if (linkModelDictionaryTree.TryGetValue($"{currentTree.Name}~{node.ToString()}", 
                    out var sqlNodeFrom) ||
                linkModelDictionaryTree.TryGetValue($"{currentModel}~{node.ToString()}", 
                    out sqlNodeFrom))
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

            GetFields(trees, childNode, linkEntityDictionaryTree, linkModelDictionaryTree, 
                sqlStatementNodes,
                currentTree,
                parentTree, visitedModels, models, entities, isEdge);
        }
    }

    private static void AddField(Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes, List<string> entities, SqlNode? sqlNode,
        bool isEdge)
    {
        foreach (var entity in linkEntityDictionaryTree
                     .Where(v => sqlNode.Column.Matches(v.Value.Column)))
        {
            if (sqlStatementNodes.ContainsKey(entity.Key) &&
                entities.Contains(entity.Key.Split("~")[0]) && entity.Value.SqlNodeType == 
                SqlNodeType.Edge)
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
        Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> linkModelDictionaryTree, List<string> whereFields,
        Dictionary<string, string> sqlWhereStatement,
        ISyntaxNode whereNode, string entityName, string rootEntityName, string wrapperEntityName,
        string clauseCondition,
        List<string> clauseType,
        Dictionary<string, List<string>> permission = null)
    {
        if (whereNode == null)
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
                    if (linkModelDictionaryTree.TryGetValue($"{currentEntity}~{column}",
                            out var currentKeyValue))
                    {
                        var fieldValue = currentKeyValue.RelationshipKey.Replace('~', '.');
                        currentEntity = $"{currentKeyValue.RelationshipKey.Split('~')[0]}";
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
                        var clauseValue = column[1].Trim().Trim('"');
                        var fieldParts = whereFields.Last().Split('.');
                        var currentNodeTree = trees[currentEntity];
                        var field = fieldParts[1];

                        switch (column[0])
                        {
                            case "eq":
                            {
                                var clause = SqlGraphQLHelper
                                    .ProcessFilter(currentNodeTree, linkEntityDictionaryTree,
                                    field, "=", 
                                    clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, clause, field, trees);
                                break;
                            }
                            case "neq":
                            {
                                var clause = SqlGraphQLHelper
                                    .ProcessFilter(currentNodeTree, linkEntityDictionaryTree, 
                                        field, "<>", clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, clause, field, trees);
                                break;
                            }
                            case "in":
                            {
                                clauseValue = "(" + string.Join(',',
                                    column[1].Replace("[", "").Replace("]", "").Split(',')
                                        .Select(v => $"'{v.Trim()}'")) + ")";
                                var clause = SqlGraphQLHelper
                                    .ProcessFilter(currentNodeTree, linkEntityDictionaryTree,
                                    field, "in", clauseValue, clauseCondition);
                                AddToDictionary(sqlWhereStatement, clause, field, trees);
                                break;
                            }
                        }
                        
                        clauseCondition = string.Empty;
                    }
                }
            }
            
            GetFieldsWhere(trees, linkEntityDictionaryTree, linkModelDictionaryTree, whereFields, 
                sqlWhereStatement,
                wNode,
                currentEntity, rootEntityName, wrapperEntityName, clauseCondition, clauseType, permission);
        }
    }
}