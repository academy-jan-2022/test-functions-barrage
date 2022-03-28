// See https://aka.ms/new-console-template for more information

using Flurl.Http;
using Polly;
using Polly.Retry;


var (id, expected) = await Functions.Initiate(8)();
var (isMatch, error) = await Functions.Check(id, expected)();
if (isMatch)
    Console.WriteLine("Success");
else
    Console.WriteLine($"Failed {error}");

public static class Values
{
    public const string GenerateUrl = "https://team-api-academy.azurewebsites.net/api/GenerateGUID";
    public const string FileUrl = "https://team-api-academy.azurewebsites.net/api/GetFile/";
}

public static class Functions
{
    private static readonly AsyncRetryPolicy retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            15,
            count => TimeSpan.FromSeconds(count)
        );

    public static Func<Task<(Guid id, int expected)>> Initiate(int number) =>
        async () => await retryPolicy.ExecuteAsync(async () =>
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
                return await retryPolicy.ExecuteAsync(async () =>
                {
                    Console.WriteLine("Checking");
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
                return (false, exception.Message);
            }
        };
}

public record Request(int Number);
