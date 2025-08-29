using System.Data;
using Dapper;
using Domain.Util.GraphQL.Extension;
using Domain.Util.GraphQL.Model;

namespace Domain.Util.GraphQL.Helper;

public static class SqlGraphQLHelper
{
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

    public static void AddFieldUpsert(in DynamicParameters dynamicParameters, string fieldUpsertParameter, string value)
    {
        if (Guid.TryParse(value, out var guid))
        {
            dynamicParameters.Add(fieldUpsertParameter, guid, DbType.Guid);   
        }
        else if (int.TryParse(value, out var integer))
        {
            dynamicParameters.Add(fieldUpsertParameter, integer, DbType.Int32);   
        }
        else
        {
            dynamicParameters.Add(fieldUpsertParameter, value, DbType.String);
        }
    }

    public static void 
        HandleUpsertField(List<string> insert, List<string> select, List<string> exclude, NodeTree nodeTree, List<SqlNode> sqlNodes, 
            string field, string value, bool isDatetime, int child)
    {
        string fieldUpsertParameter;
        field = nodeTree.Mappings.
            FirstOrDefault(s => s.Key.Matches(field)).Value;
        var sqlNode = default(SqlNode);
        var sqlNodeAux = default(SqlNode);
        sqlNode = sqlNodes.FirstOrDefault(s => s.NodeTree.Name.Matches(nodeTree.Name));
        
        var enumeration = string.Empty;
        if (!isDatetime)
        {
            if (field != null && nodeTree.EnumerationMappings.ContainsKey(field))
            {
                enumeration = nodeTree.EnumerationMappings[field].GetValueOrDefault(value);    
            }

            if (!string.IsNullOrEmpty(enumeration))
            {
                value = enumeration;
            }

            if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(field))
            {
                return;
            }
            
            fieldUpsertParameter = $"${nodeTree.Id}~{child}~{nodeTree.Name}~{field}";
            sqlNodeAux = HandleUpsertFieldStatement(nodeTree, field, fieldUpsertParameter, nodeTree.Id);
        }
        else
        {
            fieldUpsertParameter = $"${nodeTree.Id}~{child}~{nodeTree.Name}~{field}";
            value = $"TO_TIMESTAMP({$"{fieldUpsertParameter}\""}, " +
                    $"'YYYY-MM-DD T HH24:MI:SS:MS Z')::timestamptz at time zone 'Etc/UTC'";
            sqlNodeAux = HandleUpsertFieldStatement(nodeTree, field, fieldUpsertParameter, nodeTree.Id);
        }
        
        InsertIfDoesNotExist(insert, sqlNodeAux.InsertColumns[0]);
        if (sqlNodeAux.UpdateColumns.Count > 0)
        {
            InsertIfDoesNotExist(exclude, sqlNodeAux.UpdateColumns[0]);    
        }
        
        if (sqlNode != default(SqlNode))
        {
            sqlNodeAux.SqlNodeType = SqlNodeType.Mutation;
            if (sqlNode.Values.ContainsKey(fieldUpsertParameter))
            {
                var listValue = sqlNode.Values[fieldUpsertParameter];
                if (listValue != null)
                {
                    listValue.Add($"{nodeTree.Id}~{child-1}~{field}~{value}");
                }
            }
            else
            {
                var listValue = new List<string>();
                listValue.Add($"{nodeTree.Id}~{child-1}~{field}~{value}");
                sqlNode.Values.Add(fieldUpsertParameter, listValue);    
            }
        }
        else if (!string.IsNullOrEmpty(value))
        {
            sqlNodeAux.SqlNodeType = SqlNodeType.Mutation;
            var listValue = sqlNodeAux.Values.GetValueOrDefault(fieldUpsertParameter);
            listValue = new List<string>();
            listValue.Add($"{nodeTree.Id}~{child}~{field}~{value}");
            sqlNodeAux.Values.Add(fieldUpsertParameter, listValue);    
            sqlNodes.Add(sqlNodeAux);
        }
    }

    private static void InsertIfDoesNotExist(List<string> list, string value)
    {
        if (!string.IsNullOrEmpty(value) && !list.Contains(value))
        {
            list.Add(value);
        }
    }

    private static SqlNode HandleUpsertFieldStatement(NodeTree nodeTree, string field, string value, int id)
    {
        var sqlNode = new SqlNode();

        if (string.IsNullOrEmpty(value))
        {
            return sqlNode;
        }
        
        sqlNode.NodeTree = nodeTree;
        sqlNode.InsertColumns.Add(field);
        sqlNode.SelectColumns.Add($"{value} AS {field}");
        if (!nodeTree.UpsertKeys.Contains(field))
        {
            sqlNode.UpdateColumns.Add($"\"{field}\" = EXCLUDED.\"{field}\"");    
        }
        return sqlNode;
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

    public static List<string> ProcessFilter(NodeTree nodeTree, string field, string filterType, string value, string filterCondition)
    {
        var enumeration = string.Empty;
        var conditions = new List<string>(); 

        if (string.IsNullOrEmpty(field))
        {
            return conditions;
        }
        
        switch (filterType)
        {
            case "<>":
                field = nodeTree.Mappings.FirstOrDefault(s =>
                    s.Key.Matches(field)).Value;
                if (value.Matches("null"))
                {
                    conditions.Add($" {filterCondition} ~.\"{field}\" IS NOT NULL ");
                    return conditions;
                }
                enumeration = nodeTree.EnumerationMappings.FirstOrDefault(s => s.Key == field)
                    .Value.FirstOrDefault(v => v.Key.Matches(value.ToUpperCamelCase())).Value;
                conditions.Add(
                    $" {filterCondition} ~.\"{field}\" <> '{(string.IsNullOrEmpty(enumeration) ? value : enumeration)}' ");
                return conditions;
            
            case "=":
                field = nodeTree.Mappings.FirstOrDefault(s =>
                    s.Key.Matches(field)).Value;
                if (value.Matches("null"))
                {
                    conditions.Add($" {filterCondition} ~.\"{field}\" IS NULL ");
                    return conditions;
                }
                enumeration = nodeTree.EnumerationMappings.FirstOrDefault(s => s.Key == field)
                    .Value.FirstOrDefault(v => v.Key.Matches(value.ToUpperCamelCase())).Value;
                conditions.Add(
                    $" {filterCondition} ~.\"{field}\" = '{(string.IsNullOrEmpty(enumeration) ? value : enumeration)}' ");
                return conditions;
            
            case "in":
                field = nodeTree.Mappings.FirstOrDefault(s =>
                    s.Key.Matches(field)).Value;
                var inValues = string.Empty;
                foreach (var val in value.Split(','))
                {
                    var valAux = val.Sanitize().Replace("(", "").Replace(")", "").ToUpperCamelCase();
                    enumeration = nodeTree.EnumerationMappings.FirstOrDefault(s => s.Key == field).Value?
                        .FirstOrDefault(v => v.Key.Matches(valAux)).Value;
                    inValues += $"'{(string.IsNullOrEmpty(enumeration) ? valAux : enumeration)}'" + ",";
                    conditions.Add($" {filterCondition} ~.\"{field}\" = '{(string.IsNullOrEmpty(enumeration) ? valAux : enumeration)}'");
                }
                conditions.Add($" {filterCondition} ~.\"{field}\" in ({inValues.Substring(0, inValues.Length - 1)})");
                return conditions;
        }
        
        return conditions;
    }
    
    public static string HandleSort(NodeTree nodeTree, string field, string sortClause)
    {
        
        field = nodeTree.Mappings[field];
        return $" ~*~.{field} ORDER BY {sortClause},";
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