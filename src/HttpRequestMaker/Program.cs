using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;

// Create a HttpClient for the entire application
HttpClient SharedHttpClient = new HttpClient();

// Configure the HttpClient
SharedHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
SharedHttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HttpRequestMaker", "1.0"));

// Main entry point for the application
if (args.Length == 0)
{
    Console.WriteLine("Usage: HttpRequestMaker <URL> [timeout_seconds=60] [initial_parallel_requests]");
    return 1;
}

string url = args[0];
int durationSeconds = args.Length > 1 && int.TryParse(args[1], out int duration) ? duration : 60;
int initialParallelRequests = args.Length > 2 && int.TryParse(args[2], out int initial) ? initial : 0;

Console.WriteLine($"Starting performance test for {url}");
Console.WriteLine($"Test will run for {durationSeconds} seconds");
Console.WriteLine($"Press Ctrl+C to stop the test early");
Console.WriteLine();

// Get the number of processors in the system for parallelism
int processorCount = Environment.ProcessorCount;
int concurrentRequests = initialParallelRequests > 0 ? initialParallelRequests : processorCount;
Console.WriteLine($"Detected {processorCount} processors. Starting with {concurrentRequests} concurrent requests.");

// Initialize test variables
var cumulativeStats = new TestStatistics();
var currentSecondStats = new TestStatistics();
var lastSecondStats = new TestStatistics();
var stopwatch = new Stopwatch();
stopwatch.Start();

// Create a cancellation token source for test duration
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
bool scaleDown = false;
int parallelIncreaseRate = Math.Max(1, processorCount / 2); // More aggressive scaling - add multiple parallel requests

// Setup performance monitoring
Timer? statsTimer = null;
DateTime lastStatsTime = DateTime.Now;

statsTimer = new Timer(_ =>
{
    var currentTime = DateTime.Now;
    var elapsedSeconds = (currentTime - lastStatsTime).TotalSeconds;
    
    // Update last second stats (copy current values)
    lastSecondStats._totalRequests = currentSecondStats._totalRequests;
    lastSecondStats._successfulRequests = currentSecondStats._successfulRequests;
    lastSecondStats._failedRequests = currentSecondStats._failedRequests;
    lastSecondStats._bytesSent = currentSecondStats._bytesSent;
    lastSecondStats._bytesReceived = currentSecondStats._bytesReceived;
    
    // Reset current second stats for the next interval
    currentSecondStats._totalRequests = 0;
    currentSecondStats._successfulRequests = 0;
    currentSecondStats._failedRequests = 0;
    currentSecondStats._bytesSent = 0;
    currentSecondStats._bytesReceived = 0;
    
    // Calculate rates for display
    var requestsPerSecond = lastSecondStats.TotalRequests / elapsedSeconds;
    
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

    var bytesSentRate = FormatDataSize((long)(lastSecondStats.BytesSent / elapsedSeconds));
    var bytesReceivedRate = FormatDataSize((long)(lastSecondStats.BytesReceived / elapsedSeconds));
    double successRatePercentage = lastSecondStats.TotalRequests > 0 
        ? (double)lastSecondStats.SuccessfulRequests / lastSecondStats.TotalRequests * 100 
        : 0;

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] " +
        $"Requests/sec: {requestsPerSecond:F1} | " +
        $"Success/sec: {lastSecondStats.SuccessfulRequests} ({successRatePercentage:F1}%) | " +
        $"Failed/sec: {lastSecondStats.FailedRequests} | " +
        $"Total: {cumulativeStats.TotalRequests} | " +
        $"Concurrent: {concurrentRequests} | " +
        $"Traffic: ↑ {bytesSentRate}/s ↓ {bytesReceivedRate}/s");

    lastStatsTime = currentTime;

    // If all requests in the last second were successful, increase concurrency more aggressively
    if (!scaleDown && lastSecondStats.FailedRequests == 0 && lastSecondStats.TotalRequests > 0)
    {
        concurrentRequests += parallelIncreaseRate;
        Console.WriteLine($"Scaling up to {concurrentRequests} concurrent requests (+{parallelIncreaseRate})");
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
                    var requestStopwatch = new Stopwatch();
                    requestStopwatch.Start();

                    var response = await SharedHttpClient.GetAsync(url, cts.Token);
                    var responseContent = await response.Content.ReadAsByteArrayAsync(cts.Token);

                    requestStopwatch.Stop();

                    // Update both current second stats and cumulative stats atomically
                    Interlocked.Increment(ref currentSecondStats._totalRequests);
                    Interlocked.Increment(ref cumulativeStats._totalRequests);
                    
                    Interlocked.Add(ref currentSecondStats._bytesSent, url.Length);
                    Interlocked.Add(ref cumulativeStats._bytesSent, url.Length);
                    
                    Interlocked.Add(ref currentSecondStats._bytesReceived, responseContent.Length);
                    Interlocked.Add(ref cumulativeStats._bytesReceived, responseContent.Length);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref currentSecondStats._successfulRequests);
                        Interlocked.Increment(ref cumulativeStats._successfulRequests);
                    }
                    else
                    {
                        Interlocked.Increment(ref currentSecondStats._failedRequests);
                        Interlocked.Increment(ref cumulativeStats._failedRequests);
                        
                        // If we have failures, scale down the concurrent requests more aggressively
                        if (!scaleDown && currentSecondStats.FailedRequests > 0)
                        {
                            scaleDown = true;
                            int newConcurrentRequests = Math.Max(processorCount, concurrentRequests / 2);
                            Console.WriteLine($"Requests failing. Scaling down to {newConcurrentRequests} concurrent requests");
                            concurrentRequests = newConcurrentRequests;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref currentSecondStats._totalRequests);
                    Interlocked.Increment(ref cumulativeStats._totalRequests);
                    
                    Interlocked.Increment(ref currentSecondStats._failedRequests);
                    Interlocked.Increment(ref cumulativeStats._failedRequests);

                    // If we have failures, scale down the concurrent requests
                    if (!scaleDown && currentSecondStats.FailedRequests > 0)
                    {
                        scaleDown = true;
                        int newConcurrentRequests = Math.Max(processorCount, concurrentRequests / 2);
                        Console.WriteLine($"Requests failing. Scaling down to {newConcurrentRequests} concurrent requests");
                        concurrentRequests = newConcurrentRequests;
                    }
                }
            }));
        }

        // Reset scale down flag more frequently to allow more aggressive scaling up
        if (scaleDown && cumulativeStats.TotalRequests % 20 == 0)
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
Console.WriteLine($"Total requests: {cumulativeStats.TotalRequests}");
Console.WriteLine($"Successful requests: {cumulativeStats.SuccessfulRequests} ({cumulativeStats.SuccessRatePercentage:F1}%)");
Console.WriteLine($"Failed requests: {cumulativeStats.FailedRequests}");
Console.WriteLine($"Requests per second: {cumulativeStats.TotalRequests / stopwatch.Elapsed.TotalSeconds:F1}");

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

Console.WriteLine($"Total data sent: {FormatTotalDataSize(cumulativeStats.BytesSent)}");
Console.WriteLine($"Total data received: {FormatTotalDataSize(cumulativeStats.BytesReceived)}");
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
