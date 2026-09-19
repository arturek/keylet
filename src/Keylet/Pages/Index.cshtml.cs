using Keylet.Configuration;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Keylet.Pages;

public sealed class IndexModel(IOptions<KeyletOptions> options) : PageModel
{
    public KeyletOptions Options => options.Value;

    public void OnGet()
    {
    }
}

