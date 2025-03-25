using System.Net.Http.Json;
using System.Text.Json;

namespace HttpRequestReceiver;

public class TestClient
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("HttpRequestReceiver Test Client");
        Console.WriteLine("-------------------------------");
        
        var client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5240")
        };

        var runId = $"test-run-{Guid.NewGuid().ToString()[..8]}";
        Console.WriteLine($"Using Run ID: {runId}");

        // Test case 1: Sequential counter values (should be valid)
        await SendSequentialCounters(client, runId, 1, 5);
        
        // Test case 2: Duplicate counter value (should generate error)
        await SendRequestWithCounter(client, runId, 3);  // Duplicate value
        
        // Test case 3: Gap in counter values (should generate error)
        await SendRequestWithCounter(client, runId, 8);  // Skips 6, 7

        // Display current errors
        await DisplayErrors(client);
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static async Task SendSequentialCounters(HttpClient client, string runId, int start, int count)
    {
        Console.WriteLine($"\nSending {count} sequential counter values starting from {start}...");
        
        for (int i = 0; i < count; i++)
        {
            await SendRequestWithCounter(client, runId, start + i);
        }
    }

    private static async Task SendRequestWithCounter(HttpClient client, string runId, int counter)
    {
        try
        {
            var data = new { counter, runID = runId };
            Console.WriteLine($"Sending request with counter={counter}, runID={runId}");
            
            var response = await client.PostAsJsonAsync("/api/data", data);
            Console.WriteLine($"Response: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending request: {ex.Message}");
        }
    }

    private static async Task DisplayErrors(HttpClient client)
    {
        try
        {
            Console.WriteLine("\nFetching current errors...");
            var response = await client.GetAsync("/api/errors");
            
            if (response.IsSuccessStatusCode)
            {
                var errors = await response.Content.ReadFromJsonAsync<List<ErrorData>>();
                Console.WriteLine($"Total errors: {errors?.Count ?? 0}");
                
                if (errors != null && errors.Any())
                {
                    Console.WriteLine("\nError details:");
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"- Counter: {error.Counter}, RunID: {error.RunID}");
                        Console.WriteLine($"  Error: {error.ErrorMessage}");
                        Console.WriteLine($"  Time: {error.Timestamp}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Failed to fetch errors: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching errors: {ex.Message}");
        }
    }
}

record ErrorData(int Counter, string RunID, string ErrorMessage, DateTime Timestamp);