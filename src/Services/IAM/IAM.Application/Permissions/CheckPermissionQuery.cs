using FluentValidation;
using MediatR;

namespace IAM.Application;

public sealed record CheckPermissionQuery(string Subject, string Permission) : IRequest<PermissionResult>;
public sealed record PermissionResult(bool Allowed, string Role);
public sealed class CheckPermissionQueryValidator : AbstractValidator<CheckPermissionQuery>
{
    public CheckPermissionQueryValidator()
    {
        RuleFor(request => request.Subject).NotEmpty();
        RuleFor(request => request.Permission).NotEmpty().MaximumLength(200);
    }
}
public sealed class CheckPermissionQueryHandler(IPermissionEvaluator evaluator) : IRequestHandler<CheckPermissionQuery, PermissionResult>
{
    public Task<PermissionResult> Handle(CheckPermissionQuery request, CancellationToken cancellationToken) => evaluator.CheckAsync(request.Subject, request.Permission, cancellationToken);
}
