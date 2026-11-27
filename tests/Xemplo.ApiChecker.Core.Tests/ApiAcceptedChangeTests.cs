namespace Xemplo.ApiChecker.Core.Tests;

public class ApiAcceptedChangeTests
{
    private static readonly ApiFinding Finding = new(
        ApiRuleId.RemovedOutput,
        ApiSeverity.Error,
        "removed",
        new ApiOperationIdentity("GET", "/api/v1/lookups/{key}"),
        "$.title",
        "400");

    [Fact]
    public void Matches_WithRuleOnly_MatchesAnyFindingForThatRule()
    {
        Assert.True(new ApiAcceptedChange(ApiRuleId.RemovedOutput, "reason").Matches(Finding));
        Assert.False(new ApiAcceptedChange(ApiRuleId.RemovedInput, "reason").Matches(Finding));
    }

    [Theory]
    [InlineData("GET /api/v1/lookups/{key}", true)]
    [InlineData("get /api/v1/lookups/{key}", true)]
    [InlineData("*", true)]
    [InlineData("PUT /api/v1/lookups/{key}", false)]
    [InlineData("GET /api/v1/lookups", false)]
    public void Matches_FiltersByOperation(string operation, bool expected)
    {
        Assert.Equal(expected, new ApiAcceptedChange(ApiRuleId.RemovedOutput, "reason", Operation: operation).Matches(Finding));
    }

    [Theory]
    [InlineData("400", true)]
    [InlineData("*", true)]
    [InlineData("200", false)]
    public void Matches_FiltersByStatusCode(string status, bool expected)
    {
        Assert.Equal(expected, new ApiAcceptedChange(ApiRuleId.RemovedOutput, "reason", StatusCode: status).Matches(Finding));
    }

    [Fact]
    public void Matches_WithStatusFilter_DoesNotMatchFindingWithoutStatus()
    {
        var change = new ApiAcceptedChange(ApiRuleId.RemovedOutput, "reason", StatusCode: "400");

        Assert.False(change.Matches(Finding with { StatusCode = null }));
    }

    [Fact]
    public void Matches_FiltersBySchemaPathList()
    {
        var change = new ApiAcceptedChange(ApiRuleId.RemovedOutput, "reason", SchemaPaths: ["$.type", "$.title"]);

        Assert.True(change.Matches(Finding));
        Assert.False(change.Matches(Finding with { SchemaPath = "$.detail" }));
        Assert.False(change.Matches(Finding with { SchemaPath = null }));
    }

    [Fact]
    public async Task LoadFromFile_ParsesAcceptedChangesAndKeepsBaseProfileEntries()
    {
        var baseProfile = new ApiRuleProfile(
            ApiRuleCatalog.CreateDefaultSeverities(),
            [new ApiAcceptedChange(ApiRuleId.NewEndpoint, "base")]);

        var profile = await LoadAsync("""
            {
              "rules": { "output:removed": "error" },
              "accepted": [
                { "rule": "output:removed", "operation": "*", "status": "400", "path": ["$.type", "$.title"], "reason": "Moved to Result" },
                { "rule": "output:removed", "operation": "GET /api/v1/questions/{questionId}/answers", "path": "$.id", "reason": "Wrong schema" }
              ]
            }
            """, baseProfile);

        Assert.Collection(
            profile.AcceptedChanges,
            change => Assert.Equal("base", change.Reason),
            change =>
            {
                Assert.Equal(ApiRuleId.RemovedOutput, change.RuleId);
                Assert.Equal("*", change.Operation);
                Assert.Equal("400", change.StatusCode);
                Assert.Equal(["$.type", "$.title"], change.SchemaPaths);
                Assert.Equal("Moved to Result", change.Reason);
            },
            change =>
            {
                Assert.Equal("GET /api/v1/questions/{questionId}/answers", change.Operation);
                Assert.Null(change.StatusCode);
                Assert.Equal(["$.id"], change.SchemaPaths);
            });
    }

    [Fact]
    public void ApplyOverrides_KeepsAcceptedChanges()
    {
        var baseProfile = new ApiRuleProfile(
            ApiRuleCatalog.CreateDefaultSeverities(),
            [new ApiAcceptedChange(ApiRuleId.NewEndpoint, "base")]);

        var profile = ApiRuleProfileResolver.ApplyOverrides(
            baseProfile,
            new Dictionary<ApiRuleId, ApiSeverity> { [ApiRuleId.NewEndpoint] = ApiSeverity.Error });

        Assert.Single(profile.AcceptedChanges);
        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.NewEndpoint));
    }

    [Theory]
    [InlineData("""{ "rules": {}, "accepted": {} }""", "Configuration 'accepted' must be an array.")]
    [InlineData("""{ "rules": {}, "accepted": ["output:removed"] }""", "accepted[0] must be a JSON object.")]
    [InlineData("""{ "rules": {}, "accepted": [{ "reason": "x" }] }""", "accepted[0] must have a 'rule'.")]
    [InlineData("""{ "rules": {}, "accepted": [{ "rule": "output:gone", "reason": "x" }] }""", "accepted[0] rule 'output:gone' is not supported.")]
    [InlineData("""{ "rules": {}, "accepted": [{ "rule": "output:removed" }] }""", "accepted[0] must have a 'reason'.")]
    [InlineData("""{ "rules": {}, "accepted": [{ "rule": "output:removed", "reason": " " }] }""", "accepted[0] 'reason' must be a non-empty string.")]
    [InlineData("""{ "rules": {}, "accepted": [{ "rule": "output:removed", "reason": "x", "status": 400 }] }""", "accepted[0] 'status' must be a non-empty string.")]
    [InlineData("""{ "rules": {}, "accepted": [{ "rule": "output:removed", "reason": "x", "operation": "/pets" }] }""", "accepted[0] operation '/pets' must be '*' or 'METHOD /path'.")]
    [InlineData("""{ "rules": {}, "accepted": [{ "rule": "output:removed", "reason": "x", "path": [] }] }""", "accepted[0] 'path' must be a non-empty string or a non-empty list of non-empty strings.")]
    [InlineData("""{ "rules": {}, "accepted": [{ "rule": "output:removed", "reason": "x", "path": ["$.a", 1] }] }""", "accepted[0] 'path' must be a non-empty string or a non-empty list of non-empty strings.")]
    [InlineData("""{ "rules": {}, "accepted": [{ "rule": "output:removed", "reason": "x", "paths": "$.a" }] }""", "accepted[0] has unsupported property 'paths'.")]
    public async Task LoadFromFile_WithInvalidAcceptedChange_ThrowsClearError(string json, string expectedMessage)
    {
        var exception = await Assert.ThrowsAsync<ApiRuleProfileConfigurationException>(() => LoadAsync(json));

        Assert.Equal(expectedMessage, exception.Message);
    }

    private static async Task<ApiRuleProfile> LoadAsync(string json, ApiRuleProfile? baseProfile = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"api-checker-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json);

        try
        {
            return await ApiRuleProfileResolver.LoadFromFileAsync(path, baseProfile);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
