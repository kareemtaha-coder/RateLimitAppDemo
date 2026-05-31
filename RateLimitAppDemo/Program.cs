using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Threading.RateLimiting;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.AddPolicy("FixedPolicy", context =>
    {
        var clientIp = context.Request.Headers["X-Test-IP"].FirstOrDefault()
                       ?? context.Connection.RemoteIpAddress?.ToString()
                       ?? "unknown_ip";
        Console.WriteLine(clientIp);

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey: clientIp, factory: _ =>
        new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromSeconds(10),
          
        });
    });

    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiterOptions.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }
        context.HttpContext.Response.ContentType = "application/json";
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var problemDetails = new
        {
            Title = "Too Many Requests",
            Status = 429,
            Detail = "You have exceeded the rate limit.",
            RetryAfterSeconds = retryAfter.TotalSeconds,


        };
            await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: token);
    };
});
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseRateLimiter();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers().RequireRateLimiting("FixedPolicy");

app.Run();
