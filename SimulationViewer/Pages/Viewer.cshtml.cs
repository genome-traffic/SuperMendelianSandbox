using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace SimulationViewer.Pages;

/// <summary>
/// Results viewer. Shared by all drive models: the "model" query parameter
/// selects which output subdirectory to read and how the page labels itself.
/// </summary>
public class ViewerModel : PageModel
{
    public string CsvJson { get; set; } = "[]";
    public string CsvPath { get; set; } = "";
    public string? ErrorMessage { get; set; }

    /// <summary>The drive model whose results are shown; always one of the known ids.</summary>
    public string DriveModel { get; private set; } = "ffer";

    /// <summary>Display name for the model, shown in the page heading.</summary>
    public string DriveModelName => DriveModel switch
    {
        "ydrive" => "Driving Y chromosome (X-shredder sex distorter)",
        "medea"  => "MEDEA selfish genetic element",
        _        => "Suppressive gene drive targeting a mosquito female fertility gene",
    };

    /// <summary>Path back to this model's configuration page.</summary>
    public string ConfigPage => DriveModel switch
    {
        "ydrive" => "/YDrive",
        "medea"  => "/Medea",
        _        => "/Ffer",
    };

    static readonly string[] KnownModels = { "ffer", "ydrive", "medea" };

    public void OnGet(string? path, string? model)
    {
        // The model id becomes a path segment, so only accept known values.
        if (model != null && Array.IndexOf(KnownModels, model) >= 0)
            DriveModel = model;

        CsvPath = path ?? Path.Combine(
            Directory.GetCurrentDirectory(), "output", DriveModel, "modeloutput.csv");

        if (!System.IO.File.Exists(CsvPath))
        {
            ErrorMessage = $"No results found for this model yet. Run a simulation first. (Looked for: {CsvPath})";
            return;
        }

        try
        {
            var records = new List<Dictionary<string, object>>();
            var lines = System.IO.File.ReadAllLines(CsvPath);

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 9) continue;

                records.Add(new Dictionary<string, object>
                {
                    ["iteration"] = int.Parse(parts[0]),
                    ["environ"] = parts[1],
                    ["population"] = int.Parse(parts[2]),
                    ["generation"] = int.Parse(parts[3]),
                    ["category"] = parts[4],
                    ["value1"] = parts[5],
                    ["value2"] = parts[6],
                    ["count"] = int.Parse(parts[7]),
                    ["type"] = parts[8]
                });
            }

            CsvJson = JsonSerializer.Serialize(records);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error parsing CSV: {ex.Message}";
        }
    }
}
