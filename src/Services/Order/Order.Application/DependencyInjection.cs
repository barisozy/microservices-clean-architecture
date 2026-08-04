using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Common.Behaviors;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.EventHandlers;
using Order.Domain.Events;

namespace Order.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<OrderCreatedDomainEvent>, OrderReadModelUpdater>();
        services.AddScoped<IDomainEventHandler<OrderInventoryConfirmedDomainEvent>, OrderReadModelUpdater>();
        services.AddScoped<IDomainEventHandler<OrderCancelledDomainEvent>, OrderReadModelUpdater>();
        services.AddScoped<IDomainEventHandler<OrderPaidDomainEvent>, OrderReadModelUpdater>();
        services.AddScoped<IDomainEventHandler<OrderShippedDomainEvent>, OrderReadModelUpdater>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });
        
        services.AddSingleton<Checkout.CheckoutMetrics>();

        return services;
    }
}
