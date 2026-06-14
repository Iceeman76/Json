namespace WebApi.Items;

public sealed record Item(
    int Id,
    string Name,
    string Description,
    decimal Price,
    int Quantity,
    bool InStock,
    DateTime CreatedAt);
