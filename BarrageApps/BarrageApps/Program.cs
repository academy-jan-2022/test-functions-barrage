// See https://aka.ms/new-console-template for more information

using DeFuncto.Extensions;
using Flurl.Http;
using Polly;
using Polly.Retry;

var requests = await Enumerable.Range(0, 150)
    .Select<int, Func<Task<(Guid id, int expected)>>>(
        number => async () => await Functions.Initiate(number % 12)()
    )
    .Parallel(25);

Console.WriteLine("Checking results");

var results = await requests
        .Select<(Guid id, int expected), Func<Task<(bool isSuccessful, string? message)>>>(
            tuple => async () => await Functions.Check(tuple.id, tuple.expected)()
        )
        .Parallel(50);

var successCount = results.Count(r => r.isSuccessful);
var failures = results.Where(r => !r.isSuccessful).Select(r => r.message);

Console.WriteLine($"{successCount} operations were successful");
foreach (var failure in failures)
{
    Console.WriteLine($"Failure: {failure}");
}

public static class Values
{
    public const string GenerateUrl = "https://team-api-academy.azurewebsites.net/api/GenerateGUID";
    public const string FileUrl = "https://team-api-academy.azurewebsites.net/api/GetFile/";
}

public static class Functions
{
    private static readonly AsyncRetryPolicy RetryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            10,
            count => TimeSpan.FromSeconds((double)count / 2)
        );

    public static Func<Task<(Guid id, int expected)>> Initiate(int number) =>
        async () => await RetryPolicy.ExecuteAsync(async () =>
        {
            var expected = number * number;
            var payload = new Request(number);
            var result = await Values.GenerateUrl.PostJsonAsync(payload);
            var id = await result.GetStringAsync();
            return (Guid.Parse(id.Replace("\"", "")), expected);
        });

    public static Func<Task<(bool isSuccessful, string? message)>> Check(Guid id, int expected) =>
        async () =>
        {
            try
            {
                return await RetryPolicy.ExecuteAsync(async () =>
                {
                    var url = $"{Values.FileUrl}{id}";
                    var result = await url.GetAsync();
                    var number = await result.GetStringAsync();
                    var parsedNumber = int.Parse(number);
                    return
                        parsedNumber == expected
                            ? (true, null)
                            : (false, $"For {id} expected {expected} but got {parsedNumber}");
                });
            }
            catch (Exception exception)
            {
                return (false, $"For id {id} with expectation: {expected} failed with message: \n{exception.Message}");
            }
        };
}

public record Request(int Number);
