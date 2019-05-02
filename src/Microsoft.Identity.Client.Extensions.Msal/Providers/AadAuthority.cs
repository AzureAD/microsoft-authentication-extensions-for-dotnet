namespace Microsoft.Identity.Client.Extensions.Msal.Providers
{
    internal static class AadAuthority
    {
        public const string DefaultTrustedHost = "login.microsoftonline.com";
        public const string AadCanonicalAuthorityTemplate = "https://{0}/{1}/";
    }
}