using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string rum = """"<script type="text/javascript" src="https://js-cdn.dynatrace.com/jstag/1944242a637/bf28228awz/a7cb1a5e608f619f_complete.js" crossorigin="anonymous"></script>"""";

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";

    var html = $@"
<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<title>Azure .NET + Dynatrace demo</title>
{rum}
</head>
<body>
  <h1>Azure .NET + Dynatrace demo</h1>
  <ul>
    <li><a href=""/api/external"">/api/external</a> call public API</li>
    <li><a href=""/api/error"">/api/error</a> forced error</li>
    <li><a href=""/api/ok"">/api/ok</a> healthy request</li>
  </ul>

  <script>
    function randomMs(min, max) {{
      return Math.floor(Math.random() * (max - min + 1)) + min;
    }}

    function schedule(fn, min, max) {{
      fn();
      setInterval(fn, randomMs(min, max));
    }}

    // hit homepage (/) periodically to trigger page loads
    schedule(() => fetch('/').catch(()=>{{}}), 5000, 10000);

    // hit ok endpoint
    schedule(() => fetch('/api/ok').catch(()=>{{}}), 1000, 3000);

    // hit external dependency
    schedule(() => fetch('/api/external').catch(()=>{{}}), 2000, 4000);

    // send an error now and then
    schedule(() => fetch('/api/error').catch(()=>{{}}), 15000, 30000);
  </script>
</body>
</html>";

    await context.Response.WriteAsync(html);
});

app.MapGet("/api/ok", () => Results.Json(new { ok = true, t = DateTime.UtcNow }));

app.MapGet("/api/external", async () =>
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    var r = await http.GetAsync("https://jsonplaceholder.typicode.com/todos/1");
    return Results.Json(new { upstream = (int)r.StatusCode });
});

app.MapGet("/api/error", () => throw new Exception("boom"));

app.Run();
