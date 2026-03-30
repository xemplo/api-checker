using Xemplo.ApiChecker.Core;

var engine = new ApiComparisonEngine();
var result = engine.Compare(ApiComparisonInput.Empty, ApiRuleProfile.Default);

Console.WriteLine($"Findings: {result.Findings.Count}");
