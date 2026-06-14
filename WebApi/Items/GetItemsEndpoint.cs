using FastEndpoints;

namespace WebApi.Items;

public sealed class GetItemsEndpoint : Endpoint<GetItemsRequest, IReadOnlyList<Item>>
{
    public override void Configure()
    {
        Get("/items");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetItemsRequest req, CancellationToken ct)
    {
        var items = ItemCatalog.All;

        if (req.Limit is { } limit && limit < items.Count)
        {
            items = items.Take(Math.Max(limit, 0)).ToList();
        }

        Logger.LogInformation("Returning {ItemCount} of {TotalCount} items", items.Count, ItemCatalog.All.Count);

        await SendAsync(items, cancellation: ct);
    }
}
