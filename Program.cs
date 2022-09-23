using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRequestDecompression();
builder.Services.AddOutputCache();

var app = builder.Build();
app.UseOutputCache();
const string coolEndpointName = "coolPolicy";
app.UseRateLimiter(new RateLimiterOptions
{
    GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        return RateLimitPartition.GetConcurrencyLimiter<string>("globalLimiter", key => new ConcurrencyLimiterOptions
        {
            PermitLimit = 2,
            QueueLimit = 5,
            QueueProcessingOrder = QueueProcessingOrder.NewestFirst,
        });
    })
}.AddTokenBucketLimiter(coolEndpointName, o =>
{
    o.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
    o.TokenLimit = 2;
    o.QueueLimit = 5;
    o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    o.TokensPerPeriod = 3;
}));
app.UseRequestDecompression();
app.MapGet("/", () => "Hello World!");

app.MapGet("/notcached", () => DateTime.Now.ToString()).DisableRateLimiting();
app.MapGet("/cached", () => DateTime.Now.ToString()).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5))).DisableRateLimiting();
app.MapGet("/cachedvary", () => DateTime.Now.ToString()).CacheOutput(p => p.VaryByQuery("lang")).DisableRateLimiting();

app.MapGet("/cool", () => "cool").RequireRateLimiting(coolEndpointName); // to test in posh: while ($true) { curl localhost:5000/cool; " "; date }
app.MapGet("/hot", () => "hot");

app.Run();
