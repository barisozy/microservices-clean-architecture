using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Catalog.Application;

public sealed record GetVariantsQuery(string Sku) : IRequest<IReadOnlyList<ProductVariant>?>;
public sealed class GetVariantsQueryValidator : AbstractValidator<GetVariantsQuery>
{
    public GetVariantsQueryValidator() => RuleFor(request => request.Sku).NotEmpty().MaximumLength(100);
}
public sealed class GetVariantsQueryHandler(ICatalogRepository repository) : IRequestHandler<GetVariantsQuery, IReadOnlyList<ProductVariant>?>
{
    public Task<IReadOnlyList<ProductVariant>?> Handle(GetVariantsQuery request, CancellationToken cancellationToken) => repository.GetVariantsAsync(request.Sku, cancellationToken);
}
