using Keylet.Events;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Keylet.Pages;

public sealed class EventsModel(IKeyletEventStore events) : PageModel
{
    public IReadOnlyList<KeyletEvent> Events { get; private set; } = [];

    public void OnGet() => Events = events.GetAll();
}

