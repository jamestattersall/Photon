using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

public record WindowsAuthResult(
    bool IsAuthenticated,
    string? ResolvedUser,
    IReadOnlyList<string> Groups,
    int ErrorCode);

[SupportedOSPlatform("windows")]
public static partial class WindowsAuthHelper
{
    private const int LOGON32_LOGON_INTERACTIVE = 2;
    private const int LOGON32_LOGON_NETWORK = 3;
    private const int LOGON32_PROVIDER_DEFAULT = 0;

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LogonUserW(
        string lpszUsername,
        string? lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out IntPtr phToken);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);



public static WindowsAuthResult Authenticate(string rawUsername, string password)
    {
        if (string.IsNullOrWhiteSpace(rawUsername) || string.IsNullOrWhiteSpace(password))
        {
            return new WindowsAuthResult(false, null, Array.Empty<string>(), 1326);
        }

        string normalized = rawUsername.Trim().Replace('/', '\\');

        // 1. UPN or Microsoft Account (contains '@'): must use LogonUserW (CloudAP / Negotiate)
        if (normalized.Contains('@'))
        {
            return AuthenticateWithLogonUser(normalized, null, password);
        }

        // 2. Traditional Local (LAPTOP-1\user, .\user, user) or Domain (DOMAIN\user)
        var managedResult = AuthenticateWithPrincipalContext(normalized, password);
        if (managedResult.IsAuthenticated)
        {
            return managedResult;
        }

        // 3. Fallback to LogonUserW (handles cases where a local MSA alias or domain UPN was used)
        string username = normalized;
        string? domain = null;

        int slashIndex = normalized.IndexOf('\\');
        if (slashIndex > 0)
        {
            domain = normalized[..slashIndex];
            username = normalized[(slashIndex + 1)..];
        }

        return AuthenticateWithLogonUser(username, domain, password);
    }

    private static WindowsAuthResult AuthenticateWithPrincipalContext(string normalizedInput, string password)
    {
        string username = normalizedInput;
        string? domainOrMachine = null;

        int slashIndex = normalizedInput.IndexOf('\\');
        if (slashIndex > 0)
        {
            domainOrMachine = normalizedInput[..slashIndex];
            username = normalizedInput[(slashIndex + 1)..];
        }

        bool isExplicitLocal = !string.IsNullOrEmpty(domainOrMachine) &&
            (domainOrMachine.Equals(".", StringComparison.OrdinalIgnoreCase) ||
             domainOrMachine.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase));

        // Try local machine if explicitly specified or if no domain prefix was given
        if (isExplicitLocal || string.IsNullOrEmpty(domainOrMachine))
        {
            var localResult = TryValidateContext(ContextType.Machine, null, username, password, Environment.MachineName);
            if (localResult.IsAuthenticated || isExplicitLocal)
            {
                return localResult;
            }
        }

        // Try Active Directory domain (either explicit DOMAIN\user or plain username fallback)
        string? targetDomain = string.IsNullOrEmpty(domainOrMachine) ? null : domainOrMachine;
        return TryValidateContext(ContextType.Domain, targetDomain, username, password, targetDomain ?? Environment.UserDomainName);
    }

    private static WindowsAuthResult TryValidateContext(
        ContextType contextType,
        string? domainName,
        string username,
        string password,
        string displayPrefix)
    {
        try
        {
            using var context = domainName is null
                ? new PrincipalContext(contextType)
                : new PrincipalContext(contextType, domainName);

            if (!context.ValidateCredentials(username, password))
            {
                return new WindowsAuthResult(false, null, Array.Empty<string>(), 1326);
            }

            using var userPrincipal = UserPrincipal.FindByIdentity(context, username);
            var groups = new List<string>();

            if (userPrincipal is not null)
            {
                foreach (Principal group in userPrincipal.GetAuthorizationGroups())
                {
                    using (group)
                    {
                        try
                        {
                            var ntAccount = (NTAccount)group.Sid.Translate(typeof(NTAccount));
                            groups.Add(ntAccount.Value);
                        }
                        catch (IdentityNotMappedException)
                        {
                            groups.Add(group.Name);
                        }
                    }
                }
            }

            return new WindowsAuthResult(true, $"{displayPrefix}\\{username}", groups, 0);
        }
        catch
        {
            return new WindowsAuthResult(false, null, Array.Empty<string>(), 1326);
        }
    }

    private static WindowsAuthResult AuthenticateWithLogonUser(string username, string? domain, string password)
    {
        bool isValid = LogonUserW(
            username,
            domain,
            password,
            LOGON32_LOGON_INTERACTIVE,
            LOGON32_PROVIDER_DEFAULT,
            out IntPtr tokenHandle);

        int errorCode = isValid ? 0 : Marshal.GetLastPInvokeError();

        // Fallback to Network Logon if GPO blocks interactive logon (ERROR_LOGON_TYPE_NOT_GRANTED = 1385)
        if (!isValid && errorCode == 1385)
        {
            isValid = LogonUserW(
                username,
                domain,
                password,
                LOGON32_LOGON_NETWORK,
                LOGON32_PROVIDER_DEFAULT,
                out tokenHandle);

            errorCode = isValid ? 0 : Marshal.GetLastPInvokeError();
        }

        if (!isValid || tokenHandle == IntPtr.Zero)
        {
            return new WindowsAuthResult(false, null, Array.Empty<string>(), errorCode);
        }

        try
        {
            using var identity = new WindowsIdentity(tokenHandle);
            var groupNames = new List<string>();

            if (identity.Groups is not null)
            {
                foreach (IdentityReference sid in identity.Groups)
                {
                    if (sid.Value.StartsWith("S-1-5-5-", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    try
                    {
                        var ntAccount = (NTAccount)sid.Translate(typeof(NTAccount));
                        groupNames.Add(ntAccount.Value);
                    }
                    catch (IdentityNotMappedException)
                    {
                        // Ignore unmapped capability SIDs
                    }
                }
            }

            return new WindowsAuthResult(true, identity.Name, groupNames, 0);
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }
}
