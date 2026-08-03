namespace Promotion.Domain.Entities;

public sealed class Coupon
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "PERCENTAGE";
    public decimal Value { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);
}
