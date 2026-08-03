using FluentValidation;
using MediatR;

namespace Search.Application;

public sealed record SuggestQuery(string Query) : IRequest<IReadOnlyList<string>>;
public sealed class SuggestQueryValidator : AbstractValidator<SuggestQuery>
{
    public SuggestQueryValidator() => RuleFor(request => request.Query).NotEmpty().MaximumLength(200);
}
public sealed class SuggestQueryHandler(ISearchReadRepository repository) : IRequestHandler<SuggestQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(SuggestQuery request, CancellationToken cancellationToken) => repository.SuggestAsync(request.Query, cancellationToken);
}
