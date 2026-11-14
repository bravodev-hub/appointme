using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace AppointMe.Api.Wolverine.HandlerContext;

using Identity = AppointMe.Shared.Authentication.Identity;

public class IdentityContextPolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(chain => chain.Uses<Identity>()))
        {
            chain.Middleware.Add(
                new MethodCall(typeof(IdentityContextBehavior), nameof(IdentityContextBehavior.Load)));
        }
    }
}
