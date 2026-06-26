using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace SimulationViewer.Pages;

public class IndexModel : PageModel
{
    public string CsvJson { get; set; } = "[]";
    public string CsvPath { get; set; } = "";
    public string? ErrorMessage { get; set; }

    public void OnGet(string? path)
    {
        CsvPath = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "model", "modeloutput.csv");

        if (!System.IO.File.Exists(CsvPath))
        {
            ErrorMessage = $"File not found: {CsvPath}";
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
