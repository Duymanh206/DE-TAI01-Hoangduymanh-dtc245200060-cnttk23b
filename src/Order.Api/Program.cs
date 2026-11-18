using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Dang ky OrderStore Singleton trong Dependency Injection
builder.Services.AddSingleton<OrderStore>();

// Dang ky SignalR service
builder.Services.AddSignalR();

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

// POST /orders: tiep nhan don hang moi, khoi chay mo phong xu ly va day su kien SignalR
app.MapPost("/orders", (OrderStore store, IHubContext<OrderHub> hubContext, CreateOrderRequest? request) =>
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
    var now = DateTimeOffset.UtcNow;

    var order = new OrderDto(
        Id: Guid.NewGuid(),
        CustomerName: request.CustomerName.Trim(),
        Items: orderItems,
        TotalAmount: totalAmount,
        Status: "Pending",
        CreatedAt: now,
        UpdatedAt: now
    );

    store.AddOrder(order);

    // Khoi chay tac vu nen mo phong xu ly don hang bat dong bo
    _ = Task.Run(() => SimulateOrderProcessingAsync(order.Id, store, hubContext));

    return Results.Created($"/orders/{order.Id}", order);
});

// Anh xa SignalR Hub
app.MapHub<OrderHub>("/hubs/orders");

app.Run();

// Ham mo phong quy trinh xu ly don hang theo yeu cau thoi gian thuc cua De tai 01
static async Task SimulateOrderProcessingAsync(Guid orderId, OrderStore store, IHubContext<OrderHub> hubContext)
{
    try
    {
        // 1. Sau 1.5 giay: Chuyen trang thai tu "Pending" sang "Processing"
        await Task.Delay(1500);

        var processingTime = DateTimeOffset.UtcNow;
        if (store.TryUpdateStatus(orderId, "Processing", processingTime, out var processingOrder, out var oldStatus) && processingOrder is not null)
        {
            var msgProcessing = new OrderStatusChangedMessage(
                OrderId: orderId,
                CustomerName: processingOrder.CustomerName,
                OldStatus: oldStatus,
                NewStatus: "Processing",
                UpdatedAt: processingTime,
                Reason: null
            );

            await hubContext.Clients.Group($"order:{orderId}").SendAsync("OrderStatusChanged", msgProcessing);
        }

        // 2. Sau 2 giay tiep theo: Kiem tra neu co san pham mang ProductId == "OUT_OF_STOCK"
        await Task.Delay(2000);

        var finalTime = DateTimeOffset.UtcNow;
        if (store.TryGetOrder(orderId, out var currentOrder) && currentOrder is not null)
        {
            bool isOutOfStock = currentOrder.Items.Any(i => string.Equals(i.ProductId, "OUT_OF_STOCK", StringComparison.OrdinalIgnoreCase));
            string targetStatus = isOutOfStock ? "Failed" : "Completed";
            string? reason = isOutOfStock ? "San pham OUT_OF_STOCK da het hang trong kho." : null;

            if (store.TryUpdateStatus(orderId, targetStatus, finalTime, out var finalOrder, out var prevStatus) && finalOrder is not null)
            {
                var msgFinal = new OrderStatusChangedMessage(
                    OrderId: orderId,
                    CustomerName: finalOrder.CustomerName,
                    OldStatus: prevStatus,
                    NewStatus: targetStatus,
                    UpdatedAt: finalTime,
                    Reason: reason
                );

                await hubContext.Clients.Group($"order:{orderId}").SendAsync("OrderStatusChanged", msgFinal);
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Loi SimulateOrderProcessingAsync]: {ex.Message}");
    }
}

// Hub SignalR quan ly dang ky/huy dang ky theo doi don hang
public class OrderHub : Hub
{
    public async Task WatchOrder(string orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order:{orderId}");
    }

    public async Task UnwatchOrder(string orderId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order:{orderId}");
    }
}

// Lop quan ly luu tru don hang trong bo nho an toan luong (Thread-safe)
public class OrderStore
{
    private readonly ConcurrentDictionary<Guid, OrderDto> _orders = new();

    public OrderStore()
    {
        var createdAt1 = DateTimeOffset.UtcNow.AddMinutes(-10);
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
            CreatedAt: createdAt1,
            UpdatedAt: createdAt1
        );

        var createdAt2 = DateTimeOffset.UtcNow.AddMinutes(-5);
        var order2 = new OrderDto(
            Id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CustomerName: "Tran Thi B",
            Items: new List<OrderItemDto>
            {
                new("P003", "Tai nghe Gaming", 1, 890000m)
            },
            TotalAmount: 890000m,
            Status: "Pending",
            CreatedAt: createdAt2,
            UpdatedAt: createdAt2
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

    public bool TryUpdateStatus(Guid id, string newStatus, DateTimeOffset updatedAt, out OrderDto? updatedOrder, out string oldStatus)
    {
        while (_orders.TryGetValue(id, out var current))
        {
            oldStatus = current.Status;
            var updated = current with { Status = newStatus, UpdatedAt = updatedAt };
            if (_orders.TryUpdate(id, updated, current))
            {
                updatedOrder = updated;
                return true;
            }
        }
        updatedOrder = null;
        oldStatus = string.Empty;
        return false;
    }
}

// Khai bao cac record DTO va Message theo yeu cau De tai 01
public record OrderItemDto(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderItemRequest(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderRequest(string CustomerName, List<CreateOrderItemRequest> Items);
public record OrderDto(Guid Id, string CustomerName, List<OrderItemDto> Items, decimal TotalAmount, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public record OrderStatusChangedMessage(Guid OrderId, string CustomerName, string OldStatus, string NewStatus, DateTimeOffset UpdatedAt, string? Reason);
public record ApiError(string Code, string Message);
