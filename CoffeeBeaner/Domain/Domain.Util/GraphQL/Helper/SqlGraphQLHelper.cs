using Domain.Util.GraphQL.Extension;
using Domain.Util.GraphQL.Model;

namespace Domain.Util.GraphQL.Helper;

public static class SqlGraphQLHelper
{
    /// <summary>
    /// Handles upserted fields to be used for generating the SQL Statement  
    /// </summary>
    /// <param name="insert"></param>
    /// <param name="exclude"></param>
    /// <param name="nodeTree"></param>
    /// <param name="sqlNodes"></param>
    /// <param name="field"></param>
    /// <param name="value"></param>
    /// <param name="isDatetime"></param>
    /// <param name="child"></param>
    public static void 
        HandleUpsertField(List<string> insert, List<string> exclude, NodeTree nodeTree, List<SqlNode> sqlNodes, 
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

    /// <summary>
    /// Inserts an element, if it does not exist
    /// </summary>
    /// <param name="list"></param>
    /// <param name="value"></param>
    private static void InsertIfDoesNotExist(List<string> list, string value)
    {
        if (!string.IsNullOrEmpty(value) && !list.Contains(value))
        {
            list.Add(value);
        }
    }

    /// <summary>
    /// Handle the three dynamic properties required by the SQL statement
    /// </summary>
    /// <param name="nodeTree"></param>
    /// <param name="field"></param>
    /// <param name="value"></param>
    /// <param name="id"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Method to translate filter from entity model fields into data model columns 
    /// </summary>
    /// <param name="nodeTree"></param>
    /// <param name="field"></param>
    /// <param name="filterType"></param>
    /// <param name="value"></param>
    /// <param name="filterCondition"></param>
    /// <returns></returns>
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
    
    /// <summary>
    /// Method to translate sort clause from entity model fields into data model clause 
    /// </summary>
    /// <param name="nodeTree"></param>
    /// <param name="field"></param>
    /// <param name="sortClause"></param>
    /// <returns></returns>
    public static string HandleSort(NodeTree nodeTree, string field, string sortClause)
    {
        
        field = nodeTree.Mappings[field];
        return $" ~*~.{field} ORDER BY {sortClause},";
    }
}