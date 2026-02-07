using BackendAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly AppDbContext _db;
    public CartController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetMyCart()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var cart = await _db.CartItems
            .AsNoTracking()
            .Include(ci => ci.Product)
            .Where(ci => ci.AppUserId == userId)
            .Select(ci => new
            {
                ci.Id,
                ci.ProductId,
                ProductName = ci.Product != null ? ci.Product.Name : null,
                Price = ci.Product != null ? ci.Product.Price : 0,
                ci.Quantity,
                LineTotal = (ci.Product != null ? ci.Product.Price : 0) * ci.Quantity
            })
            .ToListAsync();

        var total = cart.Sum(x => x.LineTotal);
        return Ok(new { items = cart, total });
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromQuery] int productId, [FromQuery] int quantity)
    {
        if (quantity <= 0) return BadRequest("Quantity must be > 0.");

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var product = await _db.Products.FindAsync(productId);
        if (product is null || !product.IsActive) return BadRequest("Invalid product.");
        if (product.StockQty < quantity) return BadRequest("Not enough stock.");

        var existing = await _db.CartItems.FirstOrDefaultAsync(c => c.AppUserId == userId && c.ProductId == productId);

        if (existing is null)
        {
            _db.CartItems.Add(new BackendAPI.Models.CartItem
            {
                AppUserId = userId,
                ProductId = productId,
                Quantity = quantity
            });
        }
        else
        {
            existing.Quantity += quantity;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Added to cart." });
    }

    [HttpDelete("clear")]
public async Task<IActionResult> Clear()
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    var items = await _db.CartItems.Where(c => c.AppUserId == userId).ToListAsync();
    if (items.Count == 0) return NoContent();

    _db.CartItems.RemoveRange(items);
    await _db.SaveChangesAsync();
    return NoContent();
}


    [HttpDelete("{cartItemId:int}")]
    public async Task<IActionResult> Remove(int cartItemId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.Id == cartItemId && c.AppUserId == userId);
        if (item is null) return NotFound();

        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ✅ Checkout: converts cart → order, clears cart, reduces stock
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] BackendAPI.Dtos.CreateOrderRequest req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var cartItems = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.AppUserId == userId)
            .ToListAsync();

        if (!cartItems.Any()) return BadRequest("Cart is empty.");

        // build order request from cart
        req.Items = cartItems.Select(ci => new BackendAPI.Dtos.CreateOrderItem
        {
            ProductId = ci.ProductId,
            Quantity = ci.Quantity
        }).ToList();

        // reuse your existing OrdersController Create logic approach (duplicate minimal logic here)
        var order = new BackendAPI.Models.Order
        {
            AppUserId = userId,
            ShippingName = req.ShippingName,
            ShippingAddress = req.ShippingAddress,
            Phone = req.Phone,
            Status = "Pending",
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}"
        };

        decimal total = 0;

        foreach (var ci in cartItems)
        {
            var product = ci.Product!;
            if (!product.IsActive) return BadRequest($"Inactive product: {product.Name}");
            if (product.StockQty < ci.Quantity) return BadRequest($"Not enough stock for {product.Name}");

            product.StockQty -= ci.Quantity;

            var lineTotal = product.Price * ci.Quantity;
            total += lineTotal;

            order.Items.Add(new BackendAPI.Models.OrderItem
            {
                ProductId = product.Id,
                Quantity = ci.Quantity,
                UnitPrice = product.Price,
                LineTotal = lineTotal
            });
        }

        order.TotalAmount = total;

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cartItems);
        await _db.SaveChangesAsync();

        return Ok(new { order.Id, order.OrderNumber, order.TotalAmount });
    }
}
