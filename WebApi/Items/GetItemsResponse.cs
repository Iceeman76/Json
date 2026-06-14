namespace WebApi.Items;

public sealed record GetItemsResponse(int ItemCount, Item[] Items);
