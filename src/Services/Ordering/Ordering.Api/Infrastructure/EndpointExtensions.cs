namespace Ordering.Api.Infrastructure;

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
        return await next(context);
    }
}
