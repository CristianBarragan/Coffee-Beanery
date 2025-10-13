namespace CoffeeBeanery.GraphQL.Extension;

public static class GraphQLFieldExtension
{
    /// <summary>
    /// Check if a visited property is a primitive type
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    public static bool IsPrimitiveType(Type m)
    {
        return m == typeof(string) || m == typeof(bool) ||
               m == typeof(DateTime) ||
               m == typeof(decimal) || m == typeof(int);
    }
}