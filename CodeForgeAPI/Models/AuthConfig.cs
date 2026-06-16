namespace CodeForgeAPI.Models;

/// <summary>Per-HTTP-method protection flags for a single entity</summary>
public class EntityProtectionMethods
{
    public bool Get    { get; set; } = false;
    public bool Post   { get; set; } = false;
    public bool Put    { get; set; } = false;
    public bool Patch  { get; set; } = false;
    public bool Delete { get; set; } = false;

    /// <summary>Returns true if at least one method is protected</summary>
    public bool AnyProtected => Get || Post || Put || Patch || Delete;
}

/// <summary>Authentication generation settings stored as JSON in Project.AuthConfig</summary>
public class AuthConfig
{
    public bool Enabled { get; set; } = false;

    /// <summary>JWT or Session</summary>
    public string Type { get; set; } = "JWT";

    /// <summary>email / username / both</summary>
    public string UserIdentifier { get; set; } = "email";

    public bool EnableRoles { get; set; } = false;

    /// <summary>Default roles list when EnableRoles is true</summary>
    public List<string> Roles { get; set; } = new() { "User", "Admin" };

    public bool EnableRefreshTokens { get; set; } = true;

    /// <summary>Access token lifetime in minutes</summary>
    public int TokenExpiryMinutes { get; set; } = 60;

    /// <summary>Refresh token lifetime in days</summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;

    public bool EnableEmailVerification { get; set; } = false;

    /// <summary>
    /// Per-entity HTTP method protection.
    /// Key = entity name, Value = which methods require [Authorize].
    /// Replaces the old ProtectAllRoutes boolean for finer control.
    /// </summary>
    public Dictionary<string, EntityProtectionMethods> EntityProtection { get; set; } = new();

    /// <summary>Legacy fallback: if EntityProtection is empty, protect all methods on all entities</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ProtectAllRoutes => EntityProtection.Count == 0;
}
