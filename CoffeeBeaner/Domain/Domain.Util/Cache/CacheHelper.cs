using Domain.Util.GraphQL.Model;
using Newtonsoft.Json;

namespace Domain.Util.Cache;

public static class CacheHelper
{
    public static string GetKey(Dictionary<string, string> dictionary)
    {
        return string.Join("~", dictionary.Keys) ;
    }
    
    public static string GetKey(GraphQLStructure graphQLStructure)
    {
        return JsonConvert.SerializeObject(graphQLStructure);
    }
    
    public static M GetJson<M>(string graphQLStructure) where M : class
    {
        return JsonConvert.DeserializeObject<M>(graphQLStructure);
    }
}