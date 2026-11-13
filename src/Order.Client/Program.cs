using System.Net;
using System.Net.Http.Json;

var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
};

Console.WriteLine($"========================================================");
Console.WriteLine($"=== KET NOI ORDER.CLIENT TOI ORDER.API: {apiBaseUrl} ===");
Console.WriteLine($"========================================================\n");

// Buoc 1: Goi GET /orders de in danh sach don hang hien co
Console.WriteLine("=== BUOC 1: GOI GET /orders DE IN DANH SACH DON HANG HIEN CO ===");
var existingOrders = await httpClient.GetFromJsonAsync<List<OrderDto>>("/orders");

if (existingOrders is not null && existingOrders.Count > 0)
{
    foreach (var order in existingOrders)
    {
        Console.WriteLine($"[Don hang: {order.Id}]");
        Console.WriteLine($"  Khach hang   : {order.CustomerName}");
        Console.WriteLine($"  Trang thai   : {order.Status}");
        Console.WriteLine($"  Tong tien    : {order.TotalAmount:N0} VND");
        Console.WriteLine($"  Thoi diem tao: {order.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}");
        Console.WriteLine("  Danh sach san pham:");
        foreach (var item in order.Items)
        {
            Console.WriteLine($"    - {item.ProductName} ({item.ProductId}) | SL: {item.Quantity} | Don gia: {item.UnitPrice:N0} VND");
        }
        Console.WriteLine();
    }
}
else
{
    Console.WriteLine("Khong co don hang nao.\n");
}

// Buoc 2: Gui POST /orders hop le, doc OrderDto vua tao va in thong tin
Console.WriteLine("=== BUOC 2: GUI POST /orders HOP LE DE TAO DON HANG MOI ===");
var validRequest = new CreateOrderRequest(
    CustomerName: "Pham Minh Hoang",
    Items: new List<CreateOrderItemRequest>
    {
        new("P101", "Ban phim co RGB", 1, 1500000m),
        new("P102", "Chuot chuyen Gaming", 2, 600000m)
    }
);

var createResponse = await httpClient.PostAsJsonAsync("/orders", validRequest);
OrderDto? createdOrder = null;

if (createResponse.StatusCode == HttpStatusCode.Created)
{
    createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
    Console.WriteLine("Tao don hang thanh cong (201 Created):");
    Console.WriteLine($"  - Id         : {createdOrder?.Id}");
    Console.WriteLine($"  - Khach hang : {createdOrder?.CustomerName}");
    Console.WriteLine($"  - Trang thai : {createdOrder?.Status}");
    Console.WriteLine($"  - Tong tien  : {createdOrder?.TotalAmount:N0} VND");
    Console.WriteLine($"  - Thoi diem  : {createdOrder?.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}\n");
}
else
{
    Console.WriteLine("Tao don hang that bai:");
    await PrintApiErrorAsync(createResponse);
    Console.WriteLine();
}

// Buoc 3: Dung Id vua nhan duoc goi GET /orders/{id} de doc lai chi tiet don hang va xac nhan khop thong tin
Console.WriteLine("=== BUOC 3: GOI GET /orders/{id} VOI ID VUA TAO DE XAC NHAN DU LIEU ===");
if (createdOrder is not null)
{
    var getResponse = await httpClient.GetAsync($"/orders/{createdOrder.Id}");
    if (getResponse.IsSuccessStatusCode)
    {
        var fetchedOrder = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
        Console.WriteLine($"Doc thanh cong don hang tu server:");
        Console.WriteLine($"  - Id         : {fetchedOrder?.Id}");
        Console.WriteLine($"  - Khach hang : {fetchedOrder?.CustomerName}");
        Console.WriteLine($"  - Trang thai : {fetchedOrder?.Status}");
        Console.WriteLine($"  - Tong tien  : {fetchedOrder?.TotalAmount:N0} VND");

        bool isMatched = fetchedOrder?.Id == createdOrder.Id &&
                         fetchedOrder?.CustomerName == createdOrder.CustomerName &&
                         fetchedOrder?.TotalAmount == createdOrder.TotalAmount;

        Console.WriteLine($"  => Ket luan: Thong tin don hang {(isMatched ? "HOAN TOAN KHOP NHAU" : "KHONG KHOP")}\n");
    }
    else
    {
        Console.WriteLine("Doc don hang that bai:");
        await PrintApiErrorAsync(getResponse);
        Console.WriteLine();
    }
}
else
{
    Console.WriteLine("Bo qua buoc 3 do buoc 2 tao don hang khong thanh cong.\n");
}

// Buoc 4: Co y gui POST /orders voi du lieu sai (CustomerName rong, Items rong) de in loi 400 BadRequest
Console.WriteLine("=== BUOC 4: CO Y GUI POST /orders VOI DU LIEU SAI (CustomerName = \"\", Items = []) ===");
var invalidRequest = new CreateOrderRequest(
    CustomerName: "",
    Items: new List<CreateOrderItemRequest>()
);

var badRequestResponse = await httpClient.PostAsJsonAsync("/orders", invalidRequest);
Console.WriteLine($"Trang thai HTTP nhan duoc: {(int)badRequestResponse.StatusCode} {badRequestResponse.StatusCode}");
await PrintApiErrorAsync(badRequestResponse);
Console.WriteLine();

// Buoc 5: Co y goi GET /orders/{Guid.NewGuid()} voi mot ID khong ton tai de in loi 404 NotFound
Console.WriteLine("=== BUOC 5: CO Y GOI GET /orders/{Guid.NewGuid()} VOI ID KHONG TON TAI ===");
var nonExistentId = Guid.NewGuid();
var notFoundResponse = await httpClient.GetAsync($"/orders/{nonExistentId}");
Console.WriteLine($"Trang thai HTTP nhan duoc: {(int)notFoundResponse.StatusCode} {notFoundResponse.StatusCode}");
await PrintApiErrorAsync(notFoundResponse);
Console.WriteLine();

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

// Khai bao cac record DTO tuong thich de trao doi du lieu voi Order.Api
public record OrderItemDto(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderItemRequest(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderRequest(string CustomerName, List<CreateOrderItemRequest> Items);
public record OrderDto(Guid Id, string CustomerName, List<OrderItemDto> Items, decimal TotalAmount, string Status, DateTimeOffset CreatedAt);
public record ApiError(string Code, string Message);
