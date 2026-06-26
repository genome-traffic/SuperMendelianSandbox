using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
var app = builder.Build();
app.UseStaticFiles();
app.MapRazorPages();

var modelDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "model");

var simProjectPath = Path.GetFullPath(
    Path.Combine(Directory.GetCurrentDirectory(), "..", "SuperMendelianSandbox", "SuperMendelianSandbox"));

app.MapPost("/api/simulate", async (HttpContext ctx) =>
{
    Directory.CreateDirectory(modelDir);

    var configPath = Path.Combine(modelDir, "simconfig.json");
    var statusPath = Path.Combine(modelDir, "simstatus.json");

    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    await File.WriteAllTextAsync(configPath, body);
    await File.WriteAllTextAsync(statusPath,
        JsonSerializer.Serialize(new { status = "starting", iteration = 0,
            totalIterations = 3, generation = 0, totalGenerations = 30 }));

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

app.MapGet("/api/status", async () =>
{
    var statusPath = Path.Combine(modelDir, "simstatus.json");
    if (!File.Exists(statusPath))
        return Results.Json(new { status = "idle" });

    var json = await File.ReadAllTextAsync(statusPath);
    return Results.Content(json, "application/json");
});

app.Run();
