using System.Net.Http;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => """
  <h1>Azure .NET + Dynatrace demo</h1>
  <ul>
    <li><a href="/api/external">/api/external</a> call public API</li>
    <li><a href="/api/error">/api/error</a> forced error</li>
    <li><a href="/api/ok">/api/ok</a> healthy request</li>
  </ul>
""");

app.MapGet("/api/ok", () => Results.Json(new { ok = true, t = DateTime.UtcNow }));

app.MapGet("/api/external", async () =>
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    var r = await http.GetAsync("https://jsonplaceholder.typicode.com/todos/1");
    return Results.Json(new { upstream = (int)r.StatusCode });
});

app.MapGet("/api/error", () =>
{
    throw new Exception("boom");
});

app.Run();
