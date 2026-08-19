using Microsoft.Extensions.Configuration;

namespace ForgeMission.Orchestration;

// Where the durable Conversation Runtime lives, and whether that is the local development default —
// the only case in which the Supervisor may start a loopback tunnel of its own.
internal readonly record struct ConversationRuntimeEndpoint(string BaseUrl, bool IsLocalDefault);

// Selects exactly one durable Conversation Runtime base URL: the already-existing configuration
// value when it is usable, otherwise the single local development default. It owns no HTTP call and
// no process — readiness belongs to the probe, lifetime to the tunnel.
internal static class ConversationRuntimeResolver
{
    internal const string ConfigurationKey = "ConversationRuntime:BaseUrl";
    internal const string DefaultLocalBaseUrl = "http://127.0.0.1:18080/";

    public static ConversationRuntimeEndpoint Resolve(IConfiguration configuration)
    {
        var configured = configuration[ConfigurationKey];
        return string.IsNullOrWhiteSpace(configured)
            ? new ConversationRuntimeEndpoint(DefaultLocalBaseUrl, IsLocalDefault: true)
            : new ConversationRuntimeEndpoint(Normalize(configured.Trim()), IsLocalDefault: false);
    }

    // A relative or non-HTTP(S) override can never become the client's BaseAddress. Failing here
    // names the configuration key, instead of surfacing later as an invalid-URI error inside the
    // first Janus send. The trailing slash is what keeps the client's relative routes intact.
    private static string Normalize(string configured)
    {
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} must be an absolute http or https URL. Configured value: '{configured}'.");
        }

        var absolute = uri.ToString();
        return absolute.EndsWith('/') ? absolute : absolute + "/";
    }
}
