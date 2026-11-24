using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;

namespace Photon; // Update to match your project namespace

public static class WindowsAuthHelper
{
 private static bool IsLocalMachine(string domain)
{
    return string.IsNullOrWhiteSpace(domain)
           || domain == "."
           || domain.Equals("localhost", StringComparison.OrdinalIgnoreCase)
           || domain.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
}

public static bool ValidateCredentials(string domain, string username, string password, out string errorMessage)
{
    errorMessage = string.Empty;

    // 1. Local Machine Account (for Visual Studio / Local Debugging)
    if (IsLocalMachine(domain))
    {
        try
        {
            using var context = new PrincipalContext(ContextType.Machine);
            bool isValid = context.ValidateCredentials(username, password);
            if (!isValid)
            {
                errorMessage = "Invalid local machine username or password.";
            }
            return isValid;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    // 2. Active Directory / PAM Account (Kerberos-safe)
    return ValidateAdCredentials(domain, username, password, out errorMessage);
}

    public static string GetClaimName(Claim claim)
    {
        string value = claim.Value;
        if (string.IsNullOrWhiteSpace(value)) return "";

        // If the value is a raw SID (e.g., "S-1-5-32-544"), translate it to an NTAccount name
        if (value.StartsWith("S-", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var sid = new SecurityIdentifier(value);
                var account = (NTAccount)sid.Translate(typeof(NTAccount));
                value = account.Value; // e.g., "BUILTIN\Administrators" or "UHB\AppAdmins"
            }
            catch (IdentityNotMappedException)
            {
                // Skip SIDs that cannot be resolved (deleted groups or unmapped capability SIDs)
                return "";
            }
            catch (SystemException)
            {
                // Skip malformed SIDs
                return "";
            }
        }

        // Strip the prefix ("BUILTIN\Administrators" -> "Administrators", "UHB\AppAdmins" -> "AppAdmins")
        return value.Contains('\\') ? value.Split('\\', 2)[1] : value;

    }
    public static List<string> GetUserRoles(string domain, string username)
{
    // 1. Local Machine Groups (for Visual Studio / Local Debugging)
    if (IsLocalMachine(domain))
    {
        var localRoles = new List<string>();
        try
        {
            using var context = new PrincipalContext(ContextType.Machine);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username);

            if (user != null)
            {
                // GetAuthorizationGroups resolves nested local groups just like tokenGroups in AD
                foreach (var group in user.GetAuthorizationGroups())
                {
                    if (!string.IsNullOrWhiteSpace(group.Name))
                    {
                        localRoles.Add(group.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve local machine groups for {Username}.", username);
        }
        return localRoles;
    }

    // 2. Active Directory / PAM Groups
    return GetUserRolesFromAd(domain, username);
}

    public static bool ValidateAdCredentialsLdaps(string domain, string username, string password, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            var dirContext = new DirectoryContext(DirectoryContextType.Domain, domain);
            using var adDomain = Domain.GetDomain(dirContext);
            using var dc = adDomain.FindDomainController();
            string dcFqdn = dc.Name;

            var identifier = new LdapDirectoryIdentifier(dcFqdn, 636, fullyQualifiedDnsHostName: true, connectionless: false);

            // Step 1: Use the IIS App Pool's gMSA (default credentials) to find the user's exact Distinguished Name (DN)
            string? userDn = null;
            using (var lookupConnection = new LdapConnection(identifier))
            {
                lookupConnection.SessionOptions.SecureSocketLayer = true;
                lookupConnection.SessionOptions.ProtocolVersion = 3;
                lookupConnection.AuthType = AuthType.Negotiate;
                lookupConnection.Bind();

                // Get defaultNamingContext
                var rootReq = new SearchRequest("", "(objectClass=*)", SearchScope.Base, "defaultNamingContext");
                var rootRes = (SearchResponse)lookupConnection.SendRequest(rootReq);
                
                string? baseDn = null;
                if (rootRes.Entries.Count > 0 && rootRes.Entries[0].Attributes.Contains("defaultNamingContext"))
                {
                    baseDn = rootRes.Entries[0].Attributes["defaultNamingContext"][0]?.ToString();
                }

                if (!string.IsNullOrWhiteSpace(baseDn))
                {
                    string safeUser = EscapeLdapFilter(username);
                    var searchReq = new SearchRequest(
                        baseDn,
                        $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={safeUser}))",
                        SearchScope.Subtree,
                        "distinguishedName"
                    );
                    var searchRes = (SearchResponse)lookupConnection.SendRequest(searchReq);
                    if (searchRes.Entries.Count > 0)
                    {
                        userDn = searchRes.Entries[0].DistinguishedName;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(userDn))
            {
                errorMessage = $"User '{username}' not found in Active Directory.";
                return false;
            }

            // Step 2: Bind over LDAPS (636) using the user's exact Distinguished Name and password
            var credential = new NetworkCredential(userDn, password);
            using var authConnection = new LdapConnection(identifier, credential, AuthType.Basic);
            authConnection.SessionOptions.SecureSocketLayer = true;
            authConnection.SessionOptions.ProtocolVersion = 3;

            authConnection.Bind();
            return true;
        }
        catch (LdapException ex)
        {
            errorMessage = $"LDAPS Error {ex.ErrorCode}: {ex.Message} | ServerDetail: {ex.ServerErrorMessage}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool ValidateAdCredentials(string domain, string username, string password, out string errorMessage)
{
    errorMessage = string.Empty;

    try
    {
        // 1. Locate a specific Domain Controller FQDN so Kerberos SPN matching succeeds
        var dirContext = new DirectoryContext(DirectoryContextType.Domain, domain);
        using var adDomain = Domain.GetDomain(dirContext);
        using var dc = adDomain.FindDomainController();
        string dcFqdn = dc.Name; // e.g., "dc01.uhb.nhs.uk"
        string dnsDomainName = adDomain.Name; // e.g., "uhb.nhs.uk"

        // 2. Pass the specific DC FQDN with fullyQualifiedDnsHostName = true
        var identifier = new LdapDirectoryIdentifier(dcFqdn, 389, fullyQualifiedDnsHostName: true, connectionless: false);
        var credential = new NetworkCredential(username, password, dnsDomainName);

        using var connection = new LdapConnection(identifier, credential, AuthType.Negotiate);
        connection.SessionOptions.Sealing = true;
        connection.SessionOptions.Signing = true;
        connection.SessionOptions.ProtocolVersion = 3;

        connection.Bind();
        return true;
    }
    catch (LdapException ex)
    {
        // Log both ErrorCode and ServerErrorMessage (contains the hex AD sub-code like 52e, 530, 531, 775)
        errorMessage = $"LDAP Error {ex.ErrorCode}: {ex.Message} | ServerDetail: {ex.ServerErrorMessage}";
        return false;
    }
    catch (Exception ex)
    {
        errorMessage = ex.Message;
        return false;
    }
}

public static ClaimsPrincipal? AuthenticateUser(string usernameInput, string password, HashSet<string> allowedRoles)
    {
        string target = Environment.UserDomainName;
        if (target.Equals("IIS APPPOOL", StringComparison.OrdinalIgnoreCase))
        {
            target = Environment.MachineName;
        }

        string username = usernameInput;
        if (username.Contains('\\'))
        {
            var parts = username.Split('\\', 2);
            target = parts[0];
            username = parts[1];
        }

        bool isLocalMachine = target.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
                              || target == ".";

        ContextType contextType = isLocalMachine ? ContextType.Machine : ContextType.Domain;

        using var context = new PrincipalContext(contextType, isLocalMachine ? null : target);

        if (!context.ValidateCredentials(username, password))
        {
            Log.Warning("Failed to validate credentials for user {Username} in context {ContextType}.", username, contextType);
            return null;
        }

        using var user = UserPrincipal.FindByIdentity(context, username);
        if (user is null)
        {
            Log.Warning("User {Username} not found in context {ContextType}.", username, contextType);
            return null;
        }
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.SamAccountName ?? username),
            new(ClaimTypes.GivenName, user.DisplayName ?? username)
        };

        using var groups = isLocalMachine ? user.GetGroups() : user.GetAuthorizationGroups();
        foreach (Principal group in groups)
        {
            using (group)
            {
                if (!string.IsNullOrEmpty(group.Name) && allowedRoles.Contains(group.Name))
                {
                    claims.Add(new Claim(ClaimTypes.Role, group.Name));
                }
            }
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }


  [SupportedOSPlatform("windows")]
    public static ClaimsPrincipal FilterRoles(
        this ClaimsPrincipal original,
        IReadOnlySet<string> allowedRoleNames)
    {
        var filteredIdentities = original.Identities.Select(identity =>
        {
            var keptClaims = new List<Claim>();

            foreach (var claim in identity.Claims)
            {
                // 1. Check if the claim is a Role or Windows Group SID
                if (claim.Type == identity.RoleClaimType || claim.Type == ClaimTypes.GroupSid)
                {
                    var groupName = ResolveSidToGroupName(claim.Value);

                    if (groupName is not null && allowedRoleNames.Contains(groupName))
                    {
                        // Store the friendly name as a standard Role claim so User.IsInRole("Administrators") works
                        keptClaims.Add(new Claim(identity.RoleClaimType, groupName));
                    }
                }
                else
                {
                    // Keep all other non-role claims (e.g., ClaimTypes.Name)
                    keptClaims.Add(claim);
                }
            }

            return new ClaimsIdentity(
                keptClaims,
                identity.AuthenticationType,
                identity.NameClaimType,
                identity.RoleClaimType);
        });

        return new ClaimsPrincipal(filteredIdentities);
    }

    [SupportedOSPlatform("windows")]
    private static string? ResolveSidToGroupName(string sidValue)
    {
        try
        {
            // Translate SID (e.g., "S-1-5-32-544") to NTAccount (e.g., "BUILTIN\Administrators")
            var sid = new SecurityIdentifier(sidValue);
            var account = (NTAccount)sid.Translate(typeof(NTAccount));

            // Strip the "DOMAIN\" or "BUILTIN\" prefix to match short names like "Administrators"
            var fullName = account.Value;
            var slashIndex = fullName.IndexOf('\\');
            return slashIndex >= 0 ? fullName[(slashIndex + 1)..] : fullName;
        }
        catch (IdentityNotMappedException)
        {
            // Orphaned or unresolvable SID in AD—ignore safely
            return null;
        }
        catch (ArgumentException)
        {
            // Value was already a friendly name rather than a SID
            return sidValue;
        }
    }


public static List<string> GetUserRolesFromAd(string domain, string username)
{
    var roles = new List<string>();

    try
    {
        // Resolve the full DNS domain name if a short NetBIOS name (e.g., "UHB") was passed
        string dnsDomain = domain.Contains('.')
            ? domain
            : Environment.GetEnvironmentVariable("USERDNSDOMAIN") ?? domain;

        var identifier = new LdapDirectoryIdentifier(dnsDomain);

        // Using default credentials runs the LDAP search under your IIS App Pool's gMSA account
        using var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Negotiate
        };

        connection.SessionOptions.Sealing = true;
        connection.SessionOptions.Signing = true;
        connection.SessionOptions.ProtocolVersion = 3;
        connection.Bind();

        // 1. Get the defaultNamingContext (e.g., "DC=uhb,DC=nhs,DC=uk") from RootDSE
        var rootRequest = new SearchRequest(
            distinguishedName: "",
            ldapFilter: "(objectClass=*)",
            SearchScope.Base,
            "defaultNamingContext"
        );
        var rootResponse = (SearchResponse)connection.SendRequest(rootRequest);
        string? baseDn = rootResponse.Entries[0].Attributes["defaultNamingContext"][0]?.ToString();

        if (string.IsNullOrWhiteSpace(baseDn))
        {
            return roles;
        }

        // 2. Find the user object and request their 'tokenGroups' (includes all nested AD groups)
        string safeUsername = EscapeLdapFilter(username);
        var userRequest = new SearchRequest(
            distinguishedName: baseDn,
            ldapFilter: $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={safeUsername}))",
            SearchScope.Subtree,
            "tokenGroups"
        );

        var userResponse = (SearchResponse)connection.SendRequest(userRequest);
        if (userResponse.Entries.Count == 0)
        {
            return roles;
        }

        var userEntry = userResponse.Entries[0];

        // 3. tokenGroups returns raw byte[] Security Identifiers (SIDs); translate them to group names
        if (userEntry.Attributes.Contains("tokenGroups"))
        {
            foreach (byte[] sidBytes in userEntry.Attributes["tokenGroups"].GetValues(typeof(byte[])))
            {
                try
                {
                    var sid = new SecurityIdentifier(sidBytes, 0);
                    var account = (NTAccount)sid.Translate(typeof(NTAccount));

                    // account.Value is formatted as "UHB\GroupName"
                    string groupName = account.Value.Contains('\\')
                        ? account.Value.Split('\\', 2)[1]
                        : account.Value;

                    roles.Add(groupName);
                }
                catch (IdentityNotMappedException)
                {
                    // Ignore unresolvable SIDs (e.g., deleted groups or local built-in SIDs)
                }
            }
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to retrieve AD groups for {Domain}\\{Username}.", domain, username);
    }

    return roles;
}

    // Prevents LDAP injection if a user types special characters into the login box
    private static string EscapeLdapFilter(string input)
    {
        return input
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }
    private static List<string> GetLocalMachineRoles(string username)
    {
        var roles = new List<string>();

        try
        {
            using var context = new PrincipalContext(ContextType.Machine);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username);

            if (user != null)
            {
                foreach (var group in user.GetGroups())
                {
                    if (!string.IsNullOrWhiteSpace(group.Name))
                    {
                        roles.Add(group.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve local machine groups for {Username}.", username);
        }

        return roles;
    }
}

