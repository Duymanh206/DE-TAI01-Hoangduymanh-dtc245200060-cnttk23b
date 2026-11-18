using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
};

Console.WriteLine("==========================================================================");
Console.WriteLine($"=== KET NOI ORDER.CLIENT TOI ORDER.API & SIGNALR HUB: {apiBaseUrl} ===");
Console.WriteLine("==========================================================================\n");

// 1. Thiet lap ket noi SignalR Hub voi che do tu dong ket noi lai
var hubUrl = $"{apiBaseUrl}/hubs/orders";
await using var hubConnection = new HubConnectionBuilder()
    .WithUrl(hubUrl)
    .WithAutomaticReconnect()
    .Build();

// Dictionary luu TaskCompletionSource de cho cac don hang dat trang thai cuoi (Completed/Failed)
var completionSources = new ConcurrentDictionary<Guid, TaskCompletionSource<OrderStatusChangedMessage>>();

// Dang ky lang nghe su kien thoi gian thuc OrderStatusChanged tu OrderHub
hubConnection.On<OrderStatusChangedMessage>("OrderStatusChanged", message =>
{
    Console.WriteLine($"\n>>> [SIGNALR THONG BAO THOI GIAN THUC] <<<");
    Console.WriteLine($"  Ma don hang     : {message.OrderId}");
    Console.WriteLine($"  Khach hang      : {message.CustomerName}");
    Console.WriteLine($"  Chuyen trang thai: {message.OldStatus} -> {message.NewStatus}");
    Console.WriteLine($"  Thoi diem       : {message.UpdatedAt:yyyy-MM-dd HH:mm:ss.fff zzz}");
    if (!string.IsNullOrEmpty(message.Reason))
    {
        Console.WriteLine($"  Ly do chi tiet  : {message.Reason}");
    }
    Console.WriteLine("--------------------------------------------------------------------------");

    if (message.NewStatus is "Completed" or "Failed")
    {
        if (completionSources.TryGetValue(message.OrderId, out var tcs))
        {
            tcs.TrySetResult(message);
        }
    }
});

// Mo ket noi SignalR
Console.WriteLine($"[1] Dang ket noi toi SignalR Hub: {hubUrl} ...");
await hubConnection.StartAsync();
Console.WriteLine($"  => Ket noi thanh cong! ConnectionId: {hubConnection.ConnectionId}\n");

// ==========================================================================
// KICH BAN 1: Tao don hang binh thuong -> Quan sat luong Pending -> Processing -> Completed
// ==========================================================================
Console.WriteLine("==========================================================================");
Console.WriteLine("=== KICH BAN 1: TAO DON HANG HOP LE (Pending -> Processing -> Completed) ===");
Console.WriteLine("==========================================================================");

var normalOrderRequest = new CreateOrderRequest(
    CustomerName: "Pham Minh Hoang",
    Items: new List<CreateOrderItemRequest>
    {
        new("P101", "Ban phim co RGB", 1, 1500000m),
        new("P102", "Chuot chuyen Gaming", 2, 600000m)
    }
);

Console.WriteLine("Goi POST /orders de tao don hang...");
var response1 = await httpClient.PostAsJsonAsync("/orders", normalOrderRequest);
if (response1.StatusCode == HttpStatusCode.Created)
{
    var order1 = await response1.Content.ReadFromJsonAsync<OrderDto>();
    Console.WriteLine($"Tao thanh cong don hang (201 Created):");
    Console.WriteLine($"  - Id         : {order1?.Id}");
    Console.WriteLine($"  - Khach hang : {order1?.CustomerName}");
    Console.WriteLine($"  - Trang thai : {order1?.Status}");
    Console.WriteLine($"  - Tong tien  : {order1?.TotalAmount:N0} VND");

    if (order1 is not null)
    {
        var tcs1 = new TaskCompletionSource<OrderStatusChangedMessage>();
        completionSources[order1.Id] = tcs1;

        // Goi WatchOrder tren Hub de dang ky vao group order:{orderId}
        Console.WriteLine($"Goi Hub.WatchOrder(\"{order1.Id}\") de nhan thong bao trang thai...");
        await hubConnection.InvokeAsync("WatchOrder", order1.Id.ToString());

        Console.WriteLine("Dang cho SignalR day thong bao cap nhat trang thai...\n");
        var finalMsg1 = await tcs1.Task;
        Console.WriteLine($"\n[Kich ban 1 Hoan tat] Don hang {order1.Id} da dat trang thai cuoi: {finalMsg1.NewStatus}");

        // Goi GET /orders/{id} qua REST de xac nhan du lieu luu tren server
        Console.WriteLine("\nGoi GET /orders/{id} qua REST de xac minh trang thai tren server...");
        var getResponse = await httpClient.GetAsync($"/orders/{order1.Id}");
        if (getResponse.IsSuccessStatusCode)
        {
            var serverOrder = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
            Console.WriteLine($"  - Id server         : {serverOrder?.Id}");
            Console.WriteLine($"  - Trang thai server : {serverOrder?.Status}");
            Console.WriteLine($"  - Thoi diem tao     : {serverOrder?.CreatedAt:yyyy-MM-dd HH:mm:ss.fff zzz}");
            Console.WriteLine($"  - Thoi diem cap nhat: {serverOrder?.UpdatedAt:yyyy-MM-dd HH:mm:ss.fff zzz}");
            Console.WriteLine($"  => Xac nhan REST API va SignalR dong bo trang thai ({serverOrder?.Status}).");
        }

        // Huy theo doi don hang
        await hubConnection.InvokeAsync("UnwatchOrder", order1.Id.ToString());
    }
}
else
{
    Console.WriteLine("Tao don hang that bai:");
    await PrintApiErrorAsync(response1);
}

Console.WriteLine("\n");

// ==========================================================================
// KICH BAN 2: Tao don hang co san pham OUT_OF_STOCK -> Pending -> Processing -> Failed
// ==========================================================================
Console.WriteLine("==========================================================================");
Console.WriteLine("=== KICH BAN 2: TAO DON HANG HET HANG (Pending -> Processing -> Failed) ===");
Console.WriteLine("==========================================================================");

var outOfStockRequest = new CreateOrderRequest(
    CustomerName: "Dang Thi Mai",
    Items: new List<CreateOrderItemRequest>
    {
        new("P201", "CPU Core i9 the he moi", 1, 14000000m),
        new("OUT_OF_STOCK", "VGA RTX 4090 24GB (Tam het hang)", 1, 48000000m)
    }
);

Console.WriteLine("Goi POST /orders tao don hang chua san pham het hang...");
var response2 = await httpClient.PostAsJsonAsync("/orders", outOfStockRequest);
if (response2.StatusCode == HttpStatusCode.Created)
{
    var order2 = await response2.Content.ReadFromJsonAsync<OrderDto>();
    Console.WriteLine($"Tao thanh cong don hang (201 Created):");
    Console.WriteLine($"  - Id         : {order2?.Id}");
    Console.WriteLine($"  - Khach hang : {order2?.CustomerName}");
    Console.WriteLine($"  - Trang thai : {order2?.Status}");
    Console.WriteLine($"  - Tong tien  : {order2?.TotalAmount:N0} VND");

    if (order2 is not null)
    {
        var tcs2 = new TaskCompletionSource<OrderStatusChangedMessage>();
        completionSources[order2.Id] = tcs2;

        // Goi WatchOrder tren Hub de dang ky vao group order:{orderId}
        Console.WriteLine($"Goi Hub.WatchOrder(\"{order2.Id}\") de nhan thong bao trang thai...");
        await hubConnection.InvokeAsync("WatchOrder", order2.Id.ToString());

        Console.WriteLine("Dang cho SignalR day thong bao cap nhat trang thai...\n");
        var finalMsg2 = await tcs2.Task;
        Console.WriteLine($"\n[Kich ban 2 Hoan tat] Don hang {order2.Id} da dat trang thai cuoi: {finalMsg2.NewStatus}");
        Console.WriteLine($"  - Ly do that bai    : {finalMsg2.Reason}");

        // Goi GET /orders/{id} qua REST de xac nhan du lieu luu tren server
        Console.WriteLine("\nGoi GET /orders/{id} qua REST de xac minh trang thai tren server...");
        var getResponse2 = await httpClient.GetAsync($"/orders/{order2.Id}");
        if (getResponse2.IsSuccessStatusCode)
        {
            var serverOrder2 = await getResponse2.Content.ReadFromJsonAsync<OrderDto>();
            Console.WriteLine($"  - Id server         : {serverOrder2?.Id}");
            Console.WriteLine($"  - Trang thai server : {serverOrder2?.Status}");
            Console.WriteLine($"  - Thoi diem cap nhat: {serverOrder2?.UpdatedAt:yyyy-MM-dd HH:mm:ss.fff zzz}");
            Console.WriteLine($"  => Xac nhan REST API va SignalR dong bo trang thai ({serverOrder2?.Status}).");
        }

        // Huy theo doi don hang
        await hubConnection.InvokeAsync("UnwatchOrder", order2.Id.ToString());
    }
}
else
{
    Console.WriteLine("Tao don hang that bai:");
    await PrintApiErrorAsync(response2);
}

// Ngat ket noi SignalR truoc khi ket thuc chuong trinh
await hubConnection.StopAsync();
Console.WriteLine("\n=== DA HOAN TAT TOAN BO KICH BAN THOI GIAN THUC CHUONG 3 ===");

// Ham ho tro doc va in thong tin loi ApiError tu phan hoi HTTP
static async Task PrintApiErrorAsync(HttpResponseMessage response)
{
    try
    {
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        if (error is not null)
        {
            Console.WriteLine($"  [Chi tiet ApiError]");
            Console.WriteLine($"  - Code   : {error.Code}");
            Console.WriteLine($"  - Message: {error.Message}");
        }
        else
        {
            var raw = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"  - Noi dung raw: {raw}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  - Khong the parse ApiError: {ex.Message}");
    }
}

// Khai bao cac record DTO va Message tuong thich de trao doi du lieu voi Order.Api
public record OrderItemDto(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderItemRequest(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderRequest(string CustomerName, List<CreateOrderItemRequest> Items);
public record OrderDto(Guid Id, string CustomerName, List<OrderItemDto> Items, decimal TotalAmount, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public record OrderStatusChangedMessage(Guid OrderId, string CustomerName, string OldStatus, string NewStatus, DateTimeOffset UpdatedAt, string? Reason);
public record ApiError(string Code, string Message);
