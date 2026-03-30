namespace Xemplo.ApiChecker.Core;

public sealed record ApiComparisonInput(object OldSpecification, object NewSpecification)
{
    public static ApiComparisonInput Empty { get; } = new(new object(), new object());
}