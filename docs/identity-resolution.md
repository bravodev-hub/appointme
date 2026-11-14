# How does your domain know your user?

Almost every business application needs to answer a question that sounds simple: who is the current user? But "user" isn't one thing. Authorization needs to know *what you're allowed to do*. Business rules need to know *who you are*. Audit trails need to know *who did this*. These are three different questions that should not be mixed into a single "user" object.

So it helps to split the concept into two abstractions.

**Identity — who are you?**

```csharp
public union Identity(AnonymousIdentity, SystemIdentity, UserIdentity);
```

A C# 15 union is a closed set of case types: any of the three records converts implicitly to `Identity`, and the compiler knows the full set, so a `switch` over an identity needs no default arm.

It might be a registered user:

```csharp
public sealed record UserIdentity(UserId Id, PersonName Name, Email Email);
```

the system itself — a background job, or a handler reacting to an event from another bounded context:

```csharp
public sealed record SystemIdentity;
```

or anonymous:

```csharp
public sealed record AnonymousIdentity;
```

**Principal — what are you allowed to do?**

```csharp
public interface IPrincipal
{
    public bool HasRole(Role role);
    public bool HasPermission(Permission permission);
}
```

A registered user has roles and permissions granted within a company:

```csharp
public sealed record UserPrincipal(CompanyId CompanyId, IReadOnlySet<Role> Roles, IReadOnlySet<Permission> Permissions)
    : IPrincipal
{
    public bool HasRole(Role role) => Roles.Contains(role);
    public bool HasPermission(Permission permission) => Permissions.Contains(permission);
}
```

The system can do anything:

```csharp
public sealed record SystemPrincipal : IPrincipal
{
    public bool HasRole(Role role) => true;
    public bool HasPermission(Permission permission) => true;
}
```

and an anonymous caller can do nothing:

```csharp
public sealed record AnonymousPrincipal : IPrincipal
{
    public bool HasRole(Role role) => false;
    public bool HasPermission(Permission permission) => false;
}
```

This is a simple model, but it's a powerful starting point for writing business logic. The following example is a `DeleteEmployee` command handler that uses both abstractions:

```csharp
public sealed class DeleteEmployeeCommandHandler(OrganizationsDbContext dbContext)
{
    public async Task HandleAsync(DeleteEmployeeCommand command, CompanyId companyId, IPrincipal principal,
        Identity identity, CancellationToken cancellationToken)
    {
        principal.Require(EmployeePermissions.Remove);

        var employee = await dbContext.Employees.LoadAsync(new EmployeeId(command.Id), companyId, cancellationToken);
        if (identity is UserIdentity currentUser && employee.UserId == currentUser.Id)
        {
            throw new ValidationException("You cannot remove yourself from the company.");
        }

        employee.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

The principal guarantees the authorization check. The identity lets us enforce a business rule the principal can't express — you can't delete yourself from the company. Two questions, two abstractions, both answered cleanly inside the handler.

Notice that the handler simply *receives* `IPrincipal` and `Identity` as parameters. It doesn't reach into `HttpContext`, doesn't call a static accessor, doesn't know where the user came from. That's deliberate — and it's also where the hard part begins.

Because the handler takes identity and principal for granted. Something, somewhere, has to actually produce them. And in a real system, the same handler can run in any of several contexts:

- an HTTP request from a browser or API client,
- a command or query dispatched through Wolverine,
- a background job,
- an asynchronous handler reacting to a domain event from another module.

Each of these knows about "the current user" in a completely different way — or doesn't know at all.

It gets worse from there, in three specific ways.

**Some requests have no user, by design.** Public endpoints are anonymous on purpose. For those, resolving identity and principal isn't just unnecessary — it's wasted database work we want to skip entirely. So resolution has to be *lazy*: nothing should hit the database to figure out who you are unless someone actually asks.

**Some callers must run as the system, with full rights.** A background job operating on behalf of the platform isn't a user at all. We need a way to explicitly say "this runs as the system" and have it resolve to full access.

**And then there's the constraint — module boundaries.** Modules are isolated — the Booking module that needs to know the current principal can't just reach into the Organizations module's tables. Fine, we accept a query across the boundary. But if you're using Wolverine with an outbox and automatic transactions, you hit another problem: Wolverine starts the transaction automatically, and it won't know *which* `DbContext` to open it on if resolution touches a different module's context mid-handler. You *can* tell Wolverine which context to use for the transaction, but there's a cleaner way out — resolution simply *cannot* happen inside the command or query handler. It has to happen before the handler's transaction is ever opened.

That last constraint is the one that shapes everything. It means the clean handler signature you saw above — `IPrincipal principal, Identity identity` as plain parameters — is only possible if some layer *above* the handler has already resolved them, lazily, with the right answer for whichever of the four contexts we happen to be in, and made them available without the handler knowing any of it happened.

So how do you build that layer?

That's what the next part is about: the resolvers, the two kinds of laziness that make it cheap, and the Wolverine handler policies, ASP.NET Core auth policies, and job filters that wire it into every entry point — without a single handler ever knowing the difference.

---

# Part 2: Two resolvers, four entry points

In part 1 we established the shape of the problem: handlers want `Identity` and `IPrincipal` handed to them as plain parameters, but something above the handler has to produce those — lazily, correctly, for whichever execution context we're in, and *before* any transaction opens. Let's build that layer.

## The resolvers

At the center are two resolvers with an identical shape: check a cache, resolve if empty, store the result back.

```csharp
public sealed class CurrentIdentityResolver(
    ICurrentIdentity currentIdentity,
    HttpIdentityFactory httpIdentityFactory
) : IIdentityResolver
{
    public async ValueTask<Identity> Resolve(CancellationToken cancellationToken)
    {
        if (currentIdentity.Identity is { } resolved)
        {
            return resolved;
        }

        var identity = await httpIdentityFactory.Create(cancellationToken);
        currentIdentity.Change(identity);
        return identity;
    }
}
```

`ICurrentIdentity` is an *ambient* holder — a singleton backed by `AsyncLocal`, so its value flows with the current async context (an HTTP request, a background job, a message handler) rather than living in a DI scope. Its `Change` method returns an `IDisposable` that restores the previous value, which is what makes nested contexts safe. The first call within a context resolves identity and caches it there. The principal resolver layers on top, and this is where the execution contexts converge into a single decision:

```csharp
public sealed class CurrentPrincipalResolver(
    IIdentityResolver identityResolver,
    ICurrentPrincipal currentPrincipal,
    UserPrincipalFactory userPrincipalFactory
) : ICurrentPrincipalResolver
{
    public async ValueTask<IPrincipal> Resolve(CancellationToken cancellationToken)
    {
        if (currentPrincipal.Principal is { } resolved)
        {
            return resolved;
        }

        var identity = await identityResolver.Resolve(cancellationToken);

        IPrincipal principal = identity switch
        {
            AnonymousIdentity => new AnonymousPrincipal(),
            SystemIdentity => new SystemPrincipal(),
            UserIdentity user => await userPrincipalFactory.Create(user, cancellationToken),
        };

        currentPrincipal.Change(principal);
        return principal;
    }
}
```

Look at the `switch`. This is where the "don't waste a database query" requirement is enforced: an anonymous caller and a system caller get their principals constructed in memory, instantly. Only a real `UserIdentity` triggers `userPrincipalFactory.Create` — the one path that actually touches the database.

## Two kinds of laziness

There are actually two distinct mechanisms keeping this cheap, and it's worth separating them:

**Runtime memoization** — the cache-check-then-store you just saw. Within one async context (a request, a job, or a message handler), identity and principal are resolved at most once, no matter how many times they're asked for.

**Bootstrap-time selectivity** — we'll see this in a moment with the Wolverine policy: handlers that don't *declare* a need for identity never get the resolution middleware wired in at all. The cheapest query is the one that's never scheduled.

## Turning an external `ClaimsPrincipal` into an internal identity

On the HTTP path, identity is built from the incoming `ClaimsPrincipal`:

```csharp
public sealed class HttpIdentityFactory(
    IHttpContextAccessor httpContextAccessor,
    IUserIdentityRegistry userIdentityRegistry
)
{
    public async ValueTask<Identity> Create(CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return new AnonymousIdentity();
        }

        var authenticatedUser = principal.ToAuthenticatedUser();
        return await userIdentityRegistry.GetOrRegister(authenticatedUser, cancellationToken);
    }
}
```

Two things worth noting. `ToAuthenticatedUser()` reads the provider's normalized claims to collect the information needed to identify or create the user object. `GetOrRegister` is lazy provisioning: the first time an authenticated external user appears, they're registered in our system; thereafter they're fetched. There's no separate sign-up webhook or eager provisioning step — identity materializes on first authenticated request.

## Where the principal actually comes from

```csharp
public class UserPrincipalFactory(
    OrganizationsDbContext dbContext,
    ICurrentCompany currentCompany,
    PermissionResolver permissionResolver,
    RolePermissionOverridesCache permissionOverridesCache)
{
    public async ValueTask<UserPrincipal> Create(UserIdentity user, CancellationToken cancellationToken)
    {
        var companyId = currentCompany.CompanyId
                        ?? throw new AccessDeniedException("Active company was not specified.");

        var roles = await dbContext.Employees
            .Where(employee => employee.UserId == user.Id)
            .Select(employee => employee.Roles)
            .SingleOrDefaultAsync(cancellationToken);

        if (roles is null || roles.Count == 0)
        {
            throw new AccessDeniedException("User is not authorized to access requested company.");
        }

        var permissionOverrides = await permissionOverridesCache.GetAsync(companyId, cancellationToken);

        var userRoles = roles.ToHashSet();
        return new UserPrincipal(
            CompanyId: companyId,
            Roles: userRoles,
            Permissions: permissionResolver.Resolve(userRoles, permissionOverrides));
    }
}
```

The role-to-permission resolution and the override model (`PermissionResolver`, `RolePermissionOverridesCache`) are a topic of their own — we'll come back to them in a later article. What matters *here* is one line of dependencies: this factory queries `OrganizationsDbContext`. Principal resolution touches a specific module's database.

## Why none of this can live in the handler

If principal resolution runs a query against `OrganizationsDbContext`, and our command handler is busy mutating, say, the Booking module's data inside a Wolverine-managed automatic transaction — Wolverine has to decide which `DbContext` to open the transaction on. Resolving the principal mid-handler, against a *different* module's context, puts that decision in an impossible spot.

The way out is: **resolution must finish before the handler's transaction begins.** By the time a handler runs, identity and principal are already resolved and sitting in the ambient holder. The handler just receives them as parameters — which is exactly the clean signature we started with in part 1.

So resolution gets wired into each entry point, *ahead* of the handler. Four contexts, four native framework seams, one ambient holder behind them all.

## Seam 1 — HTTP, via an authorization handler

```csharp
public sealed class RegisteredUserAuthorizationHandler(
    IIdentityResolver identityResolver
) : AuthorizationHandler<RegisteredUserRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        RegisteredUserRequirement requirement)
    {
        var identity = await identityResolver.Resolve(CancellationToken.None);

        if (identity is UserIdentity)
        {
            context.Succeed(requirement);
        }
    }
}
```

This plugs straight into ASP.NET Core's authorization pipeline. Only endpoints carrying `RegisteredUserRequirement` invoke it — anonymous endpoints don't, so resolution never runs for them. When it does run, the resolver lazily builds identity from the HTTP context. This is the **pull** model: resolution happens on demand.

Note that this handler only resolves *identity*, and only to **gate** the endpoint — it answers "is this a registered user?" before the request is allowed through. The *principal* a handler later needs isn't produced here. An HTTP endpoint dispatches a command into Wolverine, and the principal is resolved there, by the policy in Seam 2. HTTP gating and principal production aren't competing entry points; they compose.

## Seam 2 — Wolverine, via a handler policy

```csharp
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

public static class IdentityContextBehavior
{
    public static async Task<Identity> Load(IIdentityResolver identityResolver,
        CancellationToken cancellationToken)
    {
        return await identityResolver.Resolve(cancellationToken);
    }
}
```

This is the bootstrap-time selectivity from earlier. At code-generation time, Wolverine inspects every handler chain; only the ones whose handler actually *uses* `Identity` get the `Load` middleware attached. Wolverine threads the middleware's return value down the chain, so the handler can take `Identity` as a parameter and it's simply there. Handlers that don't need identity pay nothing — no middleware, no resolution. Like HTTP, this is **pull**: the middleware calls the resolver on demand.

Identity is only half of what `DeleteEmployeeCommandHandler` asked for back in part 1 — it also takes `IPrincipal`. That's wired by a second, near-identical policy:

```csharp
public class PrincipalContextPolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(chain => chain.Uses<IPrincipal>()))
        {
            chain.Middleware.Add(new MethodCall(typeof(PrincipalContextBehavior),
                nameof(PrincipalContextBehavior.LoadAsync)));
        }
    }
}

public static class PrincipalContextBehavior
{
    public static async Task<IPrincipal> LoadAsync(ICurrentPrincipalResolver principalResolver,
        CancellationToken cancellationToken)
    {
        return await principalResolver.Resolve(cancellationToken);
    }
}
```

The shape is identical; only two things change. The selection predicate is `chain.Uses<IPrincipal>()` instead of `Identity`, and the behavior delegates to the `CurrentPrincipalResolver` we built at the start of this part — which is what finally gives that resolver its entry point. The two policies are independent and registered side by side, so a handler gets exactly the middleware it declares: one asking only for `Identity` gets the identity `Load`; one asking for both — like `DeleteEmployeeCommandHandler` — gets both. And because principal resolution calls into identity resolution underneath (the `switch` we saw earlier), the two compose: resolving the principal lazily resolves the identity too, each memoized once.

This is the "magic" part of Wolverine in both good and bad senses.

## Seam 3 — background jobs, via a Hangfire filter

Jobs in our case simply need to run under System:

```csharp
public sealed class SystemContextJobFilter(ICurrentIdentity currentIdentity, ICurrentPrincipal currentPrincipal)
    : IServerFilter
{
    private const string StateKey = "SystemContextJobFilter.State";

    public void OnPerforming(PerformingContext context)
    {
        var identityScope = currentIdentity.Change(new SystemIdentity());
        var principalScope = currentPrincipal.Change(new SystemPrincipal());

        context.Items[StateKey] = new State(identityScope, principalScope);
    }

    public void OnPerformed(PerformedContext context)
    {
        if (!context.Items.TryGetValue(StateKey, out var value) || value is not State state)
        {
            return;
        }

        state.PrincipalScope.Dispose();
        state.IdentityScope.Dispose();
    }

    private sealed record State(IDisposable IdentityScope, IDisposable PrincipalScope);
}
```

A job has no user and no `HttpContext`. So instead of resolving, the filter **pushes** `SystemIdentity` and `SystemPrincipal` into the holder *before* the job body runs. When a resolver's cache-check runs inside the job, the holder is already populated, so it returns the system context immediately — it never touches `HttpIdentityFactory`, never hits the database. The job runs as the system with full rights.

## Seam 4 — domain event handlers, via a handler policy

There's a fourth context hiding in the async path. When a handler reacts to a domain event raised by *another* module, there's no user behind it — the originating action already happened, possibly in a different request. These handlers should run as the system.

The same Wolverine policy mechanism from Seam 2 applies, but with a different selection rule and the opposite strategy. Instead of selecting handlers that *use* `Identity`, we select handlers whose message *is* a domain event, and instead of pulling, we push:

```csharp
public class DomainEventContextPolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains
                     .Where(handlerChain => handlerChain.MessageType.IsAssignableTo(typeof(IDomainEvent))))
        {
            chain.Middleware.Insert(0, new MethodCall(typeof(SystemContextBehavior),
                nameof(SystemContextBehavior.Apply)));
        }
    }
}
```

Note `Insert(0, ...)` rather than `Add`: the system context is pushed in at the very front of the chain, so the handler — and any other middleware — sees the system identity already in place. It's the same **push** strategy as the Hangfire filter, expressed through Wolverine's policy system: a domain-event handler runs as `SystemIdentity` / `SystemPrincipal`, with no resolution and no database hit.

The two pushes differ in one detail of housekeeping. The Hangfire filter keeps the `IDisposable` that `Change` hands back and disposes it in `OnPerformed`, restoring whatever the holder contained before the job. `SystemContextBehavior.Apply` doesn't — it pushes the system context and walks away, relying on the message's async context ending with the handler. Both are correct for their setting; just don't read them as byte-for-byte identical.

So the same Wolverine mechanism serves two of our contexts in opposite ways. Command handlers that declare `Identity` get the resolver wired in and **pull** lazily. Domain-event handlers get the system context **pushed** in up front. The selection criterion — "uses `Identity`" versus "is an `IDomainEvent`" — decides which.

## The shape of the whole thing

Step back and the design is one idea: **a single ambient holder that every context populates differently.** HTTP requests and command handlers *pull* — resolution happens lazily, on demand, when a handler or an authorization requirement asks. Background jobs and domain-event handlers *push* — the system context is seeded up front because there's no user to resolve. The holder is the same in every case; only *who fills it, and when* changes.

The handler, at the end of all this, knows none of it. It declares `IPrincipal` and `Identity` as parameters and trusts that they're correct — whether the caller was a browser, an API client, an event from another module, or a 3 a.m. cron job.

## The hard feeling

The design is clean, but it has one real cost, and it's the same constraint that created it: resolution lives *outside* the handler because of the Wolverine auto-transaction / cross-module `DbContext` problem. That means the rule "resolve before the transaction opens" is enforced by convention and by the four seams being wired correctly — not by the compiler. Add a fifth entry point and forget to wire the seam, and a handler could run with an unresolved principal. There's no type-level guarantee; there's discipline and tests.

Which raises a question I've been circling the whole time I've worked on this: what if the handler just took the current user as an explicit argument that the *caller* had to supply — no ambient holder, no middleware, no seams at all? That's a genuinely different philosophy, with a different set of tradeoffs, and I've built systems both ways. It's also the approach I'm planning to use for AppointMe's upcoming billing module — so the same codebase will soon demonstrate both. That's where this goes next.
