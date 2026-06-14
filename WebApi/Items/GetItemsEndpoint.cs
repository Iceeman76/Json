using FastEndpoints;

namespace WebApi.Items;

public sealed class GetItemsEndpoint : Endpoint<GetItemsRequest, GetItemsResponse>
{
    public override void Configure()
    {
        Get("/items");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetItemsRequest req, CancellationToken ct)
    {
        var limit = req.Limit ?? 1000;
        var items = ItemCatalog.Generate().Take(limit).ToArray();
        var response = new GetItemsResponse(items.Length, items);

        await SendOkAsync(response, cancellation: ct);
    }
}
