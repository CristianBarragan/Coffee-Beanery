using CoffeeBeanery.GraphQL.Extension;
using CoffeeBeanery.GraphQL.Model;

namespace CoffeeBeanery.GraphQL.Helper;

public static class SqlHelper
{
    /// <summary>
    /// Method for adding pagination into the query SQL statement 
    /// </summary>
    /// <param name="rootTree"></param>
    /// <param name="sqlQuery"></param>
    /// <param name="sqlOrderStatement"></param>
    /// <param name="pagination"></param>
    /// <param name="hasTotalCount"></param>
    public static string HandleQueryClause(NodeTree rootTree, string sqlQuery, string sqlOrderStatement,
        Pagination pagination, bool hasTotalCount = false)
    {
        var from = 1;
        var to = pagination!.PageSize;
        var sqlWhereStatement = string.Empty;

        if (!string.IsNullOrEmpty(pagination!.After) && pagination.First > 0 &&
            hasTotalCount)
        {
            from = int.Parse(pagination.After) + 1;
            to = from + pagination.First!.Value;
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN {from} AND {to}"
                : $" AND \"RowNumber\" BETWEEN {from} AND {to}";
        }
        else if (!string.IsNullOrEmpty(pagination?.Before) && pagination.Last > 0 &&
                 hasTotalCount)
        {
            to = int.Parse(pagination.Before) - 1;
            from = to - pagination.Last!.Value;
            to = to >= 1 ? to : 1;
            from = from >= 1 ? from : 1;
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN {from} AND {to}"
                : $" AND \"RowNumber\" BETWEEN {from} AND {to}";
        }
        else if (pagination!.First > 0 && pagination!.Last > 0 && hasTotalCount)
        {
            to = pagination!.First!.Value;
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN {pagination!.First} AND {pagination!.Last}"
                : $" AND \"RowNumber\" BETWEEN {pagination!.First} AND {pagination!.Last}";
        }
        else if (pagination!.First > 0 && hasTotalCount)
        {
            to = pagination!.First!.Value;
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN {pagination!.First} AND \"RowNumber\""
                : $" AND \"RowNumber\" BETWEEN {pagination!.First} AND \"RowNumber\"";
        }
        else if (pagination!.Last > 0 && hasTotalCount)
        {
            sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
                ? $" WHERE \"RowNumber\" BETWEEN \"RowNumber\" - {pagination!.Last} AND \"RowNumber\""
                : $" AND \"RowNumber\" BETWEEN \"RowNumber\" - {pagination!.Last} AND \"RowNumber\"";
        }
        else
        {
            to = 0;
            from = 0;
        }

        var hasPagination = pagination.First > 0 || pagination.Last > 0 ||
                            (pagination.First > 0 &&
                             !string.IsNullOrEmpty(pagination.After)) ||
                            (pagination.Last > 0 &&
                             !string.IsNullOrEmpty(pagination.Before));
        var sql = $"WITH {rootTree.Schema}s AS (SELECT * FROM (SELECT * FROM (" + sqlQuery + $") {rootTree.Name} ) ";
        var totalCount = hasPagination && hasTotalCount
            ? $" DENSE_RANK() OVER({sqlOrderStatement}) AS \"RowNumber\","
            : "";
        sqlQuery = ($" {sql} a ) " +
                    $"SELECT * FROM ( SELECT (SELECT COUNT(DISTINCT \"{"Id".ToSnakeCase(rootTree.Id)}\") FROM {rootTree.Schema}s) \"RecordCount\", " +
                    $"{totalCount} * FROM {rootTree.Schema}s) a {sqlWhereStatement.Replace('~', 'a')}");
        return sqlQuery;
    }

    /// <summary>
    /// Generate upserts 
    /// </summary>
    /// <param name="trees"></param>
    /// <param name="sqlUpsertStatementNodes"></param>
    /// <param name="entityNames"></param>
    /// <param name="sqlWhereStatement"></param>
    /// <returns></returns>
    public static string GenerateUpsertStatements(Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode> sqlNodes, string rootEntityName, string wrapperEntityName,
        Dictionary<string, string> generatedQuery, Dictionary<string, SqlNode> sqlUpsertStatementNodes, NodeTree currentTree,
        List<string> entityNames, Dictionary<string, string> sqlWhereStatement, List<string> entitiesProcessed)
    {
        var sqlUpsert = string.Empty;
        var upsertColumn = new KeyValuePair<string, SqlNode>();

        if (entitiesProcessed.Contains(currentTree.Name))
        {
            return string.Empty;
        }
        
        entitiesProcessed.Add(currentTree.Name);

        var processingTree = currentTree;
        var whereParentValue = sqlWhereStatement.GetValueOrDefault(processingTree.ParentName);
        var whereParentClause = string.Empty;
        if (!string.IsNullOrEmpty(whereParentValue))
        {
            whereParentClause = $" WHERE {whereParentValue.Replace("~", processingTree.ParentName)}";
        }

        var whereCurrentValue = sqlWhereStatement.GetValueOrDefault(processingTree.Name);
        var whereCurrentClause = string.Empty;

        if (!string.IsNullOrEmpty(whereCurrentClause) && string.IsNullOrEmpty(whereParentClause))
        {
            whereCurrentClause = $" WHERE {whereCurrentValue.Replace("~", processingTree.Name)}";
        }

        if (!string.IsNullOrEmpty(whereCurrentClause) && !string.IsNullOrEmpty(whereParentClause))
        {
            whereCurrentClause += $" {whereParentClause} {whereCurrentValue.Replace("~", processingTree.Name)}";
        }

        var upsertingEntity = sqlUpsertStatementNodes.FirstOrDefault(s =>
            s.Value.Entity.Matches(processingTree.Name) || s.Value.JoinKeys
                .Any(a => a.From.Split('~')[0].Matches(processingTree.Name)));

        if (upsertingEntity.Value == null ||
            sqlUpsertStatementNodes.FirstOrDefault(s =>
                s.Value.Entity.Matches(processingTree.Name) || s.Value.UpsertKeys
                    .Any(a => a.Split('~')[0].Matches(processingTree.Name))).Value == null)
        {
            return string.Empty;
        }
        
        var sql = string.Empty;

        upsertColumn = sqlUpsertStatementNodes.FirstOrDefault(a =>
            a.Value.Entity.Matches(currentTree.Name) &&
            a.Value.UpsertKeys.First().Split('~')[1].Matches(a.Value.Column));
        
        if (upsertColumn.Value != null)
        {
            if (sqlUpsertStatementNodes.Any() && sqlUpsertStatementNodes.Any(a => a.Value.IsGraph) && 
                upsertColumn.Value.JoinKeys.All(a => sqlUpsertStatementNodes.Any(b => 
                    b.Key.Matches(a.To))))
            {
                sql = GenerateUpsertGraph(processingTree, trees, sqlUpsertStatementNodes, 
                    sqlNodes, whereCurrentClause, entityNames, generatedQuery);
            }
            else
            {
                sql = GenerateUpsert(processingTree, trees, sqlUpsertStatementNodes, 
                    sqlNodes, whereCurrentClause, entityNames, generatedQuery);
            }

            if (!string.IsNullOrEmpty(sql))
            {
                GenerateSelectUpsert(processingTree, sqlNodes, entityNames,
                    trees, sqlUpsertStatementNodes, sqlWhereStatement, new List<string>(), generatedQuery);
            }
        }
        
        foreach (var childTree in currentTree.Children)
        {
            GenerateUpsertStatements(trees, sqlNodes, rootEntityName, wrapperEntityName, generatedQuery,
                sqlUpsertStatementNodes, childTree,
                entityNames, sqlWhereStatement, entitiesProcessed);
        }
        
        return sqlUpsert;
    }

    public static bool AddGeneratedQuery(Dictionary<string, string> generatedQuery, bool isSelectUpsert, string NodeId, string entity, string column, string sql)
    {
        var upsertTypeIndex = isSelectUpsert ? "2" : "1"; 
        
        if (!generatedQuery.ContainsKey($"{NodeId}{upsertTypeIndex}{entity}~{column}~{sql.Length}"))
        {
            generatedQuery.Add($"{NodeId}{upsertTypeIndex}{entity}~{column}~{sql.Length}", sql);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Generate the main upsert without "Join columns [Ids]"
    /// </summary>
    /// <param name="currentTree"></param>
    /// <param name="trees"></param>
    /// <param name="sqlUpsertStatementNodes"></param>
    /// <param name="whereClause"></param>
    /// <returns></returns>
    public static string GenerateUpsertGraph(NodeTree currentTree, Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode> sqlUpsertStatementNodes,
        Dictionary<string, SqlNode> sqlNodes, string whereClause, List<string> entityNames, 
        Dictionary<string, string> generatedQuery)
    {
        var sqlUpsertAux = string.Empty;
        var sqlUpsert = string.Empty;
        var upsertColumn = new KeyValuePair<string, SqlNode>();

        var currentColumns = sqlUpsertStatementNodes.Where(a => 
            a.Key.Split('~')[0].Matches(currentTree.Name)).ToList();
        
        if (currentColumns.Count == 0)
        {
            foreach (var fieldToUpsert in sqlUpsertStatementNodes.First().Value
                         .JoinKeys.DistinctBy(a => a.To.Split('~')[1]))
            {
                var childTree = trees[fieldToUpsert.From.Split('~')[0]];
                
                var upsertKey = sqlUpsertStatementNodes.FirstOrDefault(a =>
                    a.Value.Entity.Matches(currentTree.Name) &&
                    a.Value.UpsertKeys.First().Split('~')[1].Matches(a.Value.Column));
        
                if (upsertKey.Value == null)
                {
                    return string.Empty;
                }
                
                var column = childTree.Mapping.First(f => f
                    .DestinationEntity.Matches(upsertKey.Value.Entity)).FieldSourceName;
                
                sqlUpsert += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                             $"\"{column}\") VALUES ('{upsertKey.Value.Value}') " +
                             $" ON CONFLICT" +
                             $" (\"{upsertKey.Value.Column}\") ";
                sqlUpsert += $" DO NOTHING {whereClause}";
                
                if (AddGeneratedQuery(generatedQuery, false, currentTree.Id.ToString(), currentTree.Name, upsertKey.Value.Column, sqlUpsert))
                {
                    sqlUpsertAux += sqlUpsert + " ; ";
                }

                if (currentColumns.Count != 0)
                {
                    //Need a validation to make sure all the graph fields are present
                    sqlUpsert =
                        $" SELECT * FROM cypher('{currentColumns.First().Value.Graph}', $$ MERGE (p:{currentTree.Name} {{ {
                            (string.Join(",", currentColumns.Where(a => a.Value.IsColumnGraph).Select(a => $"{a.Value.Column}: '{a.Value.Value
                            }'").ToList()))}}});";

                    if (AddGeneratedQuery(generatedQuery, false, currentTree.Id.ToString(), $"{currentTree.Name}",
                            upsertKey.Value.UpsertKeys.First().Split('~')[1], sqlUpsert))
                    {
                        sqlUpsertAux += sqlUpsert;
                    }
                }

                sqlUpsert = string.Empty;
            }
        }
        else
        {
            var upsertKey = sqlUpsertStatementNodes.FirstOrDefault(a =>
                a.Value.Entity.Matches(currentTree.Name) &&
                a.Value.UpsertKeys.First().Split('~')[1].Matches(a.Value.Column));

            if (upsertKey.Value == null)
            {
                return string.Empty;
            }
            
            sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                            $" {string.Join(",", currentColumns.Select(s => $"\"{s.Value.Column}\"").ToList())}) VALUES ({
                                string.Join(",", currentColumns.Select(s => $"'{s.Value.Value}'").ToList())}) " +
                            $" ON CONFLICT" +
                            $" (\"{upsertKey.Value.Column}\") ";
            
            var exclude = new List<string>();
            
            exclude.AddRange(string.Join(",", currentColumns.Select(a => $"\"{a.Value.Column}\" = EXCLUDED.\"{a.Value.Column}\"")));
            
            if (exclude.Count > 0)
            {
                sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)} {whereClause}";
            }
            else
            {
                sqlUpsertAux += $" DO NOTHING {whereClause}";
            }
            
            if (AddGeneratedQuery(generatedQuery, false, currentTree.Id.ToString(), currentTree.Name, currentColumns.First().Value.UpsertKeys.First().Split('~')[1], sqlUpsertAux))
            {
                sqlUpsertAux += sqlUpsertAux;
            }
            
            upsertColumn = currentColumns.FirstOrDefault(a =>
                a.Value.Entity.Matches(currentTree.Name) &&
                a.Value.UpsertKeys.First().Split('~')[1].Matches(a.Value.Column));

            if (upsertColumn.Value == null)
            {
                return string.Empty;
            }

            if (currentColumns.Any() &&
                currentColumns.All(a => currentColumns.FirstOrDefault(a => 
                        a.Value.UpsertKeys.Any(b => b.Matches(a.Key))).Value.LinkKeys
                    .Any(b => b.From.Matches(a.Key))))
            {
                sqlUpsert = $" ;CREATE TEMP TABLE temp_merge AS SELECT 1 FROM cypher('{currentTree.Name}{"Edge"}', $$ MERGE (p:{currentTree.Name} {{ {
                    (string.Join(",", currentColumns.Where(a => !a.Value.Column.Matches(
                        a.Value.UpsertKeys.First().Split('~')[1])).Select(a => $"{a.Value.Column}: '{a.Value.Value
                    }'").ToList()))}}}) RETURN p $$) AS (p agtype); DROP TABLE temp_merge;";
            }
            
            if (AddGeneratedQuery(generatedQuery, false, currentTree.Id.ToString(), $"{currentTree.Name}", currentColumns.First().Value.UpsertKeys.First().Split('~')[1], sqlUpsert))
            {
                sqlUpsertAux += sqlUpsert;
            } 
        }
        
        return sqlUpsertAux;
    }

    /// <summary>
    /// Generate the main upsert without "Join columns [Ids]"
    /// </summary>
    /// <param name="currentTree"></param>
    /// <param name="trees"></param>
    /// <param name="sqlUpsertStatementNodes"></param>
    /// <param name="whereClause"></param>
    /// <returns></returns>
    public static string GenerateUpsert(NodeTree currentTree, Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode> sqlUpsertStatementNodes,
        Dictionary<string, SqlNode> sqlNodes, string whereClause, List<string> entities,
        Dictionary<string, string> generatedQuery)
    {
        var sqlUpsertAux = string.Empty;
        var parentTree = currentTree;
        var upsertColumn = new KeyValuePair<string, SqlNode>();

        var currentColumns = sqlUpsertStatementNodes
            .Where(k => k.Value.Entity.Matches(currentTree.Name) 
                        && !string.IsNullOrEmpty(k.Value.Value) && k.Value.SqlNodeType == SqlNodeType.Mutation)
            .ToList();
        
        var upsertKeys = currentColumns.FirstOrDefault();
        
        if (currentColumns.Count == 0 || (upsertKeys.Value != null &&
            !upsertKeys.Value.UpsertKeys.All(a=> currentColumns.Any(b => 
                b.Value.Column.Matches(a.Split('~')[1])))))
        {
            return sqlUpsertAux;
        }
        
        var exclude = new List<string>();

        upsertColumn = currentColumns.FirstOrDefault(a => 
            a.Value.Entity.Matches(currentTree.Name) &&
            a.Value.UpsertKeys.First().Split('~')[1].Matches(a.Value.Column));
        
        if (upsertColumn.Value == null)
        {
            return string.Empty;
        }

        if (currentColumns.Any() && currentColumns.Any(a => a.Value.IsGraph) && 
            upsertColumn.Value.JoinKeys.All(a => currentColumns.Any(b => 
                b.Key.Matches(a.To))))
        {
            foreach (var column in currentColumns.Last().Value.LinkKeys)
            {
                currentTree = trees[column.To.Split('~')[0]];
                
                if (entities.Contains(column.To.Split('~')[1]))
                {
                    continue;
                }

                exclude.Clear();

                var value = currentColumns.FirstOrDefault(l => l.Value.Column
                    .Matches(column.To.Split('~')[1]));

                if (value.Value == null)
                {
                    continue;
                }
                
                sqlUpsertAux += $" INSERT INTO \"{parentTree.Schema}\".\"{parentTree.Name}\" ( " +
                                $" {string.Join(",", currentColumns.DistinctBy(l => l.Value.Value).Select(s => $"\"{s.Value.Column}\"").ToList())}) VALUES ({
                                    string.Join(",", currentColumns.DistinctBy(l => l.Value.Value).Select(s => $"'{s.Value.Value}'").ToList())}) " +
                                $" ON CONFLICT" +
                                $" ({string.Join(",", value.Value.UpsertKeys
                                    .Select(s => $"\"{s.Split('~')[1]}\"").ToList())}) ";

                exclude.Add($"\"{value.Value.Column}\" = EXCLUDED.\"{value.Value.Column}\"");
                exclude.Add($"\"{value.Value.UpsertKeys.First().Split('~')[1]}\" = EXCLUDED.\"{value.Value.UpsertKeys.First().Split('~')[1]}\"");

                if (exclude.Count > 0)
                {
                    sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)} {whereClause}";
                }
                else
                {
                    sqlUpsertAux += $" DO NOTHING {whereClause}";
                }
            }
        }
        else
        {
            exclude.Clear();
            currentTree = parentTree;
            currentColumns = sqlUpsertStatementNodes
                .Where(k => k.Value.Entity.Matches(currentTree.Name) 
                            && !string.IsNullOrEmpty(k.Value.Value) && k.Value.SqlNodeType == SqlNodeType.Mutation)
                .ToList();
        
            if (currentColumns.Count == 0)
            {
                return sqlUpsertAux;
            }
        
            sqlUpsertAux += $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                            $" {string.Join(",", currentColumns.DistinctBy(l => l.Value.Value).Select(s => $"\"{s.Value.Column}\"").ToList())}) VALUES ({
                                string.Join(",", currentColumns.DistinctBy(l => l.Value.Value).Select(s => $"'{s.Value.Value}'").ToList())}) " +
                            $" ON CONFLICT" +
                            $" ({string.Join(",", currentColumns.LastOrDefault().Value.UpsertKeys
                                .Select(s => $"\"{s.Split('~')[1]}\"").ToList())}) ";
    
            exclude.AddRange(
                currentColumns.Where(c => c.Value.UpsertKeys
                        .Any(u => !u.Matches(c.Value.Entity))).DistinctBy(l => l.Value.Value)
                    .Select(e => $"\"{e.Value.Column}\" = EXCLUDED.\"{e.Value.Column}\"")
            );

            if (exclude.Count > 0)
            {
                sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)} {whereClause}";
            }
            else
            {
                sqlUpsertAux += $" DO NOTHING {whereClause}";
            }
        }

        if (!AddGeneratedQuery(generatedQuery, false, currentTree.Id.ToString(), currentTree.Name, currentColumns.First().Value.UpsertKeys.First().Split('~')[1], sqlUpsertAux))
        {
            return string.Empty;
        }
        
        return sqlUpsertAux;
    }

    /// <summary>
    /// Generate the upsert for "Join columns [Ids]"
    /// </summary>
    /// <param name="currentTree"></param>
    /// <param name="entityNames"></param>
    /// <param name="trees"></param>
    /// <param name="sqlUpsertStatementNodes"></param>
    /// <param name="whereClause"></param>
    /// <returns></returns>
    public static string GenerateSelectUpsert(NodeTree currentTree, Dictionary<string, SqlNode> sqlNodes, 
        List<string> entityNames,
        Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode> sqlUpsertStatementNodes,
        Dictionary<string, string> sqlWhereStatement, List<string> entitiesProcessed,
        Dictionary<string, string> generatedQuery)
    {
        var upsertColumn = new KeyValuePair<string, SqlNode>();
        if (entitiesProcessed.Contains(currentTree.Name))
        {
            return string.Empty;
        }
        
        entitiesProcessed.Add(currentTree.Name);

        var sqlUpsertQuery = string.Empty;
        var sqlUpsertAux = string.Empty;
        var hasUpsert = true;
        
        var currentColumns = sqlUpsertStatementNodes
            .Where(k => k.Value.Entity.Matches(currentTree.Name) 
                        && !string.IsNullOrEmpty(k.Value.Value) && k.Value.SqlNodeType == SqlNodeType.Mutation)
            .ToList();

        if (currentColumns.Count == 0)
        {
            return sqlUpsertAux;
        }

        var columnValue = currentColumns.FirstOrDefault(a => a.Value.Entity
                .Matches(currentTree.Name)).Value;
        
        upsertColumn = sqlUpsertStatementNodes.FirstOrDefault(a =>
            a.Value.Entity.Matches(currentTree.Name) &&
            a.Value.UpsertKeys.First().Split('~')[1].Matches(a.Value.Column));

        if (columnValue == null)
        {
            return sqlUpsertAux;
        }
        
        if (currentColumns.Any() && currentColumns.Any(a => a.Value.IsGraph) && 
            upsertColumn.Value.JoinKeys.All(a => currentColumns.Any(b => 
                b.Key.Matches(a.To))))
        {
            foreach (var linkKey in columnValue.LinkKeys)
            {
                if (linkKey.From.Split('~')[0].Matches(currentTree.Name))
                {
                    var columns = currentColumns.ToList();
                    columns.Add(new KeyValuePair<string, SqlNode>(linkKey.From, currentColumns.Last().Value));

                    var childJoinColumn = sqlNodes
                        .FirstOrDefault(k => trees[linkKey.To.Split('~')[0]].Mapping.Any(f => f
                                .DestinationEntity.Matches(k.Value.Entity) &&
                            entityNames.Any(e => e.Matches(k.Value.Entity))));
                
                    sqlUpsertQuery = GenerateCommandGraph(columns, trees, currentTree, sqlWhereStatement, childJoinColumn, 
                        linkKey.From.Split('~')[0], linkKey.To.Split('~')[1]);

                    if (AddGeneratedQuery(generatedQuery, true, currentTree.Id.ToString(), currentTree.Name, linkKey.To.Split('~')[1], sqlUpsertQuery))
                    {
                        sqlUpsertAux += sqlUpsertQuery;    
                    }
                }
            }
        }
        else
        {
            foreach (var joinKey in columnValue.JoinKeys)
            {
                if (joinKey.To.Split('~')[0].Matches(currentTree.Name))
                {
                    var columns = currentColumns.ToList();
                
                    var parentColumns = sqlNodes
                        .Where(k => trees[joinKey.From.Split('~')[0]].Mapping.Any(f => f
                                .DestinationEntity.Matches(k.Value.Entity) &&
                            entityNames.Any(e => e.Matches(k.Value.Entity)))).ToList();
                
                    sqlUpsertQuery = GenerateCommandJoin(columns, trees, currentTree, sqlWhereStatement, parentColumns, joinKey.From.Split('~')[0]);

                    if (AddGeneratedQuery(generatedQuery, true, currentTree.Id.ToString(), currentTree.Name, joinKey.From.Split('~')[1], sqlUpsertQuery))
                    {
                        sqlUpsertAux += sqlUpsertQuery;    
                    }
                }
            }
            
            foreach (var joinOneKey in columnValue.JoinOneKeys)
            {
                if (joinOneKey.To.Split('~')[0].Matches(currentTree.Name))
                {
                    var columns = currentColumns.ToList();
                    columns.Add(new KeyValuePair<string, SqlNode>(joinOneKey.To, currentColumns.Last().Value));
                
                    var parentColumns = sqlNodes
                        .Where(k => trees[joinOneKey.From.Split('~')[0]].Mapping.Any(f => f
                                .DestinationEntity.Matches(k.Value.Entity) &&
                            entityNames.Any(e => e.Matches(k.Value.Entity)))).ToList();
          
                    sqlUpsertQuery = GenerateCommandOneJoin(columns, trees, currentTree, sqlWhereStatement, parentColumns, joinOneKey.From.Split('~')[0]);

                    if (AddGeneratedQuery(generatedQuery, true, currentTree.Id.ToString(), currentTree.Name, currentColumns.First().Value.UpsertKeys.First().Split('~')[1], sqlUpsertQuery))
                    {
                        sqlUpsertAux += sqlUpsertQuery;    
                    }
                }
            }
        }

        return sqlUpsertAux;
    }
    
    private static string GenerateCommandJoin(List<KeyValuePair<string, SqlNode>> currentColumns, Dictionary<string, NodeTree> trees,
        NodeTree currentTree, Dictionary<string, string> sqlWhereStatement, List<KeyValuePair<string, SqlNode>> parentColumns, string entity)
    {
        
        currentColumns = currentColumns.DistinctBy(a => a.Key).ToList();

        if (string.IsNullOrEmpty(entity))
        {
            return string.Empty;
        }
        
        var parentTree = trees[entity];
        
        var onConflictKey = parentColumns.FirstOrDefault(a => a.Value.Entity.Matches(entity) && !string.IsNullOrEmpty(a.Value.Value) &&
                                                              a.Value.UpsertKeys.First().Split('~')[1] == a.Value.Column);

        var childKey = currentColumns.FirstOrDefault(a => a.Value
            .UpsertKeys.Any(x => x.Split('~')[0] == currentTree.Name) && a.Value.UpsertKeys.Any(x => x.Matches(a.Key)));
        
        if (onConflictKey.Value == null || childKey.Value == null)
        {
            return string.Empty;
        }
        
        var insertJoin = $"\"{entity}Id\"";
        var selectJoin = $"{entity}.\"Id\" AS \"{entity}Id\"";
       
        if (childKey.Value.Column.Matches(onConflictKey.Value.Column))
        {
            insertJoin += $", \"{onConflictKey.Value.Column}\"";
            selectJoin += $", '{onConflictKey.Value.Value}' AS \"{onConflictKey.Value.Column}\"";    
        }
        else
        {
            insertJoin += $", \"{onConflictKey.Value.Column}\", \"{childKey.Value.Column}\"";
            selectJoin += $", '{onConflictKey.Value.Value}' AS \"{onConflictKey.Value.Column}\", '{childKey.Value.Value}' AS \"{childKey.Value.Column}\"";    
        }
        
        var excludeJoin = $"\"{entity}Id\" = EXCLUDED.\"{entity}Id\", \"{onConflictKey.Value.Column}\" = EXCLUDED.\"{onConflictKey.Value.Column}\"";

        var where = $"{entity}.\"{onConflictKey.Value.Column}\" = '{onConflictKey.Value.Value}'";
        
        if (currentColumns.Count == 0
            || 
            !currentColumns.Any(a => a.Value.UpsertKeys
                                         .Any(u => u.Split('~')[1].Matches(a.Value.Column)) && a.Value.SqlNodeType == SqlNodeType.Mutation && 
                                     !string.IsNullOrEmpty(a.Value.Value)) ||
            string.IsNullOrEmpty(where))
        {
            return string.Empty;
        }
        
        var sqlUpsertAux = $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                            insertJoin +
                            $" ) ( SELECT {selectJoin}" + $" FROM \"{parentTree.Schema}\".\"{entity}\" {entity} WHERE {
                               string.Join(" AND ",  where)}";

        var exclude = new List<string>();
        exclude.Add(excludeJoin);

        sqlUpsertAux += $" ) ON CONFLICT" +
                        $" (\"{childKey.Value.Column}\") ";

        if (exclude.Count > 0)
        {
            sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)}";
        }
        else
        {
            sqlUpsertAux += $" DO NOTHING";
        }
        
        return sqlUpsertAux;
    }

    private static string GenerateCommandOneJoin(List<KeyValuePair<string, SqlNode>> currentColumns, Dictionary<string, NodeTree> trees,
        NodeTree currentTree, Dictionary<string, string> sqlWhereStatement, List<KeyValuePair<string, SqlNode>> parentColumns, string entity)
    {
        
        currentColumns = currentColumns.DistinctBy(a => a.Key).ToList();

        if (string.IsNullOrEmpty(entity))
        {
            return string.Empty;
        }
        
        var parentTree = trees[entity];

        var insertJoin = $"\"{entity}Id\"";
        var selectJoin = $"{entity}.\"Id\" AS" +
                        $" \"{entity}Id\"";

        
        var onConflictKey = parentColumns.FirstOrDefault(a => a.Value.Entity.Matches(entity) && !string.IsNullOrEmpty(a.Value.Value) &&
                                                              a.Value.UpsertKeys.First().Split('~')[1] == a.Value.Column);
        
        var childKey = currentColumns.FirstOrDefault(a => a.Value
            .UpsertKeys.Any(x => x.Split('~')[0] == currentTree.Name) && a.Value.UpsertKeys.Any(x => x.Matches(a.Key)));

        if (onConflictKey.Value == null || childKey.Value == null)
        {
            return string.Empty;
        }

        var column = onConflictKey.Value.UpsertKeys.First().Split('~')[1];
        
        insertJoin += $", \"{column}\", \"{childKey.Value.Column}\"";
        selectJoin += $", '{onConflictKey.Value.Value}' AS \"{column}\", '{childKey.Value.Value}' AS \"{childKey.Value.Column}\"";
        
        var excludeJoin = $"\"{entity}Id\" = EXCLUDED.\"{entity}Id\"";

        var where = $"{entity}.\"{onConflictKey.Value.Column}\" = '{onConflictKey.Value.Value}'";
        
        if (currentColumns.Count == 0
            || 
            !currentColumns.Any(a => a.Value.UpsertKeys
                                         .Any(u => u.Split('~')[1].Matches(a.Value.Column)) && a.Value.SqlNodeType == SqlNodeType.Mutation && 
                                     !string.IsNullOrEmpty(a.Value.Value)) ||
            string.IsNullOrEmpty(where))
        {
            return string.Empty;
        }
        
        var sqlUpsertAux = $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                            insertJoin +
                            $" ) ( SELECT {selectJoin}" + $" FROM \"{parentTree.Schema}\".\"{entity}\" {entity} WHERE {
                               string.Join(" AND ",  where)}";

        var exclude = new List<string>();
        exclude.Add(excludeJoin);

        sqlUpsertAux += $" ) ON CONFLICT" +
                        $" (\"{childKey.Value.Column}\") ";

        if (exclude.Count > 0)
        {
            sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)}";
        }
        else
        {
            sqlUpsertAux += $" DO NOTHING";
        }
        
        return sqlUpsertAux;
    }
    
    private static string GenerateCommandGraph(List<KeyValuePair<string, SqlNode>> currentColumns, Dictionary<string, NodeTree> trees,
        NodeTree currentTree, Dictionary<string, string> sqlWhereStatement, KeyValuePair<string, SqlNode> joinColumn,
        string entity, string column)
    {
        
        currentColumns = currentColumns.DistinctBy(a => a.Key).ToList();

        if (string.IsNullOrEmpty(entity) || joinColumn.Value == null)
        {
            return string.Empty;
        }

        var childEntity = joinColumn.Value.Entity;
        var childTree = trees[childEntity];

        var columnField = currentColumns.FirstOrDefault(a => a.Value.Column == column);

        if (columnField.Value == null)
        {
            return string.Empty;
        }
        
        var insertJoin = $"\"{columnField.Value.Column.Replace("Key", "Id")}\", \"{columnField.Value.Column}\"";
        var selectJoin = $"{childEntity}.\"Id\" AS" +
                         $" \"{columnField.Value.Column.Replace("Key", "Id")}\", '{columnField.Value.Value}' AS" +
                         $" \"{columnField.Value.Column}\"";

        var onConflictKey = currentColumns.FirstOrDefault(a => a.Value
            .UpsertKeys.Any(x => x == a.Value.RelationshipKey) && a.Value.Entity.Matches(currentTree.Name));

        if (onConflictKey.Value == null)
        {
            return string.Empty;
        }
        
        insertJoin += $", \"{onConflictKey.Value.Column}\"";
        selectJoin += $", '{onConflictKey.Value.Value}' AS \"{onConflictKey.Value.Column}\"";
        
        var excludeJoin = $"\"{column.Replace("Key", "Id")}\" = EXCLUDED.\"{column.Replace("Key", "Id")}\", " +
                          $"\"{columnField.Value.Column}\" = EXCLUDED.\"{columnField.Value.Column}\"";

        var whereField =
            currentColumns.FirstOrDefault(a => a.Value.Entity.Matches(entity) && a.Value.Column.Matches(column));
        var where = 
            $"{childEntity}.\"{joinColumn.Value.UpsertKeys.First().Split('~')[1]}\" = '{whereField.Value.Value}'";
        
        if (
            currentColumns.Count == 0 
            || 
            !currentColumns.Any(a => a.Value.UpsertKeys
                                         .Any(u => u.Split('~')[1].Matches(a.Value.Column)) && a.Value.SqlNodeType == SqlNodeType.Mutation && 
                                     !string.IsNullOrEmpty(a.Value.Value)) ||
            string.IsNullOrEmpty(where))
        {
            return string.Empty;
        }
        
        var sqlUpsertAux = $" INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ( " +
                            insertJoin +
                            $" ) ( SELECT {selectJoin}" + $" FROM \"{childTree.Schema}\".\"{childTree.Name}\" {childTree.Name} WHERE {
                               string.Join(" AND ",  where)}";

        var exclude = new List<string>();
        exclude.Add(excludeJoin);

        sqlUpsertAux += $" ) ON CONFLICT" +
                        $" (\"{onConflictKey.Value.Column}\") ";

        if (exclude.Count > 0)
        {
            sqlUpsertAux += $" DO UPDATE SET {string.Join(",", exclude)}";
        }
        else
        {
            sqlUpsertAux += $" DO NOTHING";
        }
        
        return sqlUpsertAux;
    }
}