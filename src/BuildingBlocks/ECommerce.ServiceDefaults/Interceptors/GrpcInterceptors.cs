using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace ECommerce.ServiceDefaults.Interceptors;

public class GrpcJwtHeaderInterceptor : Interceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GrpcJwtHeaderInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext != null && httpContext.Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationHeader))
        {
            var headers = context.Options.Headers ?? new Metadata();
            if (headers.Get("Authorization") == null)
            {
                headers.Add("Authorization", authorizationHeader.ToString());
            }

            var newOptions = context.Options.WithHeaders(headers);
            context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, newOptions);
        }

        return continuation(request, context);
    }
}

public class GrpcTraceContextInterceptor : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, context);
    }
}
