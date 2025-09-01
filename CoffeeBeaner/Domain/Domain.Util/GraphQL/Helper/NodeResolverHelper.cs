using Domain.Util.GraphQL.Extension;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using Domain.Util.GraphQL.Model;

namespace Domain.Util.GraphQL.Helper;

public static class NodeResolverHelper
{
    public static GraphQLStructure ResolveMutation(IResolverContext resolverContext, string entityName, List<string> entities,
        NodeTree nodeTree, List<KeyValuePair<string, string>> nodeId)
    {
        var whereList = new List<string>();
        var whereConditionList = new List<string>();
        var nodeSelectionList = new List<string>();
        var rootNodeName = entityName;
        var listOfFields = new List<string>();
        var conditionsProcessed = new List<string>();
        var mutations = new Dictionary<string, string>();
        var level = 1;
        
        foreach (var argument in resolverContext.Selection.SyntaxNode.Arguments)
        {
            if (argument.Name.ToString().Contains("where"))
            {
                foreach (var orderNode in argument.GetNodes())
                {
                    GetFieldsWhere(orderNode, rootNodeName, mutations, listOfFields, Entity.ClauseTypes, whereList,
                        conditionsProcessed, whereConditionList, entities, entityName, nodeTree, nodeId);
                }
            }

            if (argument.Name.Value.Matches(entityName))
            {
                GetFields(argument, entities, nodeSelectionList, mutations, rootNodeName, nodeId, string.Empty);
            }
        }

        var mutationStructure = new GraphQLStructure
        {
            NodeSelect = nodeSelectionList.ToList(), Filter = whereList, FilterConditions = whereConditionList,
            Mutations = mutations
        };
        return mutationStructure;
    }

    public static GraphQLStructure ResolveQuery(IResolverContext resolverContext, string entityName, List<string> entities,
        NodeTree nodeTree, List<KeyValuePair<string,string>> nodeId)
    {
        var sortList = new List<string>();
        var whereList = new List<string>();
        var whereConditionList = new List<string>();
        var nodeSelectionList = new List<string>();
        var edgeSelectionList = new List<string>();
        var listOfFields = new List<string>();
        var conditionsProcessed = new List<string>();
        var mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var first = "";
        var last = "";
        var before = "";
        var after = "";
        var level = 1;
        foreach (var argument in resolverContext.Selection.SyntaxNode.Arguments)
        {
            switch (argument.Name.ToString())
            {
                case "first":
                    first = argument.Value?.Value?.ToString();
                    break;
                case "last":
                    last = argument.Value?.Value?.ToString();
                    break;
                case "before":
                    before = argument.Value?.Value?.ToString();
                    break;
                case "after":
                    after = argument.Value?.Value?.ToString();
                    break;
            }

            if (argument.Name.ToString().Contains("order"))
            {
                foreach (var orderNode in argument.GetNodes())
                {
                    GetFieldsOrdering(orderNode, entityName, sortList);
                }
            }

            if (argument.Name.ToString().Contains("where"))
            {
                foreach (var orderNode in argument.GetNodes())
                {
                    GetFieldsWhere(orderNode, entityName, mutations, listOfFields, Entity.ClauseTypes, whereList,
                        conditionsProcessed, whereConditionList, entities, entityName, nodeTree, nodeId);
                }
            }
        }

        foreach (var selectionNode in resolverContext.Selection.SelectionSet?.Selections!)
        {
            if (selectionNode.ToString().Contains("edges"))
            {
                GetFields(selectionNode.GetNodes().ToList()[1].GetNodes().ToList()[0], entities,
                    edgeSelectionList, null, entityName, nodeId, string.Empty);
            }
            else
            {
                GetFields(selectionNode, entities, nodeSelectionList, null, entityName, nodeId, string.Empty);
            }
        }

        var query = new GraphQLStructure
        {
            NodeSelect = nodeSelectionList, EdgeSelect = edgeSelectionList, Filter = whereList,
            FilterConditions = whereConditionList, Sort = sortList,
            Pagination = new Pagination
            {
                After = after, Before = before, First = string.IsNullOrEmpty(first) ? 0 : int.Parse(first),
                TotalPageRecords = new TotalPageRecords(){ PageRecords = 0 }, 
                TotalRecordCount = new TotalRecordCount() { RecordCount = 0 },
                Last = string.IsNullOrEmpty(last) ? 0 : int.Parse(last)
            },
            HasTotalCount = resolverContext.Selection.SelectionSet.Selections.Count > 1 &&
                            resolverContext.Selection.SelectionSet.Selections.Any(s =>
                                s.ToString().ToLower().Contains("totalCount".ToLower())),
            Mutations = mutations
        };
        return query;
    }

    public static void GetFields(ISyntaxNode selectionNode, List<string> entities, List<string> selectList,
        Dictionary<string, string>? mutations, string nodeName, List<KeyValuePair<string,string>> nodeId,
        string mutationName)
    {
        var auxNodeName = entities.FirstOrDefault(e => e.Matches(selectionNode.ToString().Split(':')[0].Trim()));

        if (auxNodeName == null)
        {
            auxNodeName = entities.FirstOrDefault(e => e.Matches(selectionNode.ToString().Split('{')[0].Trim()));
        }

        if (auxNodeName != null)
        {
            nodeName = auxNodeName;
        }
        
        var id = nodeId.FirstOrDefault(n => n.Key.Matches(nodeName)).Value;
        
        var childIndex = 1;
        foreach (var select in selectionNode.GetNodes())
        {
            mutationName = $"{childIndex}~{nodeName}";
            if (IsFilter(select) || entities.Any(e => e.Matches(select.ToString())) || select.ToString().Contains("node"))
            {
                continue;
            }

            if (select.ToString().Split('{').Length > 1)
            {
                GetFields(select, entities, selectList, mutations, nodeName, nodeId, mutationName);
            }
            else
            {
                if (select.ToString().Split(':').Length == 2 && mutations != null)
                {
                    AddToMutationDictionary(mutations,
                        $"@{id}~{select.ToString().Split(':')[0].Trim()}~Mutation",
                        select.ToString().Split(':')[1].Trim().Replace('"', '\''), mutationName);
                    var field = $"@{id}~{select.ToString().Split(':')[0].Trim()}";
                    if (!selectList.Contains(field) && !string.IsNullOrEmpty(field))
                    {
                        selectList.Add(field);
                    }

                    continue;
                }

                if (select.ToString().Split(':').Length == 4 && select.ToString().Split('.')[1].Length == 5 &&
                    select.ToString().Split('.')[1][3] == 'Z' && mutations != null)
                {
                    AddToMutationDictionary(mutations,
                        $"@{id}~{select.ToString().Split(':')[0].Trim()}~Mutation",
                        string.Join('`', select.ToString().Split(':').Skip(1).ToList()).Replace('"', '\''), mutationName);
                    var field = $"@{id}~{select.ToString().Split(':')[0].Trim()}";
                    if (!selectList.Contains(field) && !string.IsNullOrEmpty(field))
                    {
                        selectList.Add(field);
                    }

                    continue;
                }

                if (select.ToString().Split('~').Length == 1 && mutations == null)
                {
                    var field = $"{nodeName}~{select.ToString()}";
                    if (!selectList.Contains(field) && !string.IsNullOrEmpty(field))
                    {
                        selectList.Add(field);
                    }
                }
            }

            childIndex++;
        }
    }

    private static bool IsFilter(ISyntaxNode node)
    {
        return !(!node.ToString().Contains("ASC") && !node.ToString().Contains("DESC") &&
                 !node.ToString().Contains("endCursor") && !node.ToString().Contains("hasNextPage") &&
                 !node.ToString().Contains("hasPreviousPage") && !node.ToString().Contains("startCursor"));
    }

    private static void AddToMutationDictionary(Dictionary<string, string> mutations, string nodeName, string nodeValue, string mutationName)
    {
        if (mutations.TryGetValue(mutationName, out var mutation))
        {
            // if (!mutation.TryGetValue(nodeName, out var value))
            // {
                mutations.Add(nodeName, nodeValue);
            // }
        }
        // else
        // {
        //     var newMutation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        //     newMutation.Add(nodeName, nodeValue);
        //     mutations.Add(mutationName, newMutation);
        // }
    }

    public static void GetFieldsOrdering(ISyntaxNode selectionNode, string entity, List<string> listOfOrders)
    {
        foreach (var select in selectionNode.GetNodes())
        {
            var currentEntity = entity;
            if (select.ToString().Contains("{") && select.ToString()[0] != '{' && select.ToString().Contains(":"))
            {
                currentEntity = select.ToString().Split(":")[0];
            }

            if (!select.ToString().Contains("{") && select.ToString().Contains(":"))
            {
                var column = select.ToString().Split(":");
                if (column[1].Contains("DESC") || column[1].Contains("ASC"))
                {
                    var table = $"{currentEntity[0].ToString().ToUpper() + currentEntity.Substring(1)}";
                    var columnName = $"\"{column[0][0].ToString().ToUpper() + column[0].Substring(1)}\"";
                    var orderBy = $"{table}.{columnName}{column[1]}";
                    if (!listOfOrders.Contains(orderBy))
                    {
                        listOfOrders.Add(orderBy);
                    }
                }
            }

            GetFieldsOrdering(select, currentEntity, listOfOrders);
        }
    }

    public static void GetFieldsWhere(ISyntaxNode selectionNode, string entity, Dictionary<string, string> mutations, 
        List<string> listOfFields, List<string> clauseTypes, List<string> listOfClauses, List<string> conditionsProcessed,
        List<string> whereConditionList, List<string> expectedEntities, string entityName, NodeTree nodeTree,
        List<KeyValuePair<string,string>> nodeId)
    {
        var currentEntity = entity;
        var i = 0;
        
        var id = nodeId.FirstOrDefault(n => n.Key.Matches(entityName)).Value;
        
        foreach (var select in selectionNode.GetNodes())
        {
            if ((selectionNode.ToString().TrimStart(' ').StartsWith("and:") ||
                 selectionNode.ToString().TrimStart(' ').StartsWith("or:")) &&
                !conditionsProcessed.Contains(selectionNode.ToString()))
            {
                var conditionArray = selectionNode.ToString().Split("{");
                whereConditionList.Add(conditionArray[0].Replace(" ", "").Replace(":", ""));
                conditionsProcessed.Add(selectionNode.ToString());
            }

            if (!(selectionNode.ToString().TrimStart(' ').StartsWith("{")) &&
                selectionNode.ToString().Split("{").Length >= 3 && selectionNode.ToString().Contains(":"))
            {
                var entityArray = selectionNode.ToString().Split(":");
                if (expectedEntities.Any(e => e.ToString().Matches(entityArray[0])))
                {
                    currentEntity = entityArray[0];
                }
                else if (string.Compare(entityArray[0], "all", StringComparison.InvariantCultureIgnoreCase) != 0)
                {
                    currentEntity = entityName;
                }
            }

            if (select.ToString().Contains("{") && select.ToString().Contains(":") &&
                select.ToString().Split(":").Length == 3)
            {
                var column = select.ToString().Split(":");
                if (!column[1].Contains("DESC") && !column[1].Contains("ASC") && !column[0].Contains("{"))
                {
                    var table = $"{currentEntity[0].ToString().ToUpper() + currentEntity.Substring(1)}";
                    var columnName = $"\"{column[0][0].ToString().ToUpper() + column[0].Substring(1)}\"";
                    var field = $"{table}.{columnName}";
                    if (!listOfFields.Contains(field))
                    {
                        listOfFields.Add(field);
                    }
                }
            }

            var nodes = select.GetNodes().ToList();
            var childIndex = 1;
            foreach (var node in nodes)
            {
                var mutationName = $"{childIndex}~{node}";
                if (!node.ToString().Contains("{") && node.ToString().Contains(":") &&
                    node.ToString().Split(":").Length == 2)
                {
                    var column = node.ToString().Split(":");
                    if (!column[1].Contains("DESC") && !column[1].Contains("ASC") && clauseTypes.Contains(column[0]))
                    {
                        var clauseValue = "";
                        var clauseType = "";
                        switch (column[0])
                        {
                            case "eq":
                            {
                                clauseValue = column[1];
                                clauseType = "=";
                                var filterClause =
                                    $"{listOfFields[listOfFields.Count - 1]} {clauseType} '{clauseValue.Replace("\" ", "\"").Replace("\"", "").TrimStart(' ')}'";
                                if (!listOfClauses.Contains(filterClause))
                                {
                                    AddToMutationDictionary(mutations, $"@{id}~{listOfFields[listOfFields.Count - 1].ToFieldName('.').Trim('"').Trim()}~Filter",
                                        clauseValue.Replace("\" ", "\"").Replace("\"", "").TrimStart(' '), mutationName);
                                    listOfClauses.Add(filterClause);
                                }

                                break;
                            }
                            case "neq":
                            {
                                clauseValue = column[1];
                                clauseType = "<>";
                                break;
                            }
                            case "in":
                            {
                                clauseValue = "(" + string.Join(',',
                                    column[1].Replace("[", "").Replace("]", "").Split(',')
                                        .Select(v => $"'{v.Trim()}'")) + ")";
                                clauseType = "in";
                                
                                var filterClause =
                                    $"{listOfFields[listOfFields.Count - 1]} {clauseType} '{clauseValue.Replace("\" ", "\"").Replace("\"", "").TrimStart(' ')}'";
                                if (!listOfClauses.Contains(filterClause))
                                {
                                    AddToMutationDictionary(mutations, $"@{id}~{listOfFields[listOfFields.Count - 1].ToFieldName('.').Trim('"').Trim()}~Filter",
                                        clauseValue.Replace("\" ", "\"").Replace("\"", "").TrimStart(' '), mutationName);
                                    listOfClauses.Add(filterClause);
                                    listOfClauses.Add(filterClause);
                                }
                                
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(clauseValue))
                        {
                            i++;
                        }
                    }
                }

                childIndex++;
            }

            GetFieldsWhere(select, currentEntity, mutations, listOfFields, clauseTypes, listOfClauses, conditionsProcessed,
                whereConditionList, expectedEntities, entityName, nodeTree, nodeId);
        }
    }
}

public class Entity
{
    public static List<string> ClauseTypes = new List<string>()
    {
        "eq",
        "neq",
        "in",
        "any"
    };
}