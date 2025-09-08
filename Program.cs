using System.Net.Http;
using Microsoft.Data.Sqlite;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
// HTTP client reused for all outbound calls
var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

// SQLite file under the writable home folder
var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "demo.sqlite");

// Ensure DB exists and has a table
void InitDb()
{
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    using var con = new SqliteConnection($"Data Source={dbPath}");
    con.Open();
    using var cmd = con.CreateCommand();
    cmd.CommandText = "CREATE TABLE IF NOT EXISTS hits (id INTEGER PRIMARY KEY, route TEXT, ts TEXT);";
    cmd.ExecuteNonQuery();
}
InitDb();

// Optional Redis connection. Set REDIS_CONNECTION in Azure App Settings to enable.
Lazy<ConnectionMultiplexer?> lazyRedis = new(() =>
{
    var cs = Environment.GetEnvironmentVariable("REDIS_CONNECTION");
    return string.IsNullOrWhiteSpace(cs) ? null : ConnectionMultiplexer.Connect(cs);
});


string rum = """"<script type="text/javascript" src="https://js-cdn.dynatrace.com/jstag/1944242a637/bf28228awz/a7cb1a5e608f619f_complete.js" crossorigin="anonymous"></script>"""";

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";

    var htmlTop =
@"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<title>Azure .NET + Dynatrace demo</title>
";

    var htmlMid =
@"</head>
<body>
  <h1>Azure .NET + Dynatrace demo</h1>
  <ul>
    <li><a href=""/api/external"">/api/external</a> call public API</li>
    <li><a href=""/api/error"">/api/error</a> forced error</li>
    <li><a href=""/api/ok"">/api/ok</a> healthy request</li>
  </ul>

  <script>
    function randomMs(min, max) {
      return Math.floor(Math.random() * (max - min + 1)) + min;
    }
    function schedule(fn, min, max) {
      fn();
      setInterval(fn, randomMs(min, max));
    }
    // hit homepage periodically (page stays loaded; this is a background fetch)
    schedule(() => fetch('/').catch(()=>{}), 5000, 10000);
    // steady backend traffic
    schedule(() => fetch('/api/ok').catch(()=>{}), 1000, 3000);
    schedule(() => fetch('/api/external').catch(()=>{}), 2000, 4000);
    // occasional errors
    schedule(() => fetch('/api/error').catch(()=>{}), 15000, 30000);
  </script>
</body>
</html>";

    var full = htmlTop + rum + htmlMid;
    await context.Response.WriteAsync(full);
});

app.MapGet("/api/ok", () =>
{
    return Results.Json(new { ok = true, t = DateTime.UtcNow });
});

app.MapGet("/api/external", async () =>
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    var r = await http.GetAsync("https://jsonplaceholder.typicode.com/todos/1");
    return Results.Json(new { upstream = (int)r.StatusCode });
});

// Important: provide a compatible delegate signature
app.MapGet("/api/error", (HttpContext _) =>
{
    throw new Exception("boom");
});

// Shows an extra HTTP dependency (httpbin)
app.MapGet("/api/secondapi", async () =>
{
    var r = await http.GetAsync("https://httpbin.org/uuid");
    var body = await r.Content.ReadAsStringAsync();
    return Results.Json(new { upstream = (int)r.StatusCode, len = body.Length });
});


// Inserts and reads a row so Dynatrace sees DB I/O
app.MapGet("/api/db", () =>
{
    using var con = new SqliteConnection($"Data Source={dbPath}");
    con.Open();
    using (var insert = con.CreateCommand())
    {
        insert.CommandText = "INSERT INTO hits(route, ts) VALUES ($route, $ts);";
        insert.Parameters.AddWithValue("$route", "/api/db");
        insert.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }
    using var select = con.CreateCommand();
    select.CommandText = "SELECT COUNT(1) FROM hits;";
    var count = (long)(select.ExecuteScalar() ?? 0);
    return Results.Json(new { ok = true, total = count });
});


// Requires REDIS_CONNECTION in App Settings (Azure Cache for Redis works)
app.MapGet("/api/redis", async () =>
{
    var mux = lazyRedis.Value;
    if (mux == null) return Results.Json(new { ok = false, message = "redis not configured" });

    var db = mux.GetDatabase();
    var key = "demo:counter";
    var val = await db.StringIncrementAsync(key);
    return Results.Json(new { ok = true, counter = (long)val });
});

// Adds a slow request to create latency outliers
app.MapGet("/api/slow", async () =>
{
    await Task.Delay(3000);
    return Results.Json(new { ok = true, delayedMs = 3000 });
});

// Calls a bad domain to generate connection failures
app.MapGet("/api/badupstream", async () =>
{
    try
    {
        var r = await http.GetAsync("https://does-not-exist-xyz.demo.example.com/");
        return Results.Json(new { upstream = (int)r.StatusCode });
    }
    catch (Exception e)
    {
        // surfaces as failed external call in Dynatrace
        return Results.Problem($"upstream failed: {e.GetType().Name}");
    }
});


app.Run();
