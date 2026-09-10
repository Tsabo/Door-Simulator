using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DoorSim.OpenApi;

/// <summary>
/// Documents enum schemas with the meaning of each named value, read from
/// <see cref="DescriptionAttribute" /> on the enum type and its members. The built-in
/// OpenAPI generator only lists the value names (or, pre-string-enum, raw integers) —
/// this fills in what each one actually means.
/// </summary>
public sealed class EnumDescriptionSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (!type.IsEnum)
            return Task.CompletedTask;

        var lines = new List<string>();

        var typeDescription = type.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (typeDescription is not null)
            lines.Add(typeDescription);

        foreach (var name in Enum.GetNames(type))
        {
            var memberDescription = type.GetField(name)!.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (memberDescription is not null)
                lines.Add($"- {name}: {memberDescription}");
        }

        if (lines.Count > 0)
            schema.Description = string.Join("\n", lines);

        return Task.CompletedTask;
    }
}
