using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediatR;
using Moq;
using Order.Api.Infrastructure;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class OrderApiInfrastructureTests
{
    [Fact]
    public async Task ProblemDetailsExceptionHandler_ShouldWriteRfc9457Response()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var handler = new ProblemDetailsExceptionHandler(loggerFactory.CreateLogger<ProblemDetailsExceptionHandler>());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("order failed"),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("order failed");
        body.ShouldContain("An unexpected server error occurred.");
        body.ShouldContain("An error occurred while processing your request.");
        context.Response.ContentType.ShouldStartWith("application/problem+json");
    }

    [Fact]
    public void MapEndpoints_ShouldRegisterOrderAndBasketRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ISender>(Mock.Of<ISender>());
        using var app = builder.Build();

        app.MapEndpoints();

        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        routes.ShouldContain("/api/v1/orders/");
        routes.ShouldContain("/api/v1/orders/{orderId:guid}");
        routes.ShouldContain("/api/v1/basket/");
    }

    [Fact]
    public async Task IdempotencyKeyFilter_ShouldInvokeNextFilterInPipeline()
    {
        var filter = new IdempotencyKeyFilter();
        var context = new Mock<EndpointFilterInvocationContext>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.SetupGet(value => value.HttpContext).Returns(httpContext);
        var executedNext = false;
        EndpointFilterDelegate next = _ =>
        {
            executedNext = true;
            return ValueTask.FromResult<object?>("result");
        };

        var res = await filter.InvokeAsync(context.Object, next);

        executedNext.ShouldBeTrue();
        res.ShouldBe("result");
    }
}
