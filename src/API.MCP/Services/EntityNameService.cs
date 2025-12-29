namespace API.MCP.Services;

public interface IEntityNameService
{
    string ConvertPluralToSingular(string entityName);
    string PreserveCasing(string original, string converted);
}

public class EntityNameService : IEntityNameService
{
    public string ConvertPluralToSingular(string entityName)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return entityName;
        if (entityName.Length < 3 || !entityName.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return entityName;
        var lowerName = entityName.ToLowerInvariant();
        var irregularPlurals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "children", "child" },
            { "people", "person" },
            { "men", "man" },
            { "women", "woman" },
            { "feet", "foot" },
            { "teeth", "tooth" },
            { "geese", "goose" },
            { "mice", "mouse" }
        };
        if (irregularPlurals.TryGetValue(lowerName, out var irregularSingular))
            return PreserveCasing(entityName, irregularSingular);
        if (lowerName.EndsWith("ies"))
            return entityName.Substring(0, entityName.Length - 3) + "y";
        else if (lowerName.EndsWith("ves"))
            return entityName.Substring(0, entityName.Length - 3) + "fe";
        else if (lowerName.EndsWith("ses") || lowerName.EndsWith("ches") || lowerName.EndsWith("shes") || lowerName.EndsWith("xes"))
            return entityName.Substring(0, entityName.Length - 2);
        else if (lowerName.EndsWith("s") && !lowerName.EndsWith("ss") && !lowerName.EndsWith("us"))
            return entityName.Substring(0, entityName.Length - 1);
        return entityName;
    }
    public string PreserveCasing(string original, string converted)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(converted))
            return converted;
        var result = new char[converted.Length];
        for (int i = 0; i < converted.Length && i < original.Length; i++)
            result[i] = char.IsUpper(original[i]) ? char.ToUpper(converted[i]) : converted[i];
        for (int i = original.Length; i < converted.Length; i++)
            result[i] = converted[i];
        return new string(result);
    }
}
