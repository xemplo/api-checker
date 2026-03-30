namespace Xemplo.ApiChecker.Core;

public sealed record ApiJsonSchemaAnalysis
{
    public static ApiJsonSchemaAnalysis Empty { get; } = new(Array.Empty<ApiJsonSchemaNode>());

    public ApiJsonSchemaAnalysis(IReadOnlyList<ApiJsonSchemaNode> nodes)
    {
        Nodes = nodes;
    }

    public IReadOnlyList<ApiJsonSchemaNode> Nodes { get; }

    public ApiJsonSchemaNode? FindNode(string schemaPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);

        return Nodes.FirstOrDefault(node => node.SchemaPath.Equals(schemaPath, StringComparison.Ordinal));
    }
}