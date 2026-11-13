using System.Net.Http.Json;

var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
};

Console.WriteLine($"=== KET NOI DEN ORDER API TAI: {apiBaseUrl} ===\n");

// Buoc 1: Goi GET /orders bang GetFromJsonAsync va in danh sach don hang hien co ra man hinh Console
Console.WriteLine("--- BUOC 1: LAY DANH SACH DON HANG HIEN CO (GET /orders) ---");
var existingOrders = await httpClient.GetFromJsonAsync<List<OrderResult>>("/orders");

if (existingOrders is not null && existingOrders.Count > 0)
{
    foreach (var order in existingOrders)
    {
        Console.WriteLine($"[Don hang {order.Id}]");
        Console.WriteLine($"  Khach hang: {order.CustomerName}");
        Console.WriteLine($"  Trang thai: {order.Status}");
        Console.WriteLine($"  Thoi diem tao: {order.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}");
        Console.WriteLine("  Danh sach san pham:");
        foreach (var item in order.Items)
        {
            Console.WriteLine($"    - {item.ProductName} (Ma: {item.ProductId}) | So luong: {item.Quantity} | Don gia: {item.UnitPrice:N0} VND");
        }
        Console.WriteLine();
    }
}
else
{
    Console.WriteLine("Khong co don hang nao.\n");
}

// Buoc 2: Tao mot CreateOrderRequest mau (gom ten khach hang, danh sach 2 san pham va so luong) va goi POST /orders bang PostAsJsonAsync
Console.WriteLine("--- BUOC 2: TAO DON HANG MOI (POST /orders) ---");
var newOrderRequest = new CreateOrderRequest(
    CustomerName: "Le Van Cuong",
    Items: new List<OrderItem>
    {
        new("P101", "Man hinh 27 inch 4K", 1, 8500000m),
        new("P102", "Gia do man hinh cong thai hoc", 2, 1250000m)
    }
);

var response = await httpClient.PostAsJsonAsync("/orders", newOrderRequest);

// Buoc 3: Goi response.EnsureSuccessStatusCode(), doc ket qua tra ve bang ReadFromJsonAsync<OrderResult>() va in ra ma don hang vua tao kem trang thai \"Pending\" va thoi diem tao
response.EnsureSuccessStatusCode();

var createdOrder = await response.Content.ReadFromJsonAsync<OrderResult>();

Console.WriteLine("--- BUOC 3: KET QUA TAO DON HANG ---");
if (createdOrder is not null)
{
    Console.WriteLine("Tao don hang thanh cong!");
    Console.WriteLine($"Ma don hang: {createdOrder.Id}");
    Console.WriteLine($"Khach hang: {createdOrder.CustomerName}");
    Console.WriteLine($"Trang thai: {createdOrder.Status}");
    Console.WriteLine($"Thoi diem tao: {createdOrder.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}");
}
else
{
    Console.WriteLine("Loi: Khong doc duoc du lieu don hang tra ve.");
}

public record OrderItem(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderRequest(string CustomerName, List<OrderItem> Items);
public record OrderResult(Guid Id, string CustomerName, List<OrderItem> Items, string Status, DateTimeOffset CreatedAt);

