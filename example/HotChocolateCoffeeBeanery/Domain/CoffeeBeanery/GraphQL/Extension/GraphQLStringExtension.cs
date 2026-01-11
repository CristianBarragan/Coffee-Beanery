namespace CoffeeBeanery.GraphQL.Extension;

public static class GraphQLStringExtension
{
    /// <summary>
    /// Extension method for comparing two strings
    /// </summary>
    /// <param name="input"></param>
    /// <param name="comparison"></param>
    /// <returns></returns>
    public static bool Matches(this string? input, string comparison)
    {
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        return string.Compare(input, comparison, StringComparison.InvariantCultureIgnoreCase) == 0;
    }

    /// <summary>
    /// Extension method to convert a string (column) adding underscore for uniqueness
    /// </summary>
    /// <param name="input"></param>
    /// <param name="numberOfSnakeChar"></param>
    /// <returns></returns>
    public static string ToSnakeCase(this string input, int numberOfSnakeChar = 1)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var underscored = string.Empty;
        for (var i = 0; i < numberOfSnakeChar; i++)
        {
            underscored += '_';
        }

        return input + underscored;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="field"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    public static string ToFieldName(this string field, string separator = "~")
    {
        if (string.IsNullOrEmpty(field))
        {
            return field;
        }

        var fieldSplit = field.Split(separator);
        return fieldSplit[1].Sanitize();
    }

    public static string ToEntityName(this string field, char separator = '~')
    {
        if (string.IsNullOrEmpty(field))
        {
            return field;
        }

        var fieldSplit = field.Split(separator);
        return fieldSplit[0];
    }

    public static string ToNodeId(this string field, char separator = '~')
    {
        if (string.IsNullOrEmpty(field))
        {
            return field;
        }

        var fieldSplit = field.Split(separator);
        return string.Join("", fieldSplit[0].Skip(1));
    }

    public static string ToUpperCamelCase(this string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return field;
        }

        return field.Substring(0, 1).ToUpper() + field.Substring(1, field.Length - 1);
    }

    public static string ToLowerCamelCase(this string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return field;
        }

        return field.Substring(0, 1).ToLower() + field.Substring(1, field.Length - 1);
    }

    public static string Sanitize(this string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return field;
        }

        return field.Replace("'", "").Replace("\"", "").Trim();
    }
}