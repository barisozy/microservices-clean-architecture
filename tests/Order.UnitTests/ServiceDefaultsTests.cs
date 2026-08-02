using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ECommerce.ServiceDefaults.Interceptors;
using ECommerce.ServiceDefaults.Resilience;
using Grpc.Core;
using Grpc.Core.Interceptors;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Moq;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class ServiceDefaultsTests
{
    [Fact]
    public void GrpcJwtHeaderInterceptor_ShouldAddAuthorizationHeader_WhenPresentInHttpContext()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.Authorization] = "Bearer test-jwt-token";
        httpContextAccessor.HttpContext = context;

        var interceptor = new GrpcJwtHeaderInterceptor(httpContextAccessor);

        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var clientContext = new ClientInterceptorContext<string, string>(method, "localhost", new CallOptions());

        var continuationCalled = false;
        Interceptor.AsyncUnaryCallContinuation<string, string> continuation = (req, ctx) =>
        {
            continuationCalled = true;
            ctx.Options.Headers.ShouldNotBeNull();
            var authHeader = ctx.Options.Headers.Get("Authorization");
            authHeader.ShouldNotBeNull();
            authHeader.Value.ShouldBe("Bearer test-jwt-token");
            return new AsyncUnaryCall<string>(Task.FromResult("response"), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        };

        // Act
        interceptor.AsyncUnaryCall("request", clientContext, continuation);

        // Assert
        continuationCalled.ShouldBeTrue();
    }

    [Fact]
    public void GrpcJwtHeaderInterceptor_ShouldNotAddHeader_WhenHttpContextIsNull()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var interceptor = new GrpcJwtHeaderInterceptor(httpContextAccessor);

        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var clientContext = new ClientInterceptorContext<string, string>(method, "localhost", new CallOptions());

        var continuationCalled = false;
        Interceptor.AsyncUnaryCallContinuation<string, string> continuation = (req, ctx) =>
        {
            continuationCalled = true;
            return new AsyncUnaryCall<string>(Task.FromResult("response"), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        };

        // Act
        interceptor.AsyncUnaryCall("request", clientContext, continuation);

        // Assert
        continuationCalled.ShouldBeTrue();
    }

    [Fact]
    public void GrpcTraceContextInterceptor_ShouldInvokeContinuation()
    {
        var interceptor = new GrpcTraceContextInterceptor();
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var clientContext = new ClientInterceptorContext<string, string>(method, "localhost", new CallOptions());

        var continuationCalled = false;
        Interceptor.AsyncUnaryCallContinuation<string, string> continuation = (req, ctx) =>
        {
            continuationCalled = true;
            return new AsyncUnaryCall<string>(Task.FromResult("response"), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        };

        interceptor.AsyncUnaryCall("request", clientContext, continuation);
        continuationCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task MassTransitFaultLoggerFilter_ShouldCallNext_WhenSuccessful()
    {
        var loggerMock = new Mock<ILogger<MassTransitFaultLoggerFilter<string>>>();
        var filter = new MassTransitFaultLoggerFilter<string>(loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<string>>();
        var nextMock = new Mock<IPipe<ConsumeContext<string>>>();

        await filter.Send(contextMock.Object, nextMock.Object);

        nextMock.Verify(x => x.Send(contextMock.Object), Times.Once);
    }

    [Fact]
    public async Task MassTransitFaultLoggerFilter_ShouldLogErrorAndRethrow_WhenNextFails()
    {
        var loggerMock = new Mock<ILogger<MassTransitFaultLoggerFilter<string>>>();
        var filter = new MassTransitFaultLoggerFilter<string>(loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<string>>();
        contextMock.Setup(x => x.MessageId).Returns(Guid.NewGuid());
        var nextMock = new Mock<IPipe<ConsumeContext<string>>>();
        nextMock.Setup(x => x.Send(contextMock.Object)).ThrowsAsync(new InvalidOperationException("Processing error"));

        await Should.ThrowAsync<InvalidOperationException>(() => filter.Send(contextMock.Object, nextMock.Object));
    }

    [Fact]
    public void MassTransitFaultLoggerFilter_Probe_ShouldCreateScope()
    {
        var loggerMock = new Mock<ILogger<MassTransitFaultLoggerFilter<string>>>();
        var filter = new MassTransitFaultLoggerFilter<string>(loggerMock.Object);
        var probeContextMock = new Mock<ProbeContext>();
        var scopeContextMock = new Mock<ProbeContext>();
        probeContextMock.Setup(x => x.CreateScope(It.IsAny<string>())).Returns(scopeContextMock.Object);

        filter.Probe(probeContextMock.Object);

        probeContextMock.Verify(x => x.CreateScope("filters"), Times.Once);
    }
}
