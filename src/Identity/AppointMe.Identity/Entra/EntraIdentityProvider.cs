using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace AppointMe.Identity.Entra;

public sealed partial class EntraIdentityProvider(
    IOptions<EntraIdentityOptions> options,
    ILogger<EntraIdentityProvider> logger
) : IIdentityProvider
{
    private readonly GraphServiceClient _graph = CreateGraphClient(options.Value);

    public Task<IdentityProviderUserId> CreateUserWithPasswordSetup(string email, PersonName? name,
        string redirectUri, CancellationToken cancellationToken)
    {
        return SendInvitation(email, name?.FullName, redirectUri, cancellationToken);
    }

    public async Task ResendInvitationEmail(string email, string redirectUri, CancellationToken cancellationToken)
    {
        await SendInvitation(email, displayName: null, redirectUri, cancellationToken);
    }

    private async Task<IdentityProviderUserId> SendInvitation(string email, string? displayName, string redirectUri,
        CancellationToken cancellationToken)
    {
        try
        {
            var invitation = await _graph.Invitations.PostAsync(new Invitation
                             {
                                 InvitedUserEmailAddress = email,
                                 InvitedUserDisplayName = displayName,
                                 InviteRedirectUrl = redirectUri,
                                 SendInvitationMessage = true,
                             }, cancellationToken: cancellationToken) ??
                             throw new InvalidOperationException("Graph returned a null invitation.");

            var userId = invitation.InvitedUser?.Id
                         ?? throw new InvalidOperationException(
                             "Invitation response did not include the invited user id.");
            return new IdentityProviderUserId(userId);
        }
        catch (ODataError ex) when (IsAmbiguousBadRequest(ex))
        {
            LogGraphError(ex, "Invitation rejected with BadRequest — disambiguating existing user vs invalid email");
            var existingId = await FindUserIdByEmail(email, cancellationToken);
            if (existingId is not null)
            {
                return new IdentityProviderUserId(existingId);
            }

            throw new ValidationException(
                $"The email address '{email}' was rejected by the identity provider. Some addresses with special characters (most commonly '+' aliases) aren't supported by Entra invitations. Please use a different email.",
                code: "invalid_email_for_idp");
        }
        catch (ODataError ex)
        {
            LogGraphError(ex, "Unhandled Graph error in POST /invitations");
            throw;
        }
    }

    private async Task<string?> FindUserIdByEmail(string email, CancellationToken cancellationToken)
    {
        var byMail = await _graph.Users.GetAsync(request =>
        {
            request.QueryParameters.Filter = $"mail eq '{email}'";
            request.QueryParameters.Select = ["id"];
            request.QueryParameters.Top = 1;
        }, cancellationToken);
        return byMail?.Value?.FirstOrDefault()?.Id;
    }

    private void LogGraphError(ODataError error, string context)
    {
        LogGraphErrorDetails(
            error,
            context,
            error.Error?.Code,
            error.Error?.Message,
            ReadAdditional(error, "code"),
            ReadAdditional(error, "message"));
    }

    private static string? ReadAdditional(ODataError error, string key)
    {
        var data = error.Error?.InnerError?.AdditionalData;
        return data is not null && data.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static bool IsAmbiguousBadRequest(ODataError error)
    {
        // These messages can mean either "existing tenant member" OR "email format rejected".
        // The caller must look the user up to decide which.
        var message = error.Error?.Message ?? "";
        return message.Contains("Primary SMTP Address is an invalid", StringComparison.OrdinalIgnoreCase)
               || message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
    }

    private static GraphServiceClient CreateGraphClient(EntraIdentityOptions options)
    {
        var credential = new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);
        return new GraphServiceClient(credential, scopes: ["https://graph.microsoft.com/.default"]);
    }

    [LoggerMessage(LogLevel.Error,
        "{Context}. Graph error code={Code} message={Message} innerCode={InnerCode} innerMessage={InnerMessage}")]
    private partial void LogGraphErrorDetails(Exception exception, string context, string? code, string? message,
        string? innerCode, string? innerMessage);
}
