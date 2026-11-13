var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var orders = new List<OrderResult>
{
    new(
        Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        CustomerName: "Nguyen Van A",
        Items: new List<OrderItem>
        {
            new("P001", "Ban phim co", 1, 1200000m),
            new("P002", "Chuot khong day", 2, 450000m)
        },
        Status: "Pending",
        CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10)
    ),
    new(
        Id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
        CustomerName: "Tran Thi B",
        Items: new List<OrderItem>
        {
            new("P003", "Tai nghe Gaming", 1, 890000m)
        },
        Status: "Pending",
        CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-5)
    )
};

app.MapGet("/", () => Results.Ok(new { service = "Order.Api", status = "running" }));

app.MapGet("/orders", () => Results.Ok(orders));

app.MapPost("/orders", (CreateOrderRequest? request) =>
{
    if (request is null ||
        string.IsNullOrWhiteSpace(request.CustomerName) ||
        request.Items is null ||
        request.Items.Count == 0 ||
        request.Items.Any(item => item.Quantity <= 0))
    {
        return Results.BadRequest(new { message = "Du lieu don hang khong hop le" });
    }

    var order = new OrderResult(
        Id: Guid.NewGuid(),
        CustomerName: request.CustomerName,
        Items: request.Items,
        Status: "Pending",
        CreatedAt: DateTimeOffset.UtcNow
    );

    orders.Add(order);

    return Results.Created($"/orders/{order.Id}", order);
});

app.Run();

public record OrderItem(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderRequest(string CustomerName, List<OrderItem> Items);
public record OrderResult(Guid Id, string CustomerName, List<OrderItem> Items, string Status, DateTimeOffset CreatedAt);

