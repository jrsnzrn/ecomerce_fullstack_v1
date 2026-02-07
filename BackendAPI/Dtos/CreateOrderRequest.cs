namespace BackendAPI.Dtos;

public class CreateOrderRequest
{
    public string ShippingName { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string Phone { get; set; } = "";
    public List<CreateOrderItem> Items { get; set; } = new();
}

public class CreateOrderItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
