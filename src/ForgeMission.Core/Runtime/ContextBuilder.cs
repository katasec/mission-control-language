using ForgeMission.Parser;

namespace ForgeMission.Core.Runtime;

public static class ContextBuilder
{
    public static Dictionary<string, object> Seed(
        Program ast,
        IReadOnlyDictionary<string, string>? vars = null,
        IReadOnlyDictionary<string, object>? objects = null)
    {
        var context = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var binding in ast.Bindings)
            context[binding.Name] = ResolveLetValue(binding.Value, binding.Name);

        context["output"] = string.Empty;

        if (vars is not null)
            foreach (var (key, value) in vars)
                context[key] = value;

        if (objects is not null)
            foreach (var (key, value) in objects)
                context[key] = value;

        return context;
    }

    internal static string ResolveLetValue(LetValue value, string bindingName) => value switch
    {
        StringLetValue v => v.Text,
        EnvLetValue v    => ResolveEnv(v.VarName, v.DefaultValue, bindingName),
        _                => throw new InvalidOperationException($"Unknown let value type for '{bindingName}'")
    };

    internal static string ResolveEnv(string varName, string? defaultValue, string bindingName)
    {
        var val = Environment.GetEnvironmentVariable(varName);
        if (val is not null) return val;
        if (defaultValue is not null) return defaultValue;
        throw new InvalidOperationException(
            $"Required environment variable '{varName}' (used by let binding '{bindingName}') is not set.");
    }

    internal static string ResolveBindingValue(BindingValue value, Dictionary<string, object> context) => value switch
    {
        StringBindingValue v => v.Text,
        NumberBindingValue v => v.Number.ToString(),
        VarRefBindingValue v => context.TryGetValue(v.Name, out var ctx)
            ? ctx.ToString()!
            : throw new InvalidOperationException($"Variable '{v.Name}' not found in context"),
        EnvBindingValue v    => ResolveEnv(v.VarName, v.DefaultValue, v.VarName),
        _                    => throw new InvalidOperationException("Unknown binding value type")
    };
}
