using Microsoft.EntityFrameworkCore;
using Promotion.Domain.Entities;
using Promotion.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace Promotion.UnitTests;

public class PromotionDbContextTests
{
    [Fact]
    public void Model_ShouldUseCouponCodeAndCampaignKeys()
    {
        using var db = new PromotionDbContext(new DbContextOptionsBuilder<PromotionDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var coupon = db.Model.FindEntityType(typeof(Coupon))!;

        coupon.FindPrimaryKey()!.Properties.Single().Name.ShouldBe(nameof(Coupon.Id));
        coupon.GetIndexes().ShouldContain(index => index.Properties.Single().Name == nameof(Coupon.Code) && index.IsUnique);
        db.Model.FindEntityType(typeof(Campaign))!.FindPrimaryKey()!.Properties.Single().Name.ShouldBe(nameof(Campaign.Id));
    }
}
