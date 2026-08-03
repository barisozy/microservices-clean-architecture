using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Catalog.Application;

public sealed record GetCategoriesQuery : IRequest<IReadOnlyList<Category>>;
public sealed class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>;
public sealed class GetCategoriesQueryHandler(ICatalogRepository repository) : IRequestHandler<GetCategoriesQuery, IReadOnlyList<Category>>
{
    public Task<IReadOnlyList<Category>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken) => repository.GetCategoriesAsync(cancellationToken);
}
