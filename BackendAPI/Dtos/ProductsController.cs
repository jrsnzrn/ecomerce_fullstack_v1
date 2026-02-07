using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    // ✅ Public: list active products
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.StockQty,
                p.ImageUrl,
                p.IsActive,
                p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null
            })
            .ToListAsync();

        return Ok(products);
    }

    // ✅ Public: get single product
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await _db.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Price,
                x.StockQty,
                x.ImageUrl,
                x.IsActive,
                x.CategoryId,
                CategoryName = x.Category != null ? x.Category.Name : null
            })
            .FirstOrDefaultAsync();

        return p is null ? NotFound() : Ok(p);
    }

    // ✅ Admin only: create
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(ProductCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");
        if (req.Price < 0) return BadRequest("Price must be >= 0.");
        if (req.StockQty < 0) return BadRequest("StockQty must be >= 0.");

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == req.CategoryId);
        if (!categoryExists) return BadRequest("CategoryId does not exist.");

        var product = new Product
        {
            Name = req.Name.Trim(),
            Description = req.Description,
            Price = req.Price,
            StockQty = req.StockQty,
            ImageUrl = req.ImageUrl,
            CategoryId = req.CategoryId,
            IsActive = req.IsActive
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return Ok(product);
    }

    // ✅ Admin only: update
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductUpdateRequest req)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");
        if (req.Price < 0) return BadRequest("Price must be >= 0.");
        if (req.StockQty < 0) return BadRequest("StockQty must be >= 0.");

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == req.CategoryId);
        if (!categoryExists) return BadRequest("CategoryId does not exist.");

        product.Name = req.Name.Trim();
        product.Description = req.Description;
        product.Price = req.Price;
        product.StockQty = req.StockQty;
        product.ImageUrl = req.ImageUrl;
        product.CategoryId = req.CategoryId;
        product.IsActive = req.IsActive;

        await _db.SaveChangesAsync();
        return Ok(product);
    }

    // ✅ Admin only: delete
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
