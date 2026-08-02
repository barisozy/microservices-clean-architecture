using ECommerce.Auditing;
using Shouldly;
using Xunit;

namespace Audit.UnitTests;

public sealed class AuditingBoundaryTests
{
    [Fact]
    public void AuditingAssembly_DoesNotReferenceMessagingOrMediatR()
    {
        var references = typeof(AuditableEntityInterceptor).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        references.ShouldNotContain("MassTransit");
        references.ShouldNotContain("MediatR");
        references.ShouldNotContain("ECommerce.Contracts");
    }
}
