namespace Aenaon.Api.Authentication;

/// <summary>Σταθερές πρωτοκόλλου της προσωρινής Development ροής σύνδεσης.</summary>
public static class SessionAuthenticationOptions
{
    public const string Scheme = "AenaonERP.Session";
    public const string SecurityStampClaim = "aenaon:security_stamp";
    public const string CsrfHeader = "X-Aenaon-CSRF";
    public const string RateLimitPolicy = "auth-endpoints";
}
