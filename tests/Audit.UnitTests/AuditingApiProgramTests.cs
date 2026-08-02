using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Audit.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Audit.UnitTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}

public class AuditApiProgramTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuditApiProgramTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAuditLogsEndpoint_ShouldReturn200OK_WithEmptyList_WhenNoLogsExist()
    {
        // Arrange - Clear existing logs from previous tests
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            db.AuditLogs.RemoveRange(db.AuditLogs);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/audit-logs", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.ShouldContain("totalCount\":0");
    }

    [Fact]
    public async Task GetAuditLogsEndpoint_ShouldReturnFilteredLogs_WhenLogsExistInDb()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            db.AuditLogs.Add(new AuditLogRecord
            {
                UserId = "api-user-1",
                Action = "Create",
                EntityName = "Invoice",
                EntityId = "INV-1",
                Changes = "{}",
                Timestamp = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/audit-logs?entityName=Invoice", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.ShouldContain("Invoice");
        json.ShouldContain("api-user-1");
    }

    [Fact]
    public async Task GetAuditLogsEndpoint_ShouldApplyBothFilters_OrderByNewest_AndPaginate()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            db.AuditLogs.RemoveRange(db.AuditLogs);
            var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
            db.AuditLogs.AddRange(
                CreateLog("target-user", "Order", "old", baseTime.AddMinutes(1)),
                CreateLog("target-user", "Order", "new", baseTime.AddMinutes(3)),
                CreateLog("different-user", "Order", "excluded", baseTime.AddMinutes(4)),
                CreateLog("target-user", "Product", "excluded-too", baseTime.AddMinutes(5)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _factory.CreateClient().GetAsync(
            "/api/audit-logs?entityName=Order&userId=target-user&page=2&pageSize=1",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.ShouldContain("\"totalCount\":2");
        json.ShouldContain("\"page\":2");
        json.ShouldContain("\"pageSize\":1");
        json.ShouldContain("\"entityId\":\"old\"");
        json.ShouldNotContain("excluded");
    }

    private static AuditLogRecord CreateLog(string userId, string entityName, string entityId, DateTimeOffset timestamp) => new()
    {
        UserId = userId,
        Action = "Created",
        EntityName = entityName,
        EntityId = entityId,
        Changes = "{}",
        Timestamp = timestamp
    };
}
