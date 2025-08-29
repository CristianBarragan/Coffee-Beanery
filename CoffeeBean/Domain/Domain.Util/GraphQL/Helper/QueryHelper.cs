using Dapper;
using Domain.Util.GraphQL.Extension;
using Domain.Util.GraphQL.Model;
namespace Domain.Util.GraphQL.Helper;

public static class QueryHelper
{
    public static DynamicParameters GetParameters(NodeTree nodeTree, SqlStructure query)
    {
        var queryParameters = new DynamicParameters();
        
        // foreach (var valuePair in query.Mutations)
        // {
        //     if (Guid.TryParse(valuePair.Value.Sanitize(), out var guid))
        //     {
        //         queryParameters.Add(valuePair.Key.Replace("~", ""), guid, DbType.Guid);   
        //     }
        //     else if (int.TryParse(valuePair.Value.Sanitize(), out var integer))
        //     {
        //         queryParameters.Add(valuePair.Key.Replace("~", ""), integer, DbType.Int32);   
        //     }
        //     else
        //     {
        //         queryParameters.Add(valuePair.Key.Replace("~", ""), valuePair.Value.Sanitize(), DbType.String);
        //     }
        // }
        return queryParameters;
    }

    public static void IterateTreeNode(NodeTree nodeTree, string id, string field, string value, List<string> filters)
    {
        var valueField = string.Empty;

        if ((nodeTree.Id.ToString().Matches(id) || nodeTree.Name.Matches(id)) && nodeTree.EnumerationMappings.Keys.Any(k => k.Matches(field)))
        {
            valueField = nodeTree.EnumerationMappings[field].GetValueOrDefault(value.Trim('\'').Trim().ToUpperCamelCase());
            if (!string.IsNullOrEmpty(valueField))
            {
                filters.Add(valueField);
            }
        }

        foreach (var nodeTreeChild in nodeTree.Children)
        {
            IterateTreeNode(nodeTreeChild, id, field, value, filters);
        }
    }
}