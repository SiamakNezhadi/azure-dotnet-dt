using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string rum = """"<script type="text/javascript" src="https://js-cdn.dynatrace.com/jstag/1944242a637/bf28228awz/a7cb1a5e608f619f_complete.js" crossorigin="anonymous"></script>"""";


// Home page with HTML links
app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync("""
        <h1>Azure .NET + Dynatrace demo</h1>
        <ul>
          <li><a href="/api/external">/api/external</a> call public API</li>
          <li><a href="/api/error">/api/error</a> forced error</li>
          <li><a href="/api/ok">/api/ok</a> healthy request</li>
        </ul>
    """);
});

// Healthy route
app.MapGet("/api/ok", () =>
{
    return Results.Json(new { ok = true, t = DateTime.UtcNow });
});

// External API call to show dependency
app.MapGet("/api/external", async () =>
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    var r = await http.GetAsync("https://jsonplaceholder.typicode.com/todos/1");
    return Results.Json(new { upstream = (int)r.StatusCode });
});

// Forced error to create failures and exceptions
app.MapGet("/api/error", () =>
{
    throw new Exception("boom");
});

app.Run();
