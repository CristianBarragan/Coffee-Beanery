using AutoMapper;

namespace Domain.Util.GraphQL.Helper;

using AutoMapper.Internal;

public static class GraphQLMapper
{
    public static Dictionary<string, string> GetMappings(MapperConfiguration mapper, string entity)
    {
        var configurationProvider = mapper.Internal().GetAllTypeMaps();

        var  mappings = configurationProvider
            .Where(configuration => string.Equals(configuration.SourceType.Name, entity, 
                StringComparison.CurrentCultureIgnoreCase)).ToList();

        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var mapMapping in mappings.Select(configuration =>
                     configuration.MemberMaps.Where(mm => !string.IsNullOrEmpty(mm.GetSourceMemberName()))))
        {
            foreach (var mapping in mapMapping)
            {
                if (!dictionary.ContainsKey(mapping.DestinationName))
                {
                    dictionary.Add(mapping.DestinationName, mapping.GetSourceMemberName());
                }    
            }
        }
        
        return dictionary;
    }
}