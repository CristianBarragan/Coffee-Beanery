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
    public static List<string> ProcessFilter(NodeTree nodeTree,
        Dictionary<string, SqlNode> linkModelDictionaryTree, string field, string filterType, string value,
        string filterCondition)
    {
        var enumeration = string.Empty;
        var conditions = new List<string>();

        if (string.IsNullOrEmpty(field) ||
            string.IsNullOrEmpty(filterType))
        {
            return conditions;
        }

        if (linkModelDictionaryTree.TryGetValue($"{nodeTree.Name}~{field}", out var sqlNodeTo))
        {
            if (sqlNodeTo.FromEnumeration.TryGetValue(value,
                    out var enumValue))
            {
                var toEnum = sqlNodeTo.ToEnumeration.FirstOrDefault(e =>
                    e.Value.Matches(enumValue)).Value;
                enumeration = toEnum;
            }
            else
            {
                enumeration = string.Empty;
            }

            switch (filterType)
            {
                case "<>":

                    if (value.Matches("null"))
                    {
                        conditions.Add($" {filterCondition} ~.\"{sqlNodeTo.Column}\" IS NOT NULL ");
                        return conditions;
                    }

                    conditions.Add(
                        $" {filterCondition} ~.\"{sqlNodeTo.Column}\" <> '{(string.IsNullOrEmpty(enumeration) ? value : enumeration)}' ");
                    return conditions;

                case "=":

                    if (value.Matches("null"))
                    {
                        conditions.Add($" {filterCondition} ~.\"{sqlNodeTo.Column}\" IS NULL ");
                        return conditions;
                    }

                    conditions.Add(
                        $" {filterCondition} ~.\"{sqlNodeTo.Column}\" = '{(string.IsNullOrEmpty(enumeration) ? value : enumeration)}' ");
                    return conditions;

                case "in":
                    var inValues = string.Empty;
                    foreach (var val in value.Split(','))
                    {
                        var valAux = val.Sanitize().Replace("(", "").Replace(")", "").ToUpperCamelCase();
                        inValues += $"'{(string.IsNullOrEmpty(enumeration) ? valAux : enumeration)}'" + ",";
                    }

                    conditions.Add(
                        $" {filterCondition} ~.\"{sqlNodeTo.Column}\" in ({inValues.Substring(0, inValues.Length - 1)})");
                    return conditions;
            }
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
    public static string HandleSort(NodeTree nodeTree, string field, string sortClause, Dictionary<string, SqlNode> linkModelDictionaryTree)
    {
        if (linkModelDictionaryTree.TryGetValue($"{nodeTree.Name}~{field}", out var sqlNodeTo))
        {
            return $" ~*~.{sqlNodeTo.RelationshipKey.Split('~')[1]} ORDER BY {sortClause},";
        }
        return string.Empty;
    }
}