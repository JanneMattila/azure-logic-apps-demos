using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add services for serving static files and pages
builder.Services.AddRazorPages();

var app = builder.Build();

// Enable static files
app.UseStaticFiles();
app.UseDefaultFiles();
app.UseRouting();

// In-memory storage for tracking received counters and errors
var receivedCounters = new HashSet<int>();
var errors = new List<ErrorData>();

// POST endpoint for data
app.MapPost("/api/data", async (Data data, IHubContext<ErrorHub> hubContext) =>
{
    Console.WriteLine($"Received: Counter={data.Counter}, RunID={data.RunID}");
    
    var hasError = false;
    var errorMessage = string.Empty;
    
    // Check if counter already exists
    if (receivedCounters.Contains(data.Counter))
    {
        hasError = true;
        errorMessage = $"Duplicate counter value: {data.Counter} for RunID: {data.RunID}";
    }
    else
    {
        // Check for skipped values
        if (receivedCounters.Count > 0 && data.Counter > 1)
        {
            var expectedCounter = receivedCounters.Max() + 1;
            if (data.Counter > expectedCounter)
            {
                hasError = true;
                errorMessage = $"Skipped counter values between {expectedCounter-1} and {data.Counter} for RunID: {data.RunID}";
            }
        }
        
        // Add to received counters
        receivedCounters.Add(data.Counter);
    }
    
    // If there was an error, add it to the list and notify clients
    if (hasError)
    {
        var errorData = new ErrorData(data.Counter, data.RunID, errorMessage, DateTime.UtcNow);
        errors.Add(errorData);
        
        // Notify all clients of the new error
        await hubContext.Clients.All.SendAsync("ReceiveError", errorData);
    }
    
    return Results.Ok();
});

// GET endpoint for errors
app.MapGet("/api/errors", () =>
{
    return Results.Ok(errors);
});

app.MapStaticAssets();

// Map SignalR hub
app.MapHub<ErrorHub>("/errorHub");

app.Run();

// Data model for incoming requests
internal record Data(int Counter, string RunID);

// Error data model
internal record ErrorData(int Counter, string RunID, string ErrorMessage, DateTime Timestamp);

// SignalR Hub for real-time updates
internal class ErrorHub : Hub
{
}
