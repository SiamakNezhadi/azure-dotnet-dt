using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string rum = """
<script type="text/javascript" src="https://js-cdn.dynatrace.com/jstag/1944242a637/bf28228awz/a7cb1a5e608f619f_complete.js" crossorigin="anonymous"></script>
""";

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync($$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Azure .NET + Dynatrace demo</title>
{{rum}}
</head>
<body>
  <h1>Azure .NET + Dynatrace demo</h1>
  <ul>
    <li><a href="/api/external">/api/external</a> call public API</li>
    <li><a href="/api/error">/api/error</a> forced error</li>
    <li><a href="/api/ok">/api/ok</a> healthy request</li>
  </ul>

  <script>
    // trigger a couple of XHRs so RUM sees frontend calls
    fetch('/api/ok').catch(()=>{});
    fetch('/api/external').catch(()=>{});
  </script>
</body>
</html>
""");
});

app.MapGet("/api/ok", () =>
{
    return Results.Json(new { ok = true, t = DateTime.UtcNow }



