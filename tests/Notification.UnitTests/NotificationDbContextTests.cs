using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace Notification.UnitTests;

public class NotificationDbContextTests
{
    [Fact]
    public void Model_ShouldConfigureNotificationLogPrimaryKey()
    {
        using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Model.FindEntityType(typeof(NotificationLog))!.FindPrimaryKey()!.Properties.Single().Name.ShouldBe(nameof(NotificationLog.Id));
    }
}
