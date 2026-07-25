using System.Text.Json;

namespace ForgeMission.Core.Tools;

// Arguments arrive as IDictionary<string, object?>, but the object shape differs by caller:
// a real provider round-trip deserializes JSON into JsonElement values; a test or an in-memory
// caller may hand back plain CLR types directly. Both need to work.
internal static class ToolArguments
{
    public static bool TryGetString(IDictionary<string, object?>? arguments, string key, out string value)
    {
        value = "";
        if (arguments is null || !arguments.TryGetValue(key, out var raw) || raw is null) return false;

        value = raw switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString() ?? "",
            _ => raw.ToString() ?? "",
        };
        return true;
    }

    public static int? TryGetInt(IDictionary<string, object?>? arguments, string key)
    {
        if (arguments is null || !arguments.TryGetValue(key, out var raw) || raw is null) return null;

        return raw switch
        {
            int i => i,
            long l => (int)l,
            JsonElement { ValueKind: JsonValueKind.Number } je => je.GetInt32(),
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null,
        };
    }

    public static bool GetBool(IDictionary<string, object?>? arguments, string key, bool defaultValue = false)
    {
        if (arguments is null || !arguments.TryGetValue(key, out var raw) || raw is null) return defaultValue;

        return raw switch
        {
            bool b => b,
            JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False } je => je.GetBoolean(),
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => defaultValue,
        };
    }
}
