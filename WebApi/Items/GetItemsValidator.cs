using FastEndpoints;
using FluentValidation;

namespace WebApi.Items;

public sealed class GetItemsValidator : Validator<GetItemsRequest>
{
    public GetItemsValidator()
    {
        RuleFor(request => request.Limit).InclusiveBetween(0, 10_000);
    }
}
