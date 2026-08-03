using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Catalog.Application;

public sealed record GetBrandsQuery : IRequest<IReadOnlyList<Brand>>;
public sealed class GetBrandsQueryValidator : AbstractValidator<GetBrandsQuery>;
public sealed class GetBrandsQueryHandler(ICatalogRepository repository) : IRequestHandler<GetBrandsQuery, IReadOnlyList<Brand>>
{
    public Task<IReadOnlyList<Brand>> Handle(GetBrandsQuery request, CancellationToken cancellationToken) => repository.GetBrandsAsync(cancellationToken);
}
