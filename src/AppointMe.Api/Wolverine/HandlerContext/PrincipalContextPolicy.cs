using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace AppointMe.Api.Wolverine.HandlerContext;

public class PrincipalContextPolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(chain => chain.Uses<Principal>()))
        {
            chain.Middleware.Add(new MethodCall(typeof(PrincipalContextBehavior),
                nameof(PrincipalContextBehavior.LoadAsync)));
        }
    }
}
