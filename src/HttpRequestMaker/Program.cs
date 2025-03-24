using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;

// Main entry point for the application
if (args.Length == 0)
{
    Console.WriteLine("Usage: HttpRequestMaker <URL> [timeout_seconds=60]");
    return 1;
}

string url = args[0];
int durationSeconds = args.Length > 1 && int.TryParse(args[1], out int duration) ? duration : 60;

Console.WriteLine($"Starting performance test for {url}");
Console.WriteLine($"Test will run for {durationSeconds} seconds");
Console.WriteLine($"Press Ctrl+C to stop the test early");
Console.WriteLine();

// Create an HttpClient factory that creates clients with proper configuration
HttpClient CreateHttpClient()
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HttpRequestMaker", "1.0"));
    return client;
}

// Get the number of processors in the system for parallelism
int processorCount = Environment.ProcessorCount;
Console.WriteLine($"Detected {processorCount} processors. Starting with {processorCount} concurrent requests.");

// Initialize test variables
var statistics = new TestStatistics();
var stopwatch = new Stopwatch();
stopwatch.Start();

// Create a cancellation token source for test duration
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
int concurrentRequests = processorCount; // Start with one request per processor
bool scaleDown = false;

// Setup performance monitoring
Timer? statsTimer = null;
var lastStats = new TestStatistics();
DateTime lastStatsTime = DateTime.Now;

statsTimer = new Timer(_ =>
{
    var currentTime = DateTime.Now;
    var elapsedSeconds = (currentTime - lastStatsTime).TotalSeconds;
    var requestsPerSecond = (statistics.TotalRequests - lastStats.TotalRequests) / elapsedSeconds;
    
    // Format data transfer rates
    string FormatDataSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        else if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F2} KB";
        else
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    var bytesSentRate = FormatDataSize((long)((statistics.BytesSent - lastStats.BytesSent) / elapsedSeconds));
    var bytesReceivedRate = FormatDataSize((long)((statistics.BytesReceived - lastStats.BytesReceived) / elapsedSeconds));

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] " +
        $"Requests: {statistics.TotalRequests} ({requestsPerSecond:F1}/sec) | " +
        $"Success: {statistics.SuccessfulRequests} ({statistics.SuccessRatePercentage:F1}%) | " +
        $"Failed: {statistics.FailedRequests} | " +
        $"Concurrent: {concurrentRequests} | " +
        $"Traffic: ↑ {bytesSentRate}/s ↓ {bytesReceivedRate}/s");

    // Update last stats for next calculation
    lastStats._totalRequests = statistics.TotalRequests;
    lastStats._successfulRequests = statistics.SuccessfulRequests;
    lastStats._failedRequests = statistics.FailedRequests;
    lastStats._bytesSent = statistics.BytesSent;
    lastStats._bytesReceived = statistics.BytesReceived;
    lastStatsTime = currentTime;

    // If all requests are successful, increase concurrency
    if (!scaleDown && statistics.FailedRequests == 0 && statistics.TotalRequests > lastStats.TotalRequests)
    {
        concurrentRequests++;
        Console.WriteLine($"Scaling up to {concurrentRequests} concurrent requests");
    }

}, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

// Create a thread-safe queue of tasks
var tasks = new ConcurrentBag<Task>();

// Run the test until cancelled
try
{
    while (!cts.IsCancellationRequested)
    {
        // Remove completed tasks
        int tasksToCreate = concurrentRequests - tasks.Count(t => !t.IsCompleted);

        // Create new tasks as needed
        for (int i = 0; i < tasksToCreate; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var client = CreateHttpClient();
                    var requestStopwatch = new Stopwatch();
                    requestStopwatch.Start();

                    var response = await client.GetAsync(url, cts.Token);
                    var responseContent = await response.Content.ReadAsByteArrayAsync(cts.Token);

                    requestStopwatch.Stop();

                    // Update statistics atomically
                    Interlocked.Increment(ref statistics._totalRequests);
                    Interlocked.Add(ref statistics._bytesSent, url.Length);
                    Interlocked.Add(ref statistics._bytesReceived, responseContent.Length);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref statistics._successfulRequests);
                    }
                    else
                    {
                        Interlocked.Increment(ref statistics._failedRequests);
                        
                        // If we have failures, scale down the concurrent requests
                        if (!scaleDown && statistics.FailedRequests > 0)
                        {
                            scaleDown = true;
                            int newConcurrentRequests = Math.Max(1, concurrentRequests / 2);
                            Console.WriteLine($"Requests failing. Scaling down to {newConcurrentRequests} concurrent requests");
                            concurrentRequests = newConcurrentRequests;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref statistics._totalRequests);
                    Interlocked.Increment(ref statistics._failedRequests);

                    // If we have failures, scale down the concurrent requests
                    if (!scaleDown && statistics.FailedRequests > 0)
                    {
                        scaleDown = true;
                        int newConcurrentRequests = Math.Max(1, concurrentRequests / 2);
                        Console.WriteLine($"Requests failing. Scaling down to {newConcurrentRequests} concurrent requests");
                        concurrentRequests = newConcurrentRequests;
                    }
                }
            }));
        }

        // Reset scale down flag periodically to allow re-scaling up
        if (scaleDown && statistics.TotalRequests % 100 == 0)
        {
            scaleDown = false;
        }

        // Small delay to avoid CPU spinning
        await Task.Delay(10);
    }
}
catch (OperationCanceledException)
{
    // Test time elapsed, continue to results
}
finally
{
    statsTimer?.Dispose();
}

// Print final results
stopwatch.Stop();
double durationMinutes = stopwatch.Elapsed.TotalMinutes;

Console.WriteLine("-------------------------------------------");
Console.WriteLine($"Performance Test Results for {url}");
Console.WriteLine("-------------------------------------------");
Console.WriteLine($"Test duration: {durationMinutes:F2} minutes");
Console.WriteLine($"Total requests: {statistics.TotalRequests}");
Console.WriteLine($"Successful requests: {statistics.SuccessfulRequests} ({statistics.SuccessRatePercentage:F1}%)");
Console.WriteLine($"Failed requests: {statistics.FailedRequests}");
Console.WriteLine($"Requests per second: {statistics.TotalRequests / stopwatch.Elapsed.TotalSeconds:F1}");

// Format final transfer sizes
string FormatTotalDataSize(long bytes)
{
    if (bytes < 1024)
        return $"{bytes} B";
    else if (bytes < 1024 * 1024)
        return $"{bytes / 1024.0:F2} KB";
    else if (bytes < 1024 * 1024 * 1024)
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    else
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
}

Console.WriteLine($"Total data sent: {FormatTotalDataSize(statistics.BytesSent)}");
Console.WriteLine($"Total data received: {FormatTotalDataSize(statistics.BytesReceived)}");
Console.WriteLine("-------------------------------------------");

return 0;

// Data structure to hold test statistics
class TestStatistics
{
    public long _totalRequests = 0;
    public long _successfulRequests = 0;
    public long _failedRequests = 0;
    public long _bytesSent = 0;
    public long _bytesReceived = 0;
    
    public long TotalRequests => _totalRequests;
    public long SuccessfulRequests => _successfulRequests;
    public long FailedRequests => _failedRequests;
    public long BytesSent => _bytesSent;
    public long BytesReceived => _bytesReceived;
    public double SuccessRatePercentage => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
}
