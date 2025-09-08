using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string rum = """"<script type="text/javascript" src="https://js-cdn.dynatrace.com/jstag/1944242a637/bf28228awz/a7cb1a5e608f619f_complete.js" crossorigin="anonymous"></script>"""";

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";

    // Build HTML without fancy raw strings to avoid compiler quirks on CI
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
    // trigger XHRs so RUM sees frontend calls
    fetch('/api/ok').catch(()=>{});
    fetch('/api/external').catch(()=>{});
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

app.MapGet("/api/error", () =>
{
    throw new Exception("boom");
});

app.Run();
