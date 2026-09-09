using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
var app = builder.Build();
app.UseStaticFiles();
app.MapRazorPages();

// Each drive model writes into its own subdirectory of output/, so results from
// one model are never overwritten by a run of another and the viewer can be
// pointed at whichever set the user wants to look at.
var outputRoot = Path.GetFullPath(
    Path.Combine(Directory.GetCurrentDirectory(), "output"));

var simProjectPath = Path.GetFullPath(
    Path.Combine(Directory.GetCurrentDirectory(), "..", "SuperMendelianSandbox", "SuperMendelianSandbox"));

app.MapPost("/api/simulate", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();

    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body)
        ?? new Dictionary<string, JsonElement>();

    var model = config.TryGetValue("model", out var m) ? m.GetString() : null;
    if (!DriveModels.IsKnown(model))
        return Results.Json(new { ok = false, error = "Unknown drive model: " + model });

    var outputDir = Path.Combine(outputRoot, model!);
    Directory.CreateDirectory(outputDir);

    var configPath = Path.Combine(outputDir, "simconfig.json");
    var statusPath = Path.Combine(outputDir, "simstatus.json");

    // Inject the output directory into the config so the simulation writes here
    var configWithOutput = new Dictionary<string, object>();
    foreach (var kv in config)
        configWithOutput[kv.Key] = kv.Value;
    configWithOutput["outputDir"] = outputDir;

    var configJson = JsonSerializer.Serialize(configWithOutput);
    await File.WriteAllTextAsync(configPath, configJson);

    var totalGenerations = config.TryGetValue("generations", out var g) ? g.GetInt32() : 30;
    await File.WriteAllTextAsync(statusPath,
        JsonSerializer.Serialize(new { status = "starting", iteration = 0,
            totalIterations = 3, generation = 0, totalGenerations }));

    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"run --project \"{simProjectPath}\" -- \"{configPath}\"",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    try
    {
        var process = Process.Start(psi);
        _ = Task.Run(async () =>
        {
            if (process != null)
            {
                await process.WaitForExitAsync();
                Console.WriteLine("Simulation process exited with code: " + process.ExitCode);
            }
        });

        return Results.Json(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, error = ex.Message });
    }
});

app.MapGet("/api/status", async (string? model) =>
{
    if (!DriveModels.IsKnown(model))
        return Results.Json(new { status = "idle" });

    var statusPath = Path.Combine(outputRoot, model!, "simstatus.json");
    if (!File.Exists(statusPath))
        return Results.Json(new { status = "idle" });

    var json = await File.ReadAllTextAsync(statusPath);
    return Results.Content(json, "application/json");
});

app.Run();

/// <summary>
/// The drive models the sandbox knows about. Model names arrive from the browser
/// and are used as a path segment, so they are matched against this fixed list
/// rather than trusted.
/// </summary>
static class DriveModels
{
    public static readonly string[] All = { "ffer", "ydrive", "medea" };

    public static bool IsKnown(string? model) =>
        model != null && Array.IndexOf(All, model) >= 0;
}
