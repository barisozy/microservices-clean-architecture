using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Diagnostics;

namespace ECommerce.ServiceDefaults.Interceptors;

public sealed class GrpcTraceContextInterceptor : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var activityId = Activity.Current?.Id;
        if (!string.IsNullOrWhiteSpace(activityId))
        {
            var headers = context.Options.Headers ?? new Metadata();
            if (headers.Get("traceparent") is null) headers.Add("traceparent", activityId);
            context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, context.Options.WithHeaders(headers));
        }
        return continuation(request, context);
    }
}
