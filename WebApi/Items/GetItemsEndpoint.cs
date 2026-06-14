using System.Text.Json;
using FastEndpoints;
using Json;

namespace WebApi.Items;

public sealed class GetItemsEndpoint : EndpointWithoutRequest<IReadOnlyList<Item>>
{
    private const string SelectorHeaderName = "X-Selector";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public override void Configure()
    {
        Get("/items");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var items = new List<Item>
        {
            new(1, "Keyboard", "Mechanical keyboard with RGB lighting", 89.99m, 25, true, DateTime.UtcNow.AddDays(-10)),
            new(2, "Mouse", "Wireless ergonomic mouse", 49.50m, 60, true, DateTime.UtcNow.AddDays(-5)),
            new(3, "Monitor", "27 inch 4K display", 329.00m, 0, false, DateTime.UtcNow.AddDays(-30)),
            new(4, "Headset", "Noise cancelling headset", 129.95m, 12, true, DateTime.UtcNow.AddDays(-2))
        };

        var selector = HttpContext.Request.Headers[SelectorHeaderName].ToString();

        if (string.IsNullOrWhiteSpace(selector))
        {
            await SendAsync(items, cancellation: ct);
            return;
        }

        var json = JsonSerializer.Serialize(items, SerializerOptions);
        var projected = new JsonFieldSelector(json).Select(selector);

        await SendStringAsync(projected, contentType: "application/json", cancellation: ct);
    }
}
