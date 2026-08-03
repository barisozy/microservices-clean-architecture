using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace ECommerce.ServiceDefaults.Interceptors;

public sealed class GrpcJwtHeaderInterceptor(IHttpContextAccessor httpContextAccessor) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null && httpContext.Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationHeader))
        {
            var headers = context.Options.Headers ?? new Metadata();
            if (headers.Get("authorization") is null) headers.Add("authorization", authorizationHeader.ToString());
            context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, context.Options.WithHeaders(headers));
        }
        return continuation(request, context);
    }
}
