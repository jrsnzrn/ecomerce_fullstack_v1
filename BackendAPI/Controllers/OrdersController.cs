using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db)
    {
        _db = db;
    }

    // 🛒 PLACE ORDER (USER)
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest req)
    {
       if (!req.Items.Any())
    return BadRequest("Order must contain items.");

        foreach (var item in req.Items)
            {
            if (item.Quantity <= 0)
        return BadRequest("Quantity must be greater than 0.");
            }


        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

      var order = new Order
{
    AppUserId = userId,
    ShippingName = req.ShippingName,
    ShippingAddress = req.ShippingAddress,
    Phone = req.Phone,
    Status = "Pending",
    OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}"
};


        decimal total = 0;

        foreach (var item in req.Items)
        {
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product is null || !product.IsActive)
                return BadRequest($"Invalid product {item.ProductId}");

            if (product.StockQty < item.Quantity)
                return BadRequest($"Not enough stock for {product.Name}");

            product.StockQty -= item.Quantity;

            var lineTotal = product.Price * item.Quantity;
            total += lineTotal;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                LineTotal = lineTotal
            });
        }

        order.TotalAmount = total;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return Ok(new { order.Id, order.TotalAmount });
    }

   [HttpGet("my")]
public async Task<IActionResult> MyOrders()
{
    var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);

    var orders = await _db.Orders
        .AsNoTracking()
        .Include(o => o.Items)
            .ThenInclude(i => i.Product)
        .Where(o => o.AppUserId == userId)
        .OrderByDescending(o => o.Id)
        .Select(o => new
        {
            o.Id,
            o.Status,
            o.CreatedAt,
            o.TotalAmount,
            o.ShippingName,
            o.ShippingAddress,
            o.Phone,
            Items = o.Items.Select(i => new
            {
                i.Id,
                i.ProductId,
                ProductName = i.Product != null ? i.Product.Name : null,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal
            })
        })
        .ToListAsync();

    return Ok(orders);
}


   [Authorize(Roles = "Admin")]
[HttpGet]
public async Task<IActionResult> GetAll()
{
    var orders = await _db.Orders
        .AsNoTracking()
        .Include(o => o.AppUser)
        .Include(o => o.Items)
            .ThenInclude(i => i.Product)
        .OrderByDescending(o => o.Id)
        .Select(o => new
        {
            o.Id,
            o.AppUserId,
            CustomerEmail = o.AppUser != null ? o.AppUser.Email : null,
            o.Status,
            o.CreatedAt,
            o.TotalAmount,
            o.ShippingName,
            o.ShippingAddress,
            o.Phone,
            Items = o.Items.Select(i => new
            {
                i.Id,
                i.ProductId,
                ProductName = i.Product != null ? i.Product.Name : null,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal
            })
        })
        .ToListAsync();

    return Ok(orders);
}

[HttpPost("{id:int}/pay")]
public async Task<IActionResult> Pay(int id)
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.AppUserId == userId);
    if (order is null) return NotFound();

    if (order.Status == "Cancelled") return BadRequest("Order is cancelled.");
    if (order.Status == "Shipped") return BadRequest("Order already shipped.");
    if (order.Status == "Paid") return Ok(new { message = "Already paid.", order.Id, order.Status });

    order.Status = "Paid";
    await _db.SaveChangesAsync();

    return Ok(new { message = "Payment successful (simulated).", order.Id, order.OrderNumber, order.Status });
}


[Authorize(Roles = "Admin")]
[HttpPut("{id:int}/status")]
public async Task<IActionResult> UpdateStatus(int id, [FromQuery] string status)
{
    var allowed = new[] { "Pending", "Paid", "Shipped", "Cancelled" };
    if (!allowed.Contains(status))
        return BadRequest("Invalid status. Use Pending, Paid, Shipped, Cancelled.");

    var order = await _db.Orders
        .Include(o => o.Items)
        .FirstOrDefaultAsync(o => o.Id == id);

    if (order is null) return NotFound();

    // Rules
    if (order.Status == "Cancelled")
        return BadRequest("Cancelled orders cannot be changed.");

    if (order.Status == "Shipped" && status != "Shipped")
        return BadRequest("Shipped orders cannot be changed.");

    // Restock if cancelled
    if (status == "Cancelled" && order.Status != "Cancelled")
    {
        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
                product.StockQty += item.Quantity;
        }
    }

    order.Status = status;
    await _db.SaveChangesAsync();

    return Ok(new { order.Id, order.OrderNumber, order.Status });
}

}



