using System.Net.Http;
using Microsoft.OpenApi.Models;

namespace Xemplo.ApiChecker.Core;

public sealed class ApiComparisonMapBuilder : IApiComparisonMapBuilder
{
    public ApiComparisonMap Build(ApiComparisonInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var oldOperations = GetOperations(input.OldSpecification.Document).ToList();
        var newOperations = GetOperations(input.NewSpecification.Document).ToList();

        var oldOperationsByKey = oldOperations.ToDictionary(static operation => OperationKey.From(operation), OperationKeyComparer.Instance);
        var newOperationsByKey = newOperations.ToDictionary(static operation => OperationKey.From(operation), OperationKeyComparer.Instance);

        var matchedOperations = new List<ApiOperationMatch>();
        var unmatchedOldOperations = new List<ApiOperationDescription>();
        var unmatchedNewOperations = new List<ApiOperationDescription>();

        foreach (var oldOperation in oldOperations)
        {
            var key = OperationKey.From(oldOperation);
            if (newOperationsByKey.TryGetValue(key, out var newOperation))
            {
                matchedOperations.Add(MatchResponses(oldOperation, newOperation));
            }
            else
            {
                unmatchedOldOperations.Add(oldOperation);
            }
        }

        foreach (var newOperation in newOperations)
        {
            var key = OperationKey.From(newOperation);
            if (!oldOperationsByKey.ContainsKey(key))
            {
                unmatchedNewOperations.Add(newOperation);
            }
        }

        return new ApiComparisonMap(
            matchedOperations.OrderBy(static operation => operation.OldOperation.Identity.PathTemplate, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static operation => operation.OldOperation.Identity.Method, StringComparer.Ordinal)
                .ToArray(),
            unmatchedOldOperations.OrderBy(static operation => operation.Identity.PathTemplate, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static operation => operation.Identity.Method, StringComparer.Ordinal)
                .ToArray(),
            unmatchedNewOperations.OrderBy(static operation => operation.Identity.PathTemplate, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static operation => operation.Identity.Method, StringComparer.Ordinal)
                .ToArray());
    }

    private static IEnumerable<ApiOperationDescription> GetOperations(OpenApiDocument document)
    {
        foreach (var path in document.Paths)
        {
            if (path.Value is not { } pathItem)
            {
                continue;
            }

            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations)
            {
                if (operation.Value is not { } openApiOperation)
                {
                    continue;
                }

                yield return new ApiOperationDescription(
                    new ApiOperationIdentity(ToHttpMethod(operation.Key), path.Key),
                    pathItem,
                    openApiOperation);
            }
        }
    }

    private static ApiOperationMatch MatchResponses(ApiOperationDescription oldOperation, ApiOperationDescription newOperation)
    {
        var oldResponses = GetResponseDescriptions(oldOperation).ToList();
        var newResponses = GetResponseDescriptions(newOperation).ToList();
        var oldMatchedIndexes = new HashSet<int>();
        var newMatchedIndexes = new HashSet<int>();
        var oldDefaultMatchedIndexes = new HashSet<int>();
        var newDefaultMatchedIndexes = new HashSet<int>();
        var matchedResponses = new List<ApiResponseMatch>();

        MatchExactResponses(
            oldResponses,
            newResponses,
            oldMatchedIndexes,
            newMatchedIndexes,
            oldDefaultMatchedIndexes,
            newDefaultMatchedIndexes,
            matchedResponses);
        MatchDefaultFallbackResponses(
            oldResponses,
            newResponses,
            oldMatchedIndexes,
            newMatchedIndexes,
            oldDefaultMatchedIndexes,
            newDefaultMatchedIndexes,
            matchedResponses);

        return new ApiOperationMatch(
            oldOperation,
            newOperation,
            matchedResponses.OrderBy(static match => match.NewResponse.StatusCode, StringComparer.Ordinal)
                .ThenBy(static match => match.NewResponse.MediaType, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            oldResponses.Where((response, index) => !IsMatchedResponse(response, index, oldMatchedIndexes, oldDefaultMatchedIndexes))
                .OrderBy(static response => response.StatusCode, StringComparer.Ordinal)
                .ThenBy(static response => response.MediaType, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            newResponses.Where((response, index) => !IsMatchedResponse(response, index, newMatchedIndexes, newDefaultMatchedIndexes))
                .OrderBy(static response => response.StatusCode, StringComparer.Ordinal)
                .ThenBy(static response => response.MediaType, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static void MatchExactResponses(
        IReadOnlyList<ApiResponseDescription> oldResponses,
        IReadOnlyList<ApiResponseDescription> newResponses,
        ISet<int> oldMatchedIndexes,
        ISet<int> newMatchedIndexes,
        ISet<int> oldDefaultMatchedIndexes,
        ISet<int> newDefaultMatchedIndexes,
        ICollection<ApiResponseMatch> matchedResponses)
    {
        for (var oldIndex = 0; oldIndex < oldResponses.Count; oldIndex++)
        {
            var oldResponse = oldResponses[oldIndex];

            for (var newIndex = 0; newIndex < newResponses.Count; newIndex++)
            {
                if (newMatchedIndexes.Contains(newIndex))
                {
                    continue;
                }

                var newResponse = newResponses[newIndex];
                if (!StatusCodesEqual(oldResponse.StatusCode, newResponse.StatusCode)
                    || !MediaTypesEqual(oldResponse.MediaType, newResponse.MediaType))
                {
                    continue;
                }

                oldMatchedIndexes.Add(oldIndex);
                newMatchedIndexes.Add(newIndex);
                TrackDefaultMatch(oldResponse, oldIndex, oldDefaultMatchedIndexes);
                TrackDefaultMatch(newResponse, newIndex, newDefaultMatchedIndexes);
                matchedResponses.Add(new ApiResponseMatch(oldResponse, newResponse, ApiResponseMatchKind.Exact));
                break;
            }
        }
    }

    private static void MatchDefaultFallbackResponses(
        IReadOnlyList<ApiResponseDescription> oldResponses,
        IReadOnlyList<ApiResponseDescription> newResponses,
        ISet<int> oldMatchedIndexes,
        ISet<int> newMatchedIndexes,
        ISet<int> oldDefaultMatchedIndexes,
        ISet<int> newDefaultMatchedIndexes,
        ICollection<ApiResponseMatch> matchedResponses)
    {
        var oldExplicitStatusCodes = oldResponses.Where(static response => !IsDefaultStatus(response.StatusCode))
            .Select(static response => response.StatusCode)
            .ToHashSet(StringComparer.Ordinal);
        var newExplicitStatusCodes = newResponses.Where(static response => !IsDefaultStatus(response.StatusCode))
            .Select(static response => response.StatusCode)
            .ToHashSet(StringComparer.Ordinal);

        var oldDefaultResponses = oldResponses.Select(static (response, index) => (response, index))
            .Where(static item => IsDefaultStatus(item.response.StatusCode))
            .ToArray();
        var newDefaultResponses = newResponses.Select(static (response, index) => (response, index))
            .Where(static item => IsDefaultStatus(item.response.StatusCode))
            .ToArray();

        for (var oldIndex = 0; oldIndex < oldResponses.Count; oldIndex++)
        {
            if (oldMatchedIndexes.Contains(oldIndex))
            {
                continue;
            }

            var oldResponse = oldResponses[oldIndex];
            if (IsDefaultStatus(oldResponse.StatusCode) || newExplicitStatusCodes.Contains(oldResponse.StatusCode))
            {
                continue;
            }

            foreach (var (defaultResponse, defaultIndex) in newDefaultResponses)
            {
                if (!MediaTypesEqual(oldResponse.MediaType, defaultResponse.MediaType))
                {
                    continue;
                }

                oldMatchedIndexes.Add(oldIndex);
                newDefaultMatchedIndexes.Add(defaultIndex);
                matchedResponses.Add(new ApiResponseMatch(oldResponse, defaultResponse, ApiResponseMatchKind.DefaultFallback));
                break;
            }
        }

        for (var newIndex = 0; newIndex < newResponses.Count; newIndex++)
        {
            if (newMatchedIndexes.Contains(newIndex))
            {
                continue;
            }

            var newResponse = newResponses[newIndex];
            if (IsDefaultStatus(newResponse.StatusCode) || oldExplicitStatusCodes.Contains(newResponse.StatusCode))
            {
                continue;
            }

            foreach (var (defaultResponse, defaultIndex) in oldDefaultResponses)
            {
                if (!MediaTypesEqual(newResponse.MediaType, defaultResponse.MediaType))
                {
                    continue;
                }

                oldDefaultMatchedIndexes.Add(defaultIndex);
                newMatchedIndexes.Add(newIndex);
                matchedResponses.Add(new ApiResponseMatch(defaultResponse, newResponse, ApiResponseMatchKind.DefaultFallback));
                break;
            }
        }
    }

    private static IEnumerable<ApiResponseDescription> GetResponseDescriptions(ApiOperationDescription operation)
    {
        if (operation.Operation.Responses is null)
        {
            yield break;
        }

        foreach (var response in operation.Operation.Responses)
        {
            if (response.Value is not { } openApiResponse || openApiResponse.Content is null)
            {
                continue;
            }

            foreach (var content in openApiResponse.Content)
            {
                if (content.Value is null)
                {
                    continue;
                }

                if (!IsJsonMediaType(content.Key))
                {
                    continue;
                }

                yield return new ApiResponseDescription(
                    operation.Identity,
                    NormalizeStatusCode(response.Key),
                    content.Key,
                    openApiResponse,
                    content.Value);
            }
        }
    }

    private static string NormalizeStatusCode(string statusCode)
    {
        return statusCode.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? "default"
            : statusCode;
    }

    private static bool StatusCodesEqual(string left, string right)
    {
        return NormalizeStatusCode(left).Equals(NormalizeStatusCode(right), StringComparison.Ordinal);
    }

    private static bool IsDefaultStatus(string statusCode)
    {
        return statusCode.Equals("default", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MediaTypesEqual(string left, string right)
    {
        return left.Equals(right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMatchedResponse(
        ApiResponseDescription response,
        int index,
        ISet<int> matchedIndexes,
        ISet<int> defaultMatchedIndexes)
    {
        return IsDefaultStatus(response.StatusCode)
            ? defaultMatchedIndexes.Contains(index)
            : matchedIndexes.Contains(index);
    }

    private static void TrackDefaultMatch(ApiResponseDescription response, int index, ISet<int> defaultMatchedIndexes)
    {
        if (IsDefaultStatus(response.StatusCode))
        {
            defaultMatchedIndexes.Add(index);
        }
    }

    private static bool IsJsonMediaType(string mediaType)
    {
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || (mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase)
                && mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
    }

    private static string ToHttpMethod(HttpMethod operationType)
    {
        return operationType.Method.ToUpperInvariant();
    }

    private readonly record struct OperationKey(string Method, string PathTemplate)
    {
        public static OperationKey From(ApiOperationDescription operation)
        {
            return new OperationKey(operation.Identity.Method, operation.Identity.PathTemplate);
        }
    }

    private sealed class OperationKeyComparer : IEqualityComparer<OperationKey>
    {
        public static OperationKeyComparer Instance { get; } = new();

        public bool Equals(OperationKey x, OperationKey y)
        {
            return x.Method.Equals(y.Method, StringComparison.Ordinal)
                && x.PathTemplate.Equals(y.PathTemplate, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(OperationKey obj)
        {
            return HashCode.Combine(obj.Method, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PathTemplate));
        }
    }
}