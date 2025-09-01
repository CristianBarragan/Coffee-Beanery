using Domain.Util.GraphQL.Extension;
using Domain.Util.GraphQL.Model;

namespace Domain.Util.GraphQL.Helper;

public static class GraphQLHelper
{
    public static (string sql, List<string> sqlRoute, List<string> upsertSql) HandleMutation(NodeTree nodeTree, GraphQLStructure graphQLStructure)
    {
        var upsertSql = new List<string>();
        var sqlRoute = new List<string>();
        
        var handlePartyQueryResult = HandleGraph(graphQLStructure, string.Empty, 
            upsertSql, sqlRoute, string.Empty, nodeTree, null,
            new List<string>(), false, new Dictionary<string, string>(),
            new List<string>(), new List<string>(), true);
        
        return (handlePartyQueryResult.sql, sqlRoute, upsertSql);
    }

    public static (string sql, List<string> sqlRoute, bool hasTotalCount, bool hasPagination, string totalCount, 
        int from, int to)
        HandleQuery(NodeTree nodeTree, GraphQLStructure graphQLStructure)
    {
        var pagination = "";
        var from = 1;
        var to = graphQLStructure.Pagination!.PageSize;
        
        if (!string.IsNullOrEmpty(graphQLStructure.Pagination!.After) && graphQLStructure.Pagination.First > 0 &&
            graphQLStructure.HasTotalCount)
        {
            from = int.Parse(graphQLStructure.Pagination.After) + 1;
            to = from + graphQLStructure.Pagination.First!.Value;
            pagination += $" WHERE \"RowNumber\" BETWEEN {from} AND {to}";
        }
        else if (!string.IsNullOrEmpty(graphQLStructure.Pagination?.Before) && graphQLStructure.Pagination.Last > 0 &&
                 graphQLStructure.HasTotalCount)
        {
            to = int.Parse(graphQLStructure.Pagination.Before) - 1;
            from = to - graphQLStructure.Pagination.Last!.Value;
            to = to >= 1 ? to : 1;
            from = from >= 1 ? from : 1;
            pagination += $" WHERE \"RowNumber\" BETWEEN {from} AND {to}";
        }
        else if (graphQLStructure.Pagination!.First > 0 && graphQLStructure.Pagination!.Last > 0 && graphQLStructure.HasTotalCount)
        {
            to = graphQLStructure.Pagination!.First!.Value;
            pagination +=
                $" WHERE \"RowNumber\" BETWEEN {graphQLStructure.Pagination!.First} AND {graphQLStructure.Pagination!.Last}";
        }
        else if (graphQLStructure.Pagination!.First > 0 && graphQLStructure.HasTotalCount)
        {
            to = graphQLStructure.Pagination!.First!.Value;
            pagination +=
                $" WHERE \"RowNumber\" BETWEEN {graphQLStructure.Pagination!.First} AND \"RowNumber\"";
        }
        else if (graphQLStructure.Pagination!.Last > 0 && graphQLStructure.HasTotalCount)
        {
            pagination +=
                $" WHERE \"RowNumber\" BETWEEN \"RowNumber\" - {graphQLStructure.Pagination!.Last} AND \"RowNumber\"";
        }
        else
        {
            to = 0;
            from = 0;
        }

        var sqlRoute = new List<string>();
        
        var handlePartyQueryResult = HandleGraph(graphQLStructure, string.Empty, 
            new List<string>(), sqlRoute, string.Empty, nodeTree, null,
            new List<string>(), true, new Dictionary<string, string>(),
            new List<string>(), new List<string>(), true);
        
        var hasPagination = graphQLStructure.Pagination.First > 0 || graphQLStructure.Pagination.Last > 0 ||
                            (graphQLStructure.Pagination.First > 0 &&
                             !string.IsNullOrEmpty(graphQLStructure.Pagination.After)) ||
                            (graphQLStructure.Pagination.Last > 0 &&
                             !string.IsNullOrEmpty(graphQLStructure.Pagination.Before));
        var hasTotalCount = graphQLStructure.HasTotalCount;
        var sql = $"WITH {nodeTree.Schema}s AS (SELECT * FROM (SELECT * FROM (" + handlePartyQueryResult.sql + $") {nodeTree.Name} ) ";
        var totalCount = hasPagination && hasTotalCount
            ? $" DENSE_RANK() OVER({handlePartyQueryResult.sort}) AS \"RowNumber\","
            : "";
        sql += " a ) " +
               $"SELECT * FROM ( SELECT (SELECT COUNT(DISTINCT \"{nodeTree.Name}InternalId\") FROM {nodeTree.Schema}s) \"RecordCount\", " +
               $"{totalCount} * FROM {nodeTree.Schema}s) a {pagination}";
        
        return (sql, sqlRoute, hasTotalCount, hasPagination, totalCount, from, to);
    }
    
    private static (string sql, string selectColumns, string sort)
        HandleGraph(GraphQLStructure query, string sql, List<string> upsertSql, List<string> sqlRoute,
            string selectColumns, NodeTree nodeTree, NodeTree? parentNodeTree, List<string> uniqueKeys,
            bool isQuery, Dictionary<string, string> nodeIds, List<string> entitiesGenerated,
            List<string> joinKeys, bool isRootNodeTree)
    {
        var childrenSql = string.Empty;
        var childrenSelectColumnsSql = string.Empty;
        var nodeEntityName = nodeTree.Name;
        nodeEntityName += nodeTree.Id;
        if (!uniqueKeys.Contains(nodeEntityName))
        {
            uniqueKeys.Add(nodeEntityName);
        }

        if (isRootNodeTree)
        {
            if (!sqlRoute.Contains($"{nodeTree.Name}InternalId"))
            {
                sqlRoute.Add($"{nodeTree.Name}InternalId");
            }
        }

        if (nodeTree.Mappings.Count == 0)
        {
            return (
                string.Empty, string.Empty, string.Empty);
        }

        foreach (var childNode in nodeTree.Children)
        {
            var handleResult = HandleGraph(query, sql, upsertSql, sqlRoute, selectColumns, childNode, nodeTree,
                uniqueKeys, isQuery, nodeIds, entitiesGenerated, joinKeys, false);
            if (!string.IsNullOrEmpty(handleResult.sql) && !sqlRoute.Contains($"{nodeTree.Name}InternalId"))
            {
                sqlRoute.Add($"{nodeTree.Name}InternalId");
            }

            if (!string.IsNullOrEmpty(selectColumns) && !isRootNodeTree)
            {
                childrenSelectColumnsSql = handleResult.selectColumns;
            }

            childrenSql += handleResult.sql;
            if (childNode.Children.Any())
            {
                foreach (var childOfNode in childNode.Children)
                {
                    var childOfNodeEntityName = childOfNode.Name;
                    childOfNodeEntityName += childOfNode.Id;
                    if (!uniqueKeys.Contains(childOfNodeEntityName))
                    {
                        uniqueKeys.Add(childOfNodeEntityName);
                    }

                    if (string.IsNullOrEmpty(childOfNodeEntityName) || string.IsNullOrEmpty(handleResult.selectColumns))
                    {
                        continue;
                    }

                    var childNodeName = childNode.Name;
                    childNodeName += childNode.Id;
                    childrenSelectColumnsSql = "," + handleResult.selectColumns.Substring(1)
                        .Replace(childOfNodeEntityName + $".\"Id\" AS \"{childOfNode.Name}InternalId\"",
                            childNodeName +
                            $".\"{childOfNode.Name}InternalId\" AS \"{childOfNode.Name}InternalId\"")
                        .Replace(childOfNodeEntityName + ".\"", childNodeName + ".\"");
                    selectColumns += "," + childrenSelectColumnsSql;
                }
            }
            else
            {
                if (handleResult.selectColumns != "")
                {
                    selectColumns += "," + handleResult.selectColumns;
                }
            }
        }

        var parentEntityName = nodeTree.ParentName;
        if (!string.IsNullOrEmpty(parentEntityName))
        {
            parentEntityName += parentNodeTree?.Id;
            if (!uniqueKeys.Contains(parentEntityName))
            {
                uniqueKeys.Add(parentEntityName);
            }
        }

        var handleEntityResult = HandleEntity(nodeTree, query, sql, childrenSelectColumnsSql, sqlRoute,
            isQuery, nodeIds, entitiesGenerated, parentNodeTree, isRootNodeTree, upsertSql);
        
        if (isRootNodeTree)
        {
            sql += childrenSql + handleEntityResult.onSql;
            var beforeReplacingSql = handleEntityResult.sql + sql + handleEntityResult.filter;
            var columnsReplaced = selectColumns.Split(',').ToList();
            
            foreach (var childNodeTree in nodeTree.Children)
            {
                var childOfNodeEntityName = $"{childNodeTree.Name}";
                columnsReplaced = columnsReplaced.Select(s => s.Replace(childOfNodeEntityName + ".\"Id\"",
                    childOfNodeEntityName + $".\"{childNodeTree.Name}InternalId\"")).ToList();
            }

            var resultSetCleanColumns = CleanColumns(nodeTree, query,
                handleEntityResult.selectColumns.Replace(',', '*') + " * " + string.Join('*', columnsReplaced), 
                beforeReplacingSql, nodeIds);
            
            ReplaceJoinKeys(query, upsertSql, nodeTree.UpsertKeys, resultSetCleanColumns.columns, entitiesGenerated, nodeIds,
                joinKeys);
            
            var sqlColumnsResult = string.Join(',', resultSetCleanColumns.columns.TrimEnd('*'));

            return (
                beforeReplacingSql.Replace("{~}", sqlColumnsResult).Replace('*', ',').Replace("%", ",")
                    .Replace('`', ':'), sqlColumnsResult, handleEntityResult.sortSql);
        }

        if (!isQuery)
        {
            var multipleRecordsMutations = query.Mutations
                .Where(m => m.Key.Split("~")[0].Matches(nodeTree.Id.ToString()) && m.Value.Split('~').Length > 0).ToList();
            var sqlHandleMutation = handleEntityResult.sql;
            var unionOfRecords = new Dictionary<string, string>();
            foreach (var mutation in multipleRecordsMutations)
            {
                var mutationValue = mutation.Value.Replace('"', '\'');
                for (var i = 0; i < mutation.Value.Split('~').Length; i++)
                {
                    if (!unionOfRecords.TryGetValue(i.ToString(), out var record))
                    {
                        if (!string.IsNullOrEmpty(sqlHandleMutation))
                        {
                            record = sqlHandleMutation.Substring(" LEFT JOIN (".Length,
                                sqlHandleMutation.Length - " LEFT JOIN (".Length);
                        }

                        record += $"{(i < mutationValue.Split('~').Length - 1 ? " UNION ALL " : "")} ";
                        record = record.Replace(mutationValue, mutationValue.Split('~')[i]);
                        unionOfRecords.Add(i.ToString(), record);
                    }
                    else
                    {
                        record = record.Replace(mutationValue, mutationValue.Split('~')[i]);
                        unionOfRecords[i.ToString()] = record;
                    }

                    handleEntityResult.filter =
                        handleEntityResult.filter?.Replace(mutationValue, mutationValue.Split('~')[i]);
                    var name = mutation.Key.Split('~')[1].Substring(0, 1).ToUpper() + mutation.Key.Split('~')[1]
                        .Substring(1, mutation.Key.Split('~')[1].Length - 1);
                    handleEntityResult.selectColumns =
                        handleEntityResult.selectColumns?.Replace(mutationValue, $"{nodeEntityName}.\"{name}\"");
                }
            }

            if (unionOfRecords.Count > 0)
            {
                handleEntityResult.sql = " LEFT JOIN (" + string.Join(' ', unionOfRecords.Values);
            }
        }

        if (!string.IsNullOrEmpty(handleEntityResult.filter))
        {
            sql += " ) " + handleEntityResult.onSql;
        }
        else if (!string.IsNullOrEmpty(childrenSql))
        {
            sql += childrenSql + " ) " + handleEntityResult.onSql;
        }
        else if (!string.IsNullOrEmpty(handleEntityResult.onSql))
        {
            sql += " ) " + handleEntityResult.onSql;
        }

        var entityKey = sqlRoute.FirstOrDefault(c => c.Replace("InternalId", "").Matches(nodeTree.Name));
        if (!string.IsNullOrEmpty(entityKey) &&
            !handleEntityResult.selectColumns.Split(',').ToList().Any(c => c.Contains(entityKey)))
        {
            handleEntityResult.selectColumns =
                handleEntityResult.selectColumns.Insert(0, $"{nodeEntityName}.\"Id\" AS {entityKey}");
        }

        var resultSet = CleanColumns(nodeTree, query,
            handleEntityResult.selectColumns?.Replace(',', '*'), string.Empty, nodeIds);
        
        return (handleEntityResult.sql + sql, resultSet.columns, handleEntityResult.sortSql);
    }
    
    private static (string sql, string onSql, List<string> updateSql, string selectColumns, string sortSql, string
        filter, Dictionary<string, string> nodeIds, List<string> entitiesGenerated, string joinKeyValue) 
        HandleEntity(
            NodeTree nodeTree, GraphQLStructure query, string sql, string childrenSelectColumnsSql,
            List<string> sqlRoute, bool isQuery, Dictionary<string, string> nodeIds, List<string> entitiesGenerated, 
            NodeTree? parentNodeTree, bool isRootNodeTree, List<string> upsertSql)
    {
        var filters = new List<string>();
        var entityFilter = HandleFilter(nodeTree, query);
        
        var selectResult = HandleSelect(nodeTree, query, !string.IsNullOrEmpty(entityFilter), 
            !string.IsNullOrEmpty(childrenSelectColumnsSql),isQuery, isRootNodeTree);
        
        var sort = string.Empty;
        if (isRootNodeTree)
        {
            sort = HandleSort(nodeTree, query, isQuery);
        }

        if (string.IsNullOrEmpty(selectResult.sql))
        {
            return default;
        }

        if (!sqlRoute.Contains($"{nodeTree.Name}InternalId"))
        {
            sqlRoute.Add($"{nodeTree.Name}InternalId");
        }
        
        var generateEntityQueryResult = GenerateEntityQuery(nodeTree, query, selectResult.sql, selectResult.upsertSqlFields, 
            selectResult.upsertSqlScalar, selectResult.upsertSqlExcludes, childrenSelectColumnsSql, 
            filters, entityFilter, selectResult.isJoin, isRootNodeTree, nodeIds, upsertSql, parentNodeTree);
        sql += generateEntityQueryResult.sql;
        
        if (!isQuery)
        {
            if (string.IsNullOrEmpty(entityFilter))
            {
                entityFilter = generateEntityQueryResult.upsertKeyFilter.Replace(" AND ", " WHERE ");
            }
            else
            {
                entityFilter += generateEntityQueryResult.upsertKeyFilter;
            }
        }
        
        var selectColumns = " , " + generateEntityQueryResult.entitySelect;
        var cleanedColumns = CleanColumns(nodeTree, query, selectColumns.Replace(',', '*'), 
            string.Empty, nodeIds).columns;
        
        return (sql, generateEntityQueryResult.onSql, generateEntityQueryResult.sqlUpsert, cleanedColumns,
            sort, entityFilter, nodeIds, entitiesGenerated, generateEntityQueryResult.joinKeyValue);
    }
    
    private static (string sql, List<string> sqlUpsert, string onSql, string entitySelect, 
        string childEntitySelect, string upsertKeyFilter, string joinKeyValue) 
        GenerateEntityQuery(NodeTree nodeTree, GraphQLStructure query,
            string entitySelect, string upsertSqlFields, string upsertSqlScalar, string upsertSqlExcludes,
            string childrenSelectColumnsSql, List<string> filters, string filterSql, bool isJoin, bool isRootNodeTree,
            Dictionary<string, string> nodeIds, List<string> upsertSql, NodeTree? parentNodeTree)
    {
        var sql = string.Empty;
        var onSql = string.Empty;
        var childEntitySelect = string.Empty;
        var upsertKeyFilter = string.Empty;
        
        if (nodeTree.Mappings.Count > 0 && nodeTree.Mappings.Count > 1)
        {
            var columnsUpdateScalar = upsertSqlScalar.Split(',');
            var columnsKey = string.Join(',', nodeTree.UpsertKeys.Select(k => $"\"{k}\""));

            nodeTree.ParentName = !string.IsNullOrEmpty(nodeTree.ParentName) ? nodeTree.ParentName : nodeTree.Name;
            
            var parentEntityNameUpperCase = !string.IsNullOrEmpty(nodeTree.ParentName)
                ? nodeTree.ParentName.Substring(0, 1).ToUpper() +
                  nodeTree.ParentName.Substring(1, nodeTree.ParentName.Length - 1)
                : nodeTree.Name.Substring(0, 1).ToUpper() + nodeTree.Name.Substring(1, nodeTree.Name.Length - 1);
            var parentFilterSql = string.Empty;
            
            if (!isRootNodeTree)
            {
                parentFilterSql = HandleFilter(parentNodeTree, query);
            }

            if (!string.IsNullOrEmpty(upsertSqlFields))
            {
                QueryMutation(nodeTree, query, isRootNodeTree, upsertSqlExcludes, columnsUpdateScalar.ToList(),
                    upsertSqlFields, parentEntityNameUpperCase, filterSql, parentFilterSql,
                    columnsKey, upsertSql);
            }
        }

        if (isRootNodeTree)
        {
            sql += $"SELECT {"{~}"} " + $"FROM \"{nodeTree.Schema}\".\"{nodeTree.Name}\"";
            sql += $" AS {nodeTree.Name} ";
            return (sql, upsertSql, onSql, entitySelect, childEntitySelect, upsertKeyFilter, nodeTree.JoinKey);
        }

        var childEntityList =
            childrenSelectColumnsSql.Trim(',').Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();
        var select = entitySelect.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();
        select.AddRange(childEntityList.Select((s, index) => new Ordering { Index = index, Value = s })
            .DistinctBy(s => s.Value).OrderBy(s => s.Index).Select(s => s.Value)!);
        if (!string.IsNullOrEmpty(childrenSelectColumnsSql))
        {
            select.AddRange(childrenSelectColumnsSql.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList());
            foreach (var childNodeTree in nodeTree.Children)
            {
                var childOfNodeEntityName = $"{childNodeTree.Name}{childNodeTree.Id}";
                select = select.Select(s => s.Replace(childOfNodeEntityName + ".\"Id\"",
                    childOfNodeEntityName + $".\"{childNodeTree.Name}InternalId\"")).ToList();
            }
        }

        entitySelect = string.Join(',',
            select.Select((s, index) => new Ordering { Index = index, Value = s }).DistinctBy(s => s.Value)
                .OrderBy(s => s.Index).Select(s => s.Value));
        
        var columns = CleanColumns(nodeTree, query, entitySelect.Replace(',', '*'),
            string.Empty, nodeIds).columns;
        
        sql = isJoin ? @" JOIN ( " : @" LEFT JOIN ( ";
        sql += $"SELECT {columns}, {(string.IsNullOrEmpty(nodeTree.JoinKey) ? "" : $"{nodeTree.Name}.{(nodeTree.JoinKey.Matches("Id") ? 
            $"\"{nodeTree.Name}InternalId\" AS \"{nodeTree.Name}InternalId\"" : 
            $"\"{nodeTree.JoinKey}\" AS \"{nodeTree.JoinKey}\"")}")}";
        sql += $" FROM \"{nodeTree.Schema}\".\"{nodeTree.Name}\" {nodeTree.Name} ";
        sql += $" {upsertKeyFilter} {filterSql} ";
        nodeTree.JoinKey = nodeTree.JoinKey.Matches("Id") ? $"{nodeTree.Name}InternalId" : $"{nodeTree.JoinKey}";
        onSql = $"{nodeTree.Name} ON {nodeTree.Name}.\"{nodeTree.JoinKey}\"" + " = " + $"{nodeTree.ParentName}." + "\"Id\"";
        
        return (sql, upsertSql, onSql, entitySelect, childEntitySelect, string.Empty, filterSql);
    }

    private static void QueryMutation(NodeTree nodeTree, GraphQLStructure query, bool isRootNodeTree, string upsertSqlExcludes, List<string> columnsUpdateScalar,
        string upsertSqlFields, string parentEntityNameUpperCase, string filterSql, string parentFilterSql, string columnsKey, List<string> upsertSql)
    {
        if(query.Mutations
           .Any(m =>
               m.Key.Contains("Mutation") &&
               m.Key.Split('~')[0].Replace("@","").ToString().Matches(nodeTree.Id.ToString())))
        {
            var doUpdate = string.IsNullOrEmpty(upsertSqlExcludes)
                ? " DO NOTHING "
                : $" DO UPDATE SET {upsertSqlExcludes}";
            
            var keyScalarValues = new List<string>();
            
            foreach (var upsertKey in nodeTree.UpsertKeys)
            {
                var key = columnsUpdateScalar.FirstOrDefault(s =>
                    s.Split('~').Length > 1 && s.Split('~')[1].Trim('"').ToLower().Matches(upsertKey.ToLower()));
                if (!string.IsNullOrEmpty(key))
                {
                    keyScalarValues.Add(key);
                }
            }

            var upsertQuery = $" INSERT INTO \"{nodeTree.Schema}\".\"{nodeTree.Name}\" ({upsertSqlFields}) " +
                              $"{(isRootNodeTree && string.IsNullOrEmpty(filterSql) && string.IsNullOrEmpty(parentFilterSql) ? " VALUES ( " : " ( SELECT ")} ";

            var select = query.Mutations.Where(m =>
                    m.Key.Split('~')[0].Replace("@","").Matches(nodeTree.Id.ToString()) &&
                    m.Key.Contains("Mutation"))
                .Select(m => $"{m.Key}");

            if (select.Count() > 0)
            {
                upsertQuery += $"{string.Join(',', select)} ";
            }
        
            var otherColumns = query.Mutations.Where(a => 
                    !select.Contains(a.Key) &&
                    a.Key.Split('~')[0].Replace("@","").Matches(nodeTree.Id.ToString()) && 
                    a.Key.Contains("Mutation"))
                .Select(m => $"{m.Key}");
            
            if (otherColumns?.Count() > 0)
            {
                upsertQuery += $" , {string.Join(',', otherColumns)} ";
            }
            
            upsertQuery += $"{(isRootNodeTree ? "" : $" , {nodeTree.ParentName}.\"Id\" AS \"{nodeTree.JoinKey.ToUpperCamelCase()}\"")} ";

            var currentParentFilter = parentFilterSql.Replace("WHERE", "");
            var currentFilter = filterSql.Replace("WHERE", "");
            
            if (!isRootNodeTree)
            {
                upsertQuery +=
                    $" FROM \"{nodeTree.Schema}\".\"{nodeTree.ParentName}\" {nodeTree.ParentName}";

                if (!string.IsNullOrEmpty(currentParentFilter))
                {
                    if (!string.IsNullOrEmpty(currentFilter))
                    {
                        upsertQuery +=
                            $" {parentFilterSql} {(!string.IsNullOrEmpty(filterSql) ? $" AND {filterSql} ) " : " ) ")} ";
                    }
                    else
                    {
                        upsertQuery +=
                            $" {parentFilterSql} ) ";
                    }
                }
                else if (!string.IsNullOrEmpty(currentFilter))
                {
                    upsertQuery +=
                        $" {parentFilterSql} {(!string.IsNullOrEmpty(filterSql) ? $" AND {filterSql} ) " : " ) ")} ";
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(currentFilter))
                {
                    upsertQuery +=
                        $" FROM \"{nodeTree.Schema}\".\"{nodeTree.Name}\" {nodeTree.Name}";
                    upsertQuery +=
                        $" {(!string.IsNullOrEmpty(filterSql) ? $" {filterSql} ) " : " ) ")}";
                }
            }
            
            upsertQuery += $" ) ON CONFLICT (" +
            $" {columnsKey}) {doUpdate}";
            
            if (!upsertSql.Contains(upsertQuery))
            {
                upsertSql.Add(upsertQuery);    
            }
        }
    }
    
    private static bool ProcessColumn(NodeTree nodeTree, List<string> columns, List<string> entitySelect,
        List<string> entitySelectJoin)
    {
        var isJoin = false;
        
        foreach (var nodeSelect in columns)
        {
            if (!entitySelect.Any(e => e.Matches(nodeTree.Name)) && nodeTree.Mappings.Any(s => s.Key.Matches(nodeSelect.ToFieldName())) &&
                nodeSelect.ToEntityName().Matches(nodeTree.Name))
            {
                var select = nodeTree.Mappings.FirstOrDefault(s => s.Key.Matches(nodeSelect.ToFieldName())).Value;
                if (!string.IsNullOrEmpty(select))
                {
                    var field = $"{nodeTree.Name}.\"{select}\" AS \"{select}\"";
                    entitySelect.Add(field);
                    if (!entitySelect.Any(a => a.Matches(field)) && !select.Matches(nodeTree.JoinKey))
                    {
                        entitySelect.Add(field);
                        field = $"{nodeTree.Name}.\"{select}\"";
                        entitySelectJoin.Add(field);
                    }
                    isJoin = true;
                }
            }
        }
        return isJoin;
    }

    private static void ProcessMutation(NodeTree nodeTree, GraphQLStructure query, List<string> entitySelect,
        List<string> upsertSqlScalar, List<string> upsertSqlFields,
        List<string> upsertSqlValues, List<string> upsertSqlExcludes)
    {
        foreach (var nodeSelect in query.NodeSelect)
        {
            var fieldName = nodeSelect.ToFieldName();
            var nodeId = nodeSelect.ToNodeId();

            if (!nodeTree.Id.ToString().Matches(nodeId))
            {
                continue;
            }
            
            var select = nodeTree.Mappings.
                FirstOrDefault(s => s.Key.Matches(fieldName)).Value;
            
            if (!string.IsNullOrEmpty(select))
            {
                var valueFields = query.Mutations.Where(f =>
                    f.Key.Split("~")[1].Matches(fieldName));

                foreach (var valueField in valueFields)
                {
                    var valueSplit = valueField.Value;

                    if (nodeTree.EnumerationMappings.ContainsKey(select))
                    {
                        valueSplit = nodeTree.EnumerationMappings[select].GetValueOrDefault(valueField.Value.Trim('\'').Trim().ToUpperCamelCase()) ?? valueField.Value.Sanitize();
                    }
  
                    if (!string.IsNullOrEmpty(valueSplit) && valueSplit.Contains('`'))
                    {
                        var date =
                            $"TO_TIMESTAMP({valueSplit}, " +
                            $"'YYYY-MM-DD T HH24:MI:SS:MS Z')::timestamptz at time zone 'Etc/UTC' AS {select}";
                        entitySelect.Add(date);
                        upsertSqlScalar.Add(valueField.Key);
                        query.Mutations[valueField.Key] =
                            $"TO_TIMESTAMP({valueSplit}, " +
                            $"'YYYY-MM-DD T HH24:MI:SS:MS Z')::timestamptz at time zone 'Etc/UTC'";
                    }
                    else
                    {
                        var value = $"{valueField.Key} AS {select}";
                        entitySelect.Add(value);
                        upsertSqlScalar.Add(valueField.Key);
                        query.Mutations[valueField.Key] = valueSplit.Replace("'","");
                    }

                    if (!upsertSqlFields.Contains($"\"{select}\""))
                    {
                        upsertSqlFields.Add($"\"{select}\"");
                    }
                    else 
                    {
                        foreach (var upsertKey in nodeTree.UpsertKeys)
                        {
                            if (!upsertSqlFields.Contains($"\"{upsertKey}\""))
                            {
                                upsertSqlFields.Add($"\"{upsertKey}\"");
                            }
                        }
                    }
                
                    if (!upsertSqlValues.Contains(valueField.Key))
                    {
                        upsertSqlValues.Add(valueField.Key);
                    }
                
                    if (!nodeTree.UpsertKeys.Any(k => k.Matches(select.Trim('"')))
                        && !upsertSqlExcludes.Any(u => u.Matches($"\"{select}\" = EXCLUDED.\"{select}\"")))
                    {
                        upsertSqlExcludes.Add($"\"{select}\" = EXCLUDED.\"{select}\"");
                    }
                }
            }
        }
    }

    private static void HandleMutationFields(NodeTree nodeTree, GraphQLStructure query, List<string> entitySelect, List<string> upsertSqlScalar, List<string> upsertSqlFields,
        List<string> upsertSqlValues, List<string> upsertSqlExcludes, List<string> upsertFields, List<string> selectFields, List<string> selectValues)
    {
        ProcessMutation(nodeTree, query, entitySelect, upsertSqlScalar, upsertSqlFields,
            upsertSqlValues, upsertSqlExcludes);
        
        upsertFields.AddRange(upsertSqlFields.Where(f => !upsertFields.Contains(f)));
        nodeTree.JoinKey = nodeTree.JoinKey.Matches("Id") ? $"{nodeTree.Name}InternalId" : $"{nodeTree.JoinKey}";
        if (!string.IsNullOrEmpty(nodeTree.JoinKey) && 
            !nodeTree.UpsertKeys.Any(k => k.Matches(nodeTree.JoinKey.Trim('"'))))
        {
            upsertFields.Add($"\"{nodeTree.JoinKey}\"");
            upsertSqlValues.Add($"{nodeTree.ParentName.ToLower()}{nodeTree.ParentId}.\"Id\"");
            upsertSqlExcludes.Add($"\"{nodeTree.JoinKey}\" = EXCLUDED.\"{nodeTree.JoinKey}\"");
        }

        if (upsertSqlFields.Count > 0)
        {
            for (var i = nodeTree.UpsertKeys.Count - 1; i >= 0; i--)
            {
                if (!selectFields.Contains($"\"{nodeTree.UpsertKeys[i]}\""))
                {
                    selectFields.Insert(0, $"\"{nodeTree.UpsertKeys[i]}\"");
                }
                
                var keyField = upsertFields.FindIndex(f => f.Matches($"\"{nodeTree.UpsertKeys[i]}\""));
                if (keyField >= 0 && keyField < upsertFields.Count)
                {
                    upsertFields.RemoveAt(keyField);
                    keyField = upsertSqlValues.FindIndex(f => f.Contains(nodeTree.UpsertKeys[i]));
                    if (keyField >= 0 && keyField < upsertSqlValues.Count)
                    {
                        selectValues.Insert(0, upsertSqlValues[keyField]);
                        upsertSqlValues.RemoveAt(keyField);
                    }
                    else
                    {
                        selectValues.Insert(0, $"{nodeTree.Name}{nodeTree.Id}.{nodeTree.UpsertKeys[i]}");
                        entitySelect.Add($"{nodeTree.Name}.\"{nodeTree.UpsertKeys[i]}\" AS \"{nodeTree.UpsertKeys[i]}\"");
                    }
                }
                else
                {
                    if (!selectValues.Contains($"\"{nodeTree.UpsertKeys[i]}\""))
                    {
                        selectValues.Insert(0, $"\"{nodeTree.UpsertKeys[i]}\"");
                        entitySelect.Add($"{nodeTree.Name}.\"{nodeTree.UpsertKeys[i]}\" AS \"{nodeTree.UpsertKeys[i]}\"");    
                    }
                }
            }
            
            selectFields.AddRange(upsertFields);
            selectValues.AddRange(upsertSqlValues);
        }
    }

    private static (string sql, string upsertSqlFields, string upsertSqlValues, string upsertSqlScalar, string
        upsertSqlExcludes, string sqlJoin, bool isJoin) 
        HandleSelect(NodeTree nodeTree, GraphQLStructure query, bool hasFilter,
            bool hasChildrenSelect, bool isQuery, bool isRootNodeTree)
    {
        var entitySelect = new List<string>();
        var entitySelectJoin = new List<string>();
        var upsertSqlFields = new List<string>();
        var upsertSqlValues = new List<string>();
        var upsertSqlScalar = new List<string>();
        var upsertSqlExcludes = new List<string>();
        var upsertFields = new List<string>();
        var isJoin = false;
        var selectFields = new List<string>();
        var selectValues = new List<string>();
        
        if (isQuery)
        {
            isJoin = ProcessColumn(nodeTree, query.NodeSelect, entitySelect, entitySelectJoin);
            isJoin = ProcessColumn(nodeTree, query.EdgeSelect, entitySelect, entitySelectJoin);
        }
        else
        {
            HandleMutationFields(nodeTree, query, entitySelect, upsertSqlScalar, upsertSqlFields,
                upsertSqlValues, upsertSqlExcludes, upsertFields, selectFields, selectValues);
        }

        foreach (var filterCondition in query.FilterConditions)
        {
            if (!entitySelect.Contains(filterCondition.Split(' ')[0]) &&
                nodeTree.Mappings.Keys.Contains(filterCondition.ToLower().Split(' ')[0].Trim('"')) &&
                filterCondition.ToLower().Split(' ')[0].Trim('"').ToLower().StartsWith(nodeTree.Name.ToLower()))
            {
                var select = nodeTree.Mappings
                    .FirstOrDefault(s => filterCondition.ToLower().Split(' ')[0].Trim('"').Contains(s.Key)).Value;
                if (!string.IsNullOrEmpty(select))
                {
                    var selectSplit = select.Split('~');
                    var field = $"Id.{selectSplit[1]} AS {selectSplit[1]}";
                    if (!entitySelect.Any(a => a.Matches(field)) && !selectSplit[1].Matches(nodeTree.JoinKey))
                    {
                        entitySelect.Add(field);
                        field = $"{nodeTree.JoinKey}.{selectSplit[1]}";
                        entitySelectJoin.Add(field);
                    }
                }
            }
        }

        foreach (var sortCondition in query.Sort)
        {
            var condition = sortCondition.ToLower().Split(' ')[0].Replace("\"", "");
            if (!entitySelect.Any(s => s.Split(" AS ")[0].Matches(condition)) &&
                nodeTree.Mappings.Keys.Contains(condition) && condition.StartsWith(nodeTree.Name.ToLower()))
            {
                var select = nodeTree.Mappings.FirstOrDefault(s => condition.Contains(s.Key.ToLower())).Value;
                if (!string.IsNullOrEmpty(select))
                {
                    var selectSplit = select.Split('~');
                    var field = $"{nodeTree.Name}.\"{selectSplit[1]}\" AS {selectSplit[1]}";
                    if (!entitySelect.Any(a => a.Matches(field)) && selectSplit[1].Matches(nodeTree.JoinKey))
                    {
                        entitySelect.Add(field);
                    }

                    field = $"{nodeTree.Name}.\"{selectSplit[1]}\" AS {selectSplit[1]}";
                    if (!entitySelectJoin.Any(a => a.Matches(field)) && selectSplit[1].Matches(nodeTree.JoinKey))
                    {
                        entitySelectJoin.Add(field);
                    }
                }
            }
        }

        if (entitySelect.Any() || hasFilter || isRootNodeTree)
        {
            entitySelect.Insert(0, $"{nodeTree.Name}.\"Id\" AS \"{nodeTree.Name}InternalId\"");
            entitySelectJoin.Insert(0, $"{nodeTree.Name}.\"{nodeTree.Name}InternalId\"");
            var field = $"{nodeTree.Name}.\"{nodeTree.JoinKey}\" AS \"{nodeTree.JoinKey}\"";
            if (!string.IsNullOrEmpty(nodeTree.JoinKey) && nodeTree.JoinKey.Matches("Id") && !entitySelect.Contains(field))
            {
                if (!entitySelect.Any(a => a.Matches(field)))
                {
                    entitySelect.Add(field);
                }
            }

            field = $"{nodeTree.Name}.\"{nodeTree.JoinKey}\"";
            if (!string.IsNullOrEmpty(nodeTree.JoinKey) && !nodeTree.JoinKey.Matches("Id") && !entitySelectJoin.Contains(field))
            {
                if (!entitySelectJoin.Any(a => a.Matches(field)))
                {
                    entitySelectJoin.Add(field);
                }
            }

            return (string.Join(",", entitySelect), string.Join(",", selectFields), string.Join(",", selectValues),
                string.Join(',', upsertSqlScalar), string.Join(",", upsertSqlExcludes),
                string.Join(",", entitySelectJoin), isJoin);
        }

        if (hasChildrenSelect && entitySelect.Count == 0)
        {
            entitySelect.Add($"{nodeTree.Name}.\"Id\" AS \"{nodeTree.Name}InternalId\"");
            entitySelect.Add($"{nodeTree.Name}.\"{nodeTree.JoinKey}\" AS \"{nodeTree.JoinKey}\"");
            entitySelectJoin.Add($"{nodeTree.Name}.\"{nodeTree.JoinKey}\"");
        }

        return (string.Join(",", entitySelect), string.Join(",", selectFields), string.Join(",", selectValues),
            string.Join(',', upsertSqlScalar), string.Join(",", upsertSqlExcludes), string.Join(",", entitySelectJoin),
            isJoin);
    }

    private static string HandleFilter(NodeTree? nodeTree, GraphQLStructure query)
    {
        if (nodeTree == null)
        {
            return string.Empty;
        }
        
        var entityFilters = query.Filter.Where(f => f.Split(".")[0].Matches(nodeTree.Name)).ToList();
        var sql = string.Empty;
        if (entityFilters.Any())
        {
            sql += " WHERE ";
            for (var i = 0; i < entityFilters.Count(); i++)
            {
                sql = ProcessFilter(nodeTree, query, sql);
            }
        }

        if (sql.Length > 5)
        {
            sql = sql.Substring(0, sql.Length - (" AND ".Length));
        }

        return sql;
    }

    private static string ProcessFilter(NodeTree nodeTree, GraphQLStructure query, string sql)
    {
        for (var i = 0; i < query.Filter.Count; i++)
        {
            if (query.Filter[i].Split(".")[0].Matches(nodeTree.Name))
            {
                var filter = query.Filter[i];
                var filterCondition = query.FilterConditions.Count > i ? query.FilterConditions[i] : string.Empty;
                var entity = filter.Split('.')[0].Sanitize();
                var field = string.Empty;
                var value = string.Empty;
                    
                if (filter.ToLower().Contains("<> 'null'"))
                {
                    field = filter.Split("<>")[0].Split('.')[1].Sanitize();
                    field = nodeTree.Mappings.FirstOrDefault(s =>
                        s.Key.Matches(field)).Value;
                    sql += $"{entity}.\"{field}\" IS NOT NULL {HandleFilterCondition(filterCondition)}";
                }
                else if (filter.ToLower().Contains("= 'null'"))
                {
                    field = filter.Split("=")[0].Split('.')[1].Sanitize();
                    field = nodeTree.Mappings.FirstOrDefault(s =>
                        s.Key.Matches(field)).Value;
                    sql += $"{entity}.\"{field}\" IS NULL {HandleFilterCondition(filterCondition)}";
                }
                else if (filter.Contains("in"))
                {
                    field = filter.Split("in")[0].Split('.')[1].Sanitize();
                    field = nodeTree.Mappings.FirstOrDefault(s =>
                        s.Key.Matches(field)).Value;
                    var inValues = string.Empty;
                    
                    foreach (var inValue in filter.Split("in")[1].Sanitize().Split(','))
                    {
                        value = nodeTree.EnumerationMappings.FirstOrDefault(s => s.Key == field)
                            .Value.FirstOrDefault(v => v.Key.Matches(inValue.ToUpperCamelCase())).Value;
                        inValues += $"'{value}'" + ",";
                    }
                    inValues = inValues.Remove(inValues.Length - 1);
                    sql += $"{entity}.\"{field}\" IN ({inValues}) {HandleFilterCondition(filterCondition)}";
                }
                else if (filter.Contains("="))
                {
                    field = filter.Split("=")[0].Split('.')[1].Sanitize();
                    field = nodeTree.Mappings.FirstOrDefault(s =>
                        s.Key.Matches(field)).Value;
                    value = nodeTree.EnumerationMappings.FirstOrDefault(s => s.Key == field)
                        .Value.FirstOrDefault(v => v.Key.Matches(filter.Split("=")[1].Sanitize().ToUpperCamelCase())).Value;
                    sql += $"{entity}.\"{field}\" = '{value}' {HandleFilterCondition(filterCondition)}";
                }
            }
        }
        return sql;
    }

    private static string HandleFilterCondition(string filterCondition)
    {
        return string.IsNullOrEmpty(filterCondition) ? " AND " : $" {filterCondition} ";
    }
    
    private static string HandleSort(NodeTree nodeTree, GraphQLStructure query, bool isQuery)
    {
        var sortSql = " ORDER BY ";
        bool hasSort = false;
        foreach (var sortClause in query.Sort)
        {
            sortSql += $" {nodeTree.Schema}s.{sortClause.Split('.')[1]} ,";
            hasSort = true;
        }

        if (!hasSort && isQuery)
        {
            sortSql += $" \"Id_\" ,";
        }

        return sortSql.Remove(sortSql.Length - 1);
    }

    private static (string columns, List<string> sqlRoute, string beforeReplacingSql)
        CleanColumns(NodeTree nodeTree, GraphQLStructure query, string columns, string beforeReplacingSql,
            Dictionary<string, string> nodeIds)
    {
        if (string.IsNullOrEmpty(columns))
        {
            return (string.Empty, new List<string>(), string.Empty);
        }

        var sqlRoute = new List<string>();
        var splitSubQueryColumns = columns.Split('*');
        var cleanedColumns = new List<string>();
        var snakeToken = "_";
        var uniqueColumns = new List<string>();
        var uniqueAlias = new List<string>();
        foreach (var columnAlias in splitSubQueryColumns)
        {
            if (columnAlias.Contains(nodeTree.JoinKey ?? string.Empty))
            {
                columnAlias.Replace($"{nodeTree.Name}{nodeTree.Id}.\"{nodeTree.JoinKey}\"",
                    $"{nodeTree.ParentName}{nodeTree.ParentId}.\"Id\"");
            }

            if (string.IsNullOrEmpty(columnAlias))
            {
                continue;
            }

            if (columnAlias.Contains("TO_TIMESTAMP("))
            {
                AddCleanedColumn(cleanedColumns, columnAlias.Replace("^", "%"));
                continue;
            }

            var columnAliasSplit = columnAlias.Split(" AS ");
            if (columnAliasSplit.Length < 2)
            {
                continue;
            }

            var columnAlias1 = string.Empty;
            if (columnAliasSplit[0].Split('"').Length > 1)
            {
                columnAlias1 = columnAliasSplit[0].Split('"')[1].Trim('"');
            }
            else
            {
                columnAlias1 = columnAliasSplit[0].Trim('"');
            }

            var columnAlias2 = columnAliasSplit[1].Trim('"');
            if (uniqueAlias.Any(c => c.Contains(columnAliasSplit[1])))
            {
                continue;
            }

            if (uniqueColumns.Contains(columnAlias1) && !columnAlias1.StartsWith("'"))
            {
                var column = string.Empty;
                if (columnAlias2.Contains('~'))
                {
                    column = columnAliasSplit[0].Split('~')[0] + ".\"" + columnAlias2 + "\" AS \"" + columnAlias2 +
                             snakeToken + "\"";
                    snakeToken += "_";
                    if (("\"" + columnAlias2 + "\"").ToLower().Contains("Internal".ToLower()))
                    {
                        sqlRoute.Add("\"" + columnAlias2 + "\"");
                    }

                    uniqueAlias.Add(columnAlias2 + snakeToken);
                }
                else
                {
                    if (columnAliasSplit[0].Split('~').Length > 1)
                    {
                        column = columnAliasSplit[0].Split('~')[0] + "." + columnAliasSplit[0].Split('~')[1] +
                                 " AS \"" + columnAlias2.ToSnakeCase() + snakeToken + "\"";
                        if (("\"" + columnAlias2.ToSnakeCase() + snakeToken + "\"").ToLower()
                            .Contains("Internal".ToLower()))
                        {
                            sqlRoute.Add(columnAlias2.ToSnakeCase() + snakeToken);
                        }

                        snakeToken += "_";
                        uniqueAlias.Add(columnAlias2 + snakeToken);
                    }
                }

                AddCleanedColumn(cleanedColumns, column);
                uniqueColumns.Add(column.Trim('"'));
            }
            else
            {
                if (columnAliasSplit[1].Trim('"').ToLower().Contains("Internal".ToLower()))
                {
                    uniqueColumns.Add(columnAliasSplit[1].Trim('"'));
                    AddCleanedColumn(cleanedColumns, $"{columnAliasSplit[0]} AS {columnAliasSplit[1]}");
                    sqlRoute.Add(columnAliasSplit[1]);
                    if (cleanedColumns.Count == 1)
                    {
                        var idColumn = columnAlias.Split(" AS ")[0] + " AS \"" +
                                       columnAlias.Split(" AS ")[0].Split('.')[1].Trim('"') + snakeToken + "\"";
                        uniqueColumns.Add((columnAlias.Split(" AS ")[0].Split('.')[1].Trim('"') + snakeToken)
                            .Trim('"'));
                        AddCleanedColumn(cleanedColumns, idColumn);
                        snakeToken += '~';
                    }
                }
                else
                {
                    uniqueColumns.Add(columnAlias1.Trim('"'));
                    AddCleanedColumn(cleanedColumns, $"{columnAliasSplit[0]} AS {columnAliasSplit[1]}");
                    uniqueAlias.Add(columnAliasSplit[1]);
                }
            }
        }

        if (!string.IsNullOrEmpty(beforeReplacingSql))
        {
            foreach (var mutation in query.Mutations)
            {
                var mutationName = mutation.Key.Split("~")[1];
                var entityMutationName = mutation.Key.Split('~')[0];
                mutationName = mutationName.Substring(0, 1).ToUpper() +
                               mutationName.Substring(1, mutationName.Length - 1);
                entityMutationName = entityMutationName.Substring(0, 1).ToUpper() +
                                     entityMutationName.Substring(1, entityMutationName.Length - 1);
                var columnAlias = cleanedColumns.FirstOrDefault(c => c.ToLower().Contains(mutationName.ToLower()));
                foreach (var nodeNameId in nodeIds)
                {
                    beforeReplacingSql = beforeReplacingSql.Replace(
                        $"#{entityMutationName.ToLower()}#{nodeNameId.Value}#{mutationName}",
                        $"{columnAlias.Split("AS")[1].Trim()} = {columnAlias.Split("AS")[0].Trim()}");
                    beforeReplacingSql = beforeReplacingSql.Replace(
                        $"~{entityMutationName.ToLower()}~{nodeNameId.Value}~{mutationName}",
                        $"{columnAlias.Split("AS")[1].Trim()} = {columnAlias.Split("AS")[0].Trim()}");
                }
            }
        }

        return (string.Join('*', cleanedColumns), sqlRoute, beforeReplacingSql);
    }

    private static void AddCleanedColumn(List<string> cleanedColumns, string column)
    {
        if (!cleanedColumns.Any(c => c.Matches(column)))
        {
            cleanedColumns.Add(column);
        }
    }

    private static void ReplaceJoinKeys(GraphQLStructure query, List<string> upsertSql, List<string> upsertKeys, string columns,
        List<string> entitiesGenerated, Dictionary<string, string> nodeIds,
        List<string> joinKeys)
    {
        for (var i = 0; i < entitiesGenerated.Count; i++)
        {
            nodeIds.TryGetValue(entitiesGenerated[i], out var nodeId);
            foreach (var upsertKey in upsertKeys)
            {
                var columnAlias = columns.Split(',').FirstOrDefault(c => c.Contains(upsertKey));
                if (!string.IsNullOrEmpty(columnAlias))
                {
                    upsertSql[i] = upsertSql[i].Replace($"~{entitiesGenerated[i]}~{nodeId}~{upsertKey}",
                        $"{columnAlias.Split("AS")[1].Trim()}");
                }
            }

            foreach (var mutation in query.Mutations.Where(m =>
                         (m.Value.Split(':')[0].Split(".")[0].Matches(entitiesGenerated[i].ToLower())) ||
                         m.Value.Split(':')[0].Split(".")[0].Matches($"{entitiesGenerated[i]}{nodeId}")))
            {
                var mutationName = mutation.Value.Split(':')[0].Split(".")[1];
                if (mutationName.Matches(joinKeys[i]))
                {
                    mutationName = mutation.Value.Split(':')[1];
                }

                mutationName = mutationName.Substring(0, 1).ToUpper() +
                               mutationName.Substring(1, mutationName.Length - 1);
                var columnAlias = columns.Split(',').FirstOrDefault(c => c.Contains(mutationName));
                
                if (!string.IsNullOrEmpty(columnAlias))
                {
                    if (!mutation.Value.Contains("'") && columnAlias.Contains("Internal") && mutation.Value
                            .Contains(columnAlias.Split("AS")[0].Split('~')[1].Trim().Trim('"')))
                    {
                        for (var j = 0; j < entitiesGenerated.Count; j++)
                        {
                            nodeIds.TryGetValue(entitiesGenerated[i], out var nodeMutationId);
                            upsertSql[i] = upsertSql[i]
                                .Replace($"~{entitiesGenerated[j].ToLower()}~{nodeMutationId}~{mutationName.Trim('"')}",
                                    $"{columnAlias.Split("AS")[1].Trim()}");
                        }
                    }
                    else
                    {
                        upsertSql[i] = upsertSql[i]
                            .Replace($"~{entitiesGenerated[i].ToLower()}~{nodeId}~{mutationName.Trim('"')}",
                                $"{columnAlias.Split("AS")[1].Trim()}");
                    }
                }
            }
        }
    }
}

public class Ordering
{
    public int Index { get; set; }
    public string? Value { get; set; }
}