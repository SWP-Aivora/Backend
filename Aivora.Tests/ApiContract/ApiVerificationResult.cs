using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Aivora.Tests.ApiContract;

public class ApiVerificationResult
{
    public required string Flow { get; set; }
    public required string Method { get; set; }
    public required string Path { get; set; }
    public required int ExpectedStatus { get; set; }
    public required int ActualStatus { get; set; }
    public required bool RequestMatchesDoc { get; set; }
    public required bool ResponseMatchesDoc { get; set; }
    public required string Result { get; set; }
    public string? FailureReason { get; set; }
}

public static class ApiVerificationTracker
{
    private static readonly ConcurrentBag<ApiVerificationResult> Results = new();

    public static void Record(
        string flow,
        string method,
        string path,
        int expectedStatus,
        int actualStatus,
        bool requestMatchesDoc,
        bool responseMatchesDoc,
        string? failureReason = null)
    {
        var result = (actualStatus == expectedStatus && requestMatchesDoc && responseMatchesDoc) ? "TRUE" : "FALSE";
        Results.Add(new ApiVerificationResult
        {
            Flow = flow,
            Method = method,
            Path = path,
            ExpectedStatus = expectedStatus,
            ActualStatus = actualStatus,
            RequestMatchesDoc = requestMatchesDoc,
            ResponseMatchesDoc = responseMatchesDoc,
            Result = result,
            FailureReason = failureReason
        });
    }

    public static void ExportResults()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "verification_results.json");
        var orderedResults = Results
            .OrderBy(r => r.Flow).ThenBy(r => r.Path).ThenBy(r => r.Method).ThenBy(r => r.ExpectedStatus).ToList();
        var json = JsonSerializer.Serialize(orderedResults, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json);
    }

    public static IReadOnlyCollection<ApiVerificationResult> GetResults() => Results;
}
