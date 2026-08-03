using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Catalog.Application;

public sealed record GetImagesQuery(string Sku) : IRequest<IReadOnlyList<ProductImage>?>;
public sealed class GetImagesQueryValidator : AbstractValidator<GetImagesQuery>
{
    public GetImagesQueryValidator() => RuleFor(request => request.Sku).NotEmpty().MaximumLength(100);
}
public sealed class GetImagesQueryHandler(ICatalogRepository repository) : IRequestHandler<GetImagesQuery, IReadOnlyList<ProductImage>?>
{
    public Task<IReadOnlyList<ProductImage>?> Handle(GetImagesQuery request, CancellationToken cancellationToken) => repository.GetImagesAsync(request.Sku, cancellationToken);
}
