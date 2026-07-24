using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Auditing.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Auditing.UnitTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}

public class AuditingApiProgramTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuditingApiProgramTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAuditLogsEndpoint_ShouldReturn200OK_WithEmptyList_WhenNoLogsExist()
    {
        // Arrange - Clear existing logs from previous tests
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();
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
            var db = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();
            db.AuditLogs.Add(new AuditLogRecord
            {
                Id = Guid.NewGuid(),
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
}
