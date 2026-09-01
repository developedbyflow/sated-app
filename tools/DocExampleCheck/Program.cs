using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using DocExampleCheck;

var host = args.Length > 0 ? args[0] : "https://localhost:7245";
var markdown = args.Length > 1 ? args[1] : "../../docs/reference/http-api.md";

if (!File.Exists(markdown))
{
    Console.Error.WriteLine($"Reference not found: {Path.GetFullPath(markdown)}");
    Console.Error.WriteLine("Run this from tools/DocExampleCheck, with the API already running:");
    Console.Error.WriteLine("  dotnet run --project server/Sated.Api --launch-profile https");
    return 1;
}

var examples = Reference.Examples(File.ReadAllText(markdown));

using var handler = new HttpClientHandler
{
    CookieContainer = new CookieContainer(),
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
};

using var api = new HttpClient(handler) { BaseAddress = new Uri(host) };

async Task<JsonNode?> Call(HttpMethod method, string path, object? body = null)
{
    using var request = new HttpRequestMessage(method, path);

    if (body is not null)
    {
        request.Content = new StringContent(
            body as string ?? JsonNode.Parse(
                System.Text.Json.JsonSerializer.Serialize(body))!.ToJsonString(),
            Encoding.UTF8,
            "application/json");
    }

    using var response = await api.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();

    return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
}

var failures = 0;

void Check(string name, string section, int block, JsonNode? actual, bool fragment = false)
{
    if (!examples.TryGetValue(section, out var blocks) || blocks.Count <= block)
    {
        Console.WriteLine($"  ? {name} — the reference has no example {block} under {section}");
        failures++;

        return;
    }

    var documented = blocks[block];

    if (documented is null)
    {
        Console.WriteLine($"  – {name} — the example is a fragment, not whole JSON. Skipped.");

        return;
    }

    var differences = Comparison.Differences(documented, actual, fragment);

    if (differences.Count == 0)
    {
        Console.WriteLine($"  ok {name}");

        return;
    }

    failures++;
    Console.WriteLine($"  NO {name}");

    foreach (var difference in differences.Take(8))
    {
        Console.WriteLine($"       {difference}");
    }

    if (differences.Count > 8)
    {
        Console.WriteLine($"       … and {differences.Count - 8} more");
    }
}

Console.WriteLine("Anonymous");

Check("GET /api/lenses", "`GET /api/lenses`", 0, await Call(HttpMethod.Get, "/api/lenses"));

var gradeRequest = examples["`POST /api/grades`"][0]!.ToJsonString();
Check("POST /api/grades", "`POST /api/grades`", 1,
    await Call(HttpMethod.Post, "/api/grades", gradeRequest));

Check("GET /api/foods?search=broccoli, raw", "`GET /api/foods`", 0,
    await Call(HttpMethod.Get, "/api/foods?search=broccoli,%20raw"));

var milk = await Call(HttpMethod.Get, "/api/foods/5348");
Check("GET /api/foods/5348", "`GET /api/foods/{id}`", 0, milk);
Check("Provenance on the detail", "Provenance — on the list and on the detail", 1,
    milk?["provenance"]);

var milkNfs = (await Call(HttpMethod.Get, "/api/foods?search=Milk,%20NFS"))!["items"]!
    .AsArray().Single(row => (int)row!["id"]!.AsValue() == 5347);
Check("Provenance on a list row", "Provenance — on the list and on the detail", 0, milkNfs, fragment: true);

Check("Servings on the egg 5943", "Servings, on `GET /api/foods/{id}`", 0,
    await Call(HttpMethod.Get, "/api/foods/5943"), fragment: true);

Check("GET /api/foods/5348/grade", "`GET /api/foods/{id}/grade`", 0,
    await Call(HttpMethod.Get, "/api/foods/5348/grade?lensId=weight-loss"));

Check("GET /api/foods/5348/grades", "`GET /api/foods/{id}/grades`", 0,
    await Call(HttpMethod.Get, "/api/foods/5348/grades"));

Check("GET /api/foods/categories", "`GET /api/foods/categories`", 0,
    await Call(HttpMethod.Get, "/api/foods/categories"));

Console.WriteLine();
Console.WriteLine("With a session, on an account this tool creates and deletes");

const string password = "abcdefghijkl";
var email = $"doc-example-check-{Guid.NewGuid():N}@sated.test";

var registered = await Call(HttpMethod.Post, "/api/auth/register", new { email, password });
var offered = await Call(HttpMethod.Get, "/api/consents/HealthData");

await Call(HttpMethod.Post, "/api/consents/HealthData",
    new { version = offered!["version"]!.ToString() });
await Call(HttpMethod.Put, "/api/profile",
    new { weightKg = 82, heightCm = 180, activeLensId = "weight-loss" });
await Call(HttpMethod.Put, "/api/profile/calorie-target", new { kcal = 2000 });

var meal = await Call(HttpMethod.Post, "/api/meals", new { date = "2026-08-31", name = "Breakfast" });
await Call(HttpMethod.Post, $"/api/meals/{meal!["id"]}/entries",
    new { foodId = 5943, servingCount = 2, servingDescription = "1 egg" });

Check("POST /api/auth/register", "`POST /api/auth/register`", 1, registered);
Check("GET /api/auth/me", "`GET /api/auth/me`", 0, await Call(HttpMethod.Get, "/api/auth/me"));
Check("GET /api/consents/HealthData", "`GET /api/consents/{purpose}`", 0,
    await Call(HttpMethod.Get, "/api/consents/HealthData"));
Check("GET /api/profile", "`GET /api/profile`", 0, await Call(HttpMethod.Get, "/api/profile"));
Check("POST /api/account/export", "`POST /api/account/export`", 1,
    await Call(HttpMethod.Post, "/api/account/export", new { password }));

using var goodbye = new HttpRequestMessage(HttpMethod.Delete, "/api/account")
{
    Content = new StringContent($$"""{"password":"{{password}}"}""", Encoding.UTF8, "application/json")
};

var removed = await api.SendAsync(goodbye);

Console.WriteLine();
Console.WriteLine(removed.IsSuccessStatusCode
    ? "The account this tool made is gone."
    : $"WARNING: the account {email} could not be deleted ({(int)removed.StatusCode}).");

Console.WriteLine(failures == 0
    ? "Every documented example matches what the API answers."
    : $"{failures} example(s) no longer match the API.");

return failures == 0 ? 0 : 1;
