using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SimulationViewer.Pages;

/// <summary>
/// Landing page. Lists the drive models the sandbox can simulate and links each
/// to its own configuration page.
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
