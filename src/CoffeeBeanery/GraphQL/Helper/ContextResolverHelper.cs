using CoffeeBeanery.GraphQL.Model;
using HotChocolate.Types.Pagination;

namespace CoffeeBeanery.GraphQL.Helper;

public static class ContextResolverHelper
{
    /// <summary>
    /// Generate connection result based on entity node list and pagination
    /// </summary>
    /// <param name="entityNodes"></param>
    /// <param name="pagination"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static Connection<T> GenerateConnection<T>(IEnumerable<EntityNode<T>> entityNodes, Pagination pagination)
        where T : class
    {
        var index = 0;
        var source = (IEnumerable<CursorResult<T>>)entityNodes.Select(c => new CursorResult<T>(c.Entity,
            (++index).ToString(), typeof(T).Name)).ToList();
        var edger =
            (source != null ? source.Where(cr => cr != null).Select(cr => new Edge<T>(cr.Entity, cr.Key)) : null) ??
            Enumerable.Empty<Edge<T>>();
        var connectionInfo = new ConnectionPageInfo(((string.IsNullOrEmpty(pagination.After) &&
                                                      pagination.First < pagination.TotalRecordCount?.RecordCount) ||
                                                     !string.IsNullOrEmpty(pagination.After) &&
                                                     (int.Parse(pagination.After) +
                                                         pagination.First < pagination.TotalRecordCount?.RecordCount) ||
                                                     (!string.IsNullOrEmpty(pagination.Before) &&
                                                      int.Parse(pagination.Before) -
                                                      pagination.Last > 1)),
            ((!string.IsNullOrEmpty(pagination.After) && int.Parse(pagination.After) > 1) ||
             (!string.IsNullOrEmpty(pagination.Before) &&
              int.Parse(pagination.Before) < pagination.TotalRecordCount?.RecordCount - 1)),
            pagination.StartCursor?.ToString(), pagination.EndCursor?.ToString());

        var connection =
            new Connection<T>(edger.ToList(), connectionInfo, pagination.TotalRecordCount?.RecordCount ?? 0);
        return connection;
    }
}

public class CursorResult<T>
{
    public CursorResult(T entity, string cursor, string key)
    {
        Cursor = cursor;
        Entity = entity;
        Key = key;
    }

    public T Entity { get; }
    public string Cursor { get; }
    public string Key { get; set; }
}

public class EntityNode<T> where T : class
{
    public EntityNode(T entity, string key)
    {
        Entity = entity;
        Key = key;
    }

    public T Entity { get; set; }
    public string Key { get; set; }
}