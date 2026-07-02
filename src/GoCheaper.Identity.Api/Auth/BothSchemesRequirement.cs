using Microsoft.AspNetCore.Authorization;

namespace GoCheaper.Identity.Api.Auth;

public class BothSchemesRequirement : IAuthorizationRequirement { }

public class BothSchemesHandler : AuthorizationHandler<BothSchemesRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, BothSchemesRequirement requirement)
    {
        var hasApiKey = context.User.Identities
            .Any(i => i.AuthenticationType == "ApiKey" && i.IsAuthenticated);
        var hasJwt = context.User.Identities
            .Any(i => i.AuthenticationType == "Bearer" && i.IsAuthenticated);

        if (hasApiKey && hasJwt)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
