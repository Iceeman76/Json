namespace WebApi.Items;

public static class ItemCatalog
{
    private static readonly string[] Names =
    [
        "Keyboard", "Mouse", "Monitor", "Headset", "Webcam",
        "Microphone", "Speaker", "Laptop", "Tablet", "Docking Station",
        "USB Cable", "HDMI Cable", "Router", "Switch", "External SSD",
        "Power Bank", "Stylus", "Graphics Card", "CPU Cooler", "Desk Lamp"
    ];

    private static readonly string[] Descriptions =
    [
        "Premium quality with a sleek design",
        "Reliable everyday performer",
        "Compact and portable",
        "Built for professionals",
        "Great value for money"
    ];

    public static IEnumerable<Item> Generate()
    {
        var now = DateTime.UtcNow;
        var index = 0;

        while (index < int.MaxValue)
        {
            var id = index + 1;
            var name = $"{Names[index % Names.Length]} #{id}";
            var description = index % 3 == 0 ? null : Descriptions[index % Descriptions.Length];
            decimal? price = index % 4 == 0 ? null : decimal.Round(9.99m + index * 3.5m, 2);
            int? quantity = index % 5 == 0 ? null : index * 7 % 100;
            bool? inStock = quantity is null ? null : quantity > 0;
            DateTime? createdAt = index % 2 == 0 ? null : now.AddDays(-(index % 365));

            index++;

            yield return new Item(id, name, description, price, quantity, inStock, createdAt);
        }
    }
}
