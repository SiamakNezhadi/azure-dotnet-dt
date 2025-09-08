using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

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

  <section style=""margin:1rem 0;padding:1rem;border:1px solid #ddd"">
    <h2>frontend controls</h2>
    <button id=""burst"">burst 50 calls</button>
    <button id=""slow"">trigger slow</button>
    <button id=""fail"">trigger failures</button>
    <button id=""jsError"">throw JS error</button>
    <label style=""margin-left:1rem;""><input type=""checkbox"" id=""auto"" checked> auto traffic</label>
  </section>

  <section>
    <ul>
      <li><a href=""/api/ok"">/api/ok</a></li>
      <li><a href=""/api/external"">/api/external</a></li>
      <li><a href=""/api/secondapi"">/api/secondapi</a></li>
      <li><a href=""/api/db"">/api/db</a></li>
      <li><a href=""/api/slow"">/api/slow</a></li>
      <li><a href=""/api/badupstream"">/api/badupstream</a></li>
    </ul>
  </section>

  <img id=""brokenImg"" src=""/does-not-exist.png"" alt=""broken"" style=""display:none"">

  <script>
    const fetchJson = p => fetch(p, {cache:'no-store'}).then(r => r.json()).catch(_=>{});
    const hit = async () => {
      await fetchJson('/api/ok');
      await fetchJson('/api/external');
      await fetchJson('/api/secondapi');
      await fetchJson('/api/db');
      // fire one that fails
      fetch('/no-such-route', {cache:'no-store'}).catch(()=>{});
      // load a broken resource
      document.getElementById('brokenImg').src = '/missing-' + Date.now() + '.png';
    };

    // auto traffic every 3 seconds
    let timer = setInterval(hit, 3000);
    document.getElementById('auto').addEventListener('change', e => {
      if (e.target.checked) timer = setInterval(hit, 3000);
      else { clearInterval(timer); timer = null; }
    });

    // buttons
    document.getElementById('burst').addEventListener('click', async () => {
      for (let i=0;i<50;i++) hit();
    });

    document.getElementById('slow').addEventListener('click', async () => {
      await fetchJson('/api/slow');
    });

    document.getElementById('fail').addEventListener('click', async () => {
      await fetchJson('/api/badupstream');
      // also call a route that throws on server
      fetch('/api/error').catch(()=>{});
    });

    document.getElementById('jsError').addEventListener('click', () => {
      // intentional JS error for RUM
      throw new Error('frontend exploded on purpose');
    });

    // warm up immediately
    hit();
  </script>
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

app.Run();
