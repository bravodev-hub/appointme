using System.IdentityModel.Tokens.Jwt;
using AppointMe.Api.Authentication.EntraExternalId;
using AppointMe.Api.Authentication.Keycloak;
using AppointMe.Api.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AppointMe.Api.Authentication;

internal static class AuthenticationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppointMeAuthentication(IConfiguration configuration)
        {
            var providerName = configuration["Authentication:Provider"] ?? "Keycloak";
            switch (providerName)
            {
                case "Keycloak":
                    services.AddKeycloakAuthOptions(configuration);
                    break;
                case "EntraExternalId":
                    services.AddEntraExternalIdAuthOptions(configuration);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown Authentication:Provider '{providerName}'. Expected 'Keycloak' or 'EntraExternalId'.");
            }

            // Super-admin membership is sourced from config so it works across providers.
            // Enforced via SuperAdminAuthorizationHandler (not a claims transformer) to avoid
            // shadowing the single provider IClaimsTransformation that normalizes identity claims.
            var superAdminEmails = configuration.GetSection(SuperAdmin.ConfigurationSection).Get<string[]>() ?? [];
            services.AddSingleton(new SuperAdminRegistry(superAdminEmails));

            var requireHttpsMetadata = configuration.GetValue("Authentication:RequireHttpsMetadata", false);

            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = HybridAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddPolicyScheme(HybridAuthenticationDefaults.AuthenticationScheme, "Cookie or Bearer", options =>
                {
                    options.ForwardDefaultSelector = context => context.Request.HasBearerTokenHeader()
                        ? JwtBearerDefaults.AuthenticationScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = "appointme.auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                })
                .AddOpenIdConnect()
                .AddJwtBearer();

            services
                .AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
                .Configure<IdentityProviderOptions>((oidc, provider) =>
                {
                    oidc.Authority = provider.Authority;
                    oidc.ClientId = provider.OidcClientId;
                    oidc.ClientSecret = provider.OidcClientSecret;
                    oidc.ResponseType = OpenIdConnectResponseType.Code;
                    oidc.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    oidc.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Name;

                    oidc.Scope.Add("openid");
                    oidc.Scope.Add("profile");
                    oidc.Scope.Add("email");

                    oidc.GetClaimsFromUserInfoEndpoint = true;
                    oidc.CallbackPath = "/signin-oidc";

                    oidc.Events = new OpenIdConnectEvents
                    {
                        OnRedirectToIdentityProvider = context =>
                        {
                            if (context.Request.HasBearerTokenHeader())
                            {
                                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                context.HandleResponse();
                            }

                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            // Persist only the id_token (logout sends it as id_token_hint). SaveTokens would
                            // also put the access and refresh tokens into the auth cookie, pushing localhost
                            // request headers past Keycloak's 8KB limit.
                            if (context.TokenEndpointResponse?.IdToken is { Length: > 0 } idToken)
                            {
                                context.Properties?.StoreTokens(
                                [
                                    new AuthenticationToken
                                    {
                                        Name = OpenIdConnectParameterNames.IdToken,
                                        Value = idToken
                                    }
                                ]);
                            }

                            return Task.CompletedTask;
                        }
                    };

                    oidc.RequireHttpsMetadata = requireHttpsMetadata;
                    oidc.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

                    provider.CustomizeOidc?.Invoke(oidc);
                });

            services
                .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IdentityProviderOptions>((jwt, provider) =>
                {
                    jwt.Authority = provider.Authority;
                    jwt.Audience = provider.ApiAudience;
                    jwt.RequireHttpsMetadata = requireHttpsMetadata;

                    jwt.Events = new JwtBearerEvents
                    {
                        OnChallenge = context =>
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }
                    };
                });

            return services
                .AddHttpContextAccessor()
                .AddSingleton<ICurrentIdentity, CurrentIdentity>()
                .AddScoped<HttpIdentityFactory>()
                .AddScoped<IIdentityResolver, CurrentIdentityResolver>();
        }
    }
}
