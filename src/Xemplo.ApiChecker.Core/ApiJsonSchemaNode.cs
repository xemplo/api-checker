namespace Xemplo.ApiChecker.Core;

public sealed record ApiJsonSchemaNode(
    string SchemaPath,
    string? PropertyName,
    ApiJsonSchemaNodeKind Kind,
    ApiSchemaUsage Usage,
    ApiSchemaCondition Required,
    ApiSchemaCondition Nullable,
    ApiSchemaCondition HasDefault,
    IReadOnlyList<string> EnumValues,
    IReadOnlyList<string> AmbiguityReasons);