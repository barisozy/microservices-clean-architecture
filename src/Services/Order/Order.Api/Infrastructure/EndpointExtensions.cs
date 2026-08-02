namespace Order.Api.Infrastructure;

public interface IEndpointGroup
{
    void Map(WebApplication app);
}

public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var endpointGroupTypes = typeof(EndpointExtensions).Assembly.GetTypes()
            .Where(t => typeof(IEndpointGroup).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in endpointGroupTypes)
        {
            if (Activator.CreateInstance(type) is IEndpointGroup instance)
            {
                instance.Map(app);
            }
        }

        return app;
    }
}

public class IdempotencyKeyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var value = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadHttpRequestException(
                "Idempotency-Key header is required.",
                StatusCodes.Status400BadRequest);
        }

        if (!Guid.TryParseExact(value, "D", out var key) || key.ToString("D")[14] != '7')
        {
            throw new BadHttpRequestException(
                "Idempotency-Key must be a canonical UUIDv7 value.",
                StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}

