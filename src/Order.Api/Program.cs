using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Dang ky OrderStore Singleton trong Dependency Injection
builder.Services.AddSingleton<OrderStore>();

var app = builder.Build();

// Endpoint kiem tra trang thai service
app.MapGet("/", () => Results.Ok(new { service = "Order.Api", status = "running" }));

// GET /orders: ho tro query string tuy chon (status, customerName) de loc danh sach don hang
app.MapGet("/orders", (OrderStore store, string? status, string? customerName) =>
{
    var orders = store.GetOrders(status, customerName);
    return Results.Ok(orders);
});

// GET /orders/{id:guid}: tim don hang theo id voi rang buoc kieu Guid tren route
app.MapGet("/orders/{id:guid}", (OrderStore store, Guid id) =>
{
    if (!store.TryGetOrder(id, out var order) || order is null)
    {
        return Results.NotFound(new ApiError("ORDER_NOT_FOUND", "Khong tim thay don hang."));
    }

    return Results.Ok(order);
});

// POST /orders: tiep nhan va kiem tra tinh hop le cua don hang moi
app.MapPost("/orders", (OrderStore store, CreateOrderRequest? request) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.CustomerName))
    {
        return Results.BadRequest(new ApiError("INVALID_CUSTOMER", "Ten khach hang khong duoc de trong."));
    }

    if (request.Items is null || request.Items.Count == 0)
    {
        return Results.BadRequest(new ApiError("EMPTY_ORDER_ITEMS", "Don hang phai co it nhat mot san pham."));
    }

    if (request.Items.Any(item => item is null ||
                                 string.IsNullOrWhiteSpace(item.ProductName) ||
                                 item.Quantity <= 0 ||
                                 item.UnitPrice <= 0))
    {
        return Results.BadRequest(new ApiError("INVALID_ORDER_ITEM", "Thong tin san pham va so luong khong hop le."));
    }

    var orderItems = request.Items
        .Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
        .ToList();

    var totalAmount = orderItems.Sum(i => i.Quantity * i.UnitPrice);

    var order = new OrderDto(
        Id: Guid.NewGuid(),
        CustomerName: request.CustomerName.Trim(),
        Items: orderItems,
        TotalAmount: totalAmount,
        Status: "Pending",
        CreatedAt: DateTimeOffset.UtcNow
    );

    store.AddOrder(order);

    return Results.Created($"/orders/{order.Id}", order);
});

app.Run();

// Lop quan ly luu tru don hang trong bo nho an toan luong (Thread-safe)
public class OrderStore
{
    private readonly ConcurrentDictionary<Guid, OrderDto> _orders = new();

    public OrderStore()
    {
        var order1 = new OrderDto(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CustomerName: "Nguyen Van A",
            Items: new List<OrderItemDto>
            {
                new("P001", "Ban phim co", 1, 1200000m),
                new("P002", "Chuot khong day", 2, 450000m)
            },
            TotalAmount: 2100000m,
            Status: "Pending",
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10)
        );

        var order2 = new OrderDto(
            Id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CustomerName: "Tran Thi B",
            Items: new List<OrderItemDto>
            {
                new("P003", "Tai nghe Gaming", 1, 890000m)
            },
            TotalAmount: 890000m,
            Status: "Pending",
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-5)
        );

        _orders.TryAdd(order1.Id, order1);
        _orders.TryAdd(order2.Id, order2);
    }

    public IEnumerable<OrderDto> GetOrders(string? status = null, string? customerName = null)
    {
        var query = _orders.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            query = query.Where(o => o.CustomerName.Contains(customerName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(o => o.CreatedAt);
    }

    public bool TryGetOrder(Guid id, out OrderDto? order)
    {
        return _orders.TryGetValue(id, out order);
    }

    public void AddOrder(OrderDto order)
    {
        _orders.TryAdd(order.Id, order);
    }
}

// Khai bao cac record DTO theo chuan RESTful
public record OrderItemDto(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderItemRequest(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderRequest(string CustomerName, List<CreateOrderItemRequest> Items);
public record OrderDto(Guid Id, string CustomerName, List<OrderItemDto> Items, decimal TotalAmount, string Status, DateTimeOffset CreatedAt);
public record ApiError(string Code, string Message);
