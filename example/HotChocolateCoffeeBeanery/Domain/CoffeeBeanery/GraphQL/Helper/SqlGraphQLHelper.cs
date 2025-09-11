using CoffeeBeanery.GraphQL.Extension;
using CoffeeBeanery.GraphQL.Model;

namespace CoffeeBeanery.GraphQL.Helper;

public static class SqlGraphQLHelper
{
    /// <summary>
    /// Method to translate filter from entity model fields into data model columns 
    /// </summary>
    /// <param name="nodeTree"></param>
    /// <param name="field"></param>
    /// <param name="filterType"></param>
    /// <param name="value"></param>
    /// <param name="filterCondition"></param>
    /// <returns></returns>
    public static List<string> ProcessFilter(NodeTree nodeTree, string field, string filterType, string value,
        string filterCondition)
    {
        var enumeration = string.Empty;
        var conditions = new List<string>();

        if (string.IsNullOrEmpty(field))
        {
            return conditions;
        }

        var fieldMap = nodeTree.Mappings.FirstOrDefault(s =>
            s.FieldSourceName.Matches(field))!;
        // enumeration = fieldMap.DestinationEnumerationValues.FirstOrDefault(s => s.Key == field).Value;
        
        switch (filterType)
        {
            case "<>":
                
                if (value.Matches("null"))
                {
                    conditions.Add($" {filterCondition} ~.\"{fieldMap.FieldDestinationName}\" IS NOT NULL ");
                    return conditions;
                }
                
                conditions.Add(
                    $" {filterCondition} ~.\"{field}\" <> '{(string.IsNullOrEmpty(enumeration) ? value : enumeration)}' ");
                return conditions;

            case "=":
                
                if (value.Matches("null"))
                {
                    conditions.Add($" {filterCondition} ~.\"{field}\" IS NULL ");
                    return conditions;
                }
                
                conditions.Add(
                    $" {filterCondition} ~.\"{field}\" = '{(string.IsNullOrEmpty(enumeration) ? value : enumeration)}' ");
                return conditions;

            case "in":
                var inValues = string.Empty;
                foreach (var val in value.Split(','))
                {
                    var valAux = val.Sanitize().Replace("(", "").Replace(")", "").ToUpperCamelCase();
                    inValues += $"'{(string.IsNullOrEmpty(enumeration) ? valAux : enumeration)}'" + ",";
                    conditions.Add(
                        $" {filterCondition} ~.\"{field}\" = '{(string.IsNullOrEmpty(enumeration) ? valAux : enumeration)}'");
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
        field = nodeTree.Mappings.First(f => f.FieldSourceName.Matches(field)).FieldDestinationName;
        return $" ~*~.{field} ORDER BY {sortClause},";
    }
}