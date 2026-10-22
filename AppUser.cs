namespace SqlMarkdownRunner;

/// <summary>
/// Who is using the app. There is no sign-in: the app runs locally as a Windows user, so that
/// account is the answer to "who ran this".
/// </summary>
public static class AppUser
{
    public static string Name { get; } = Resolve();

    /// <summary>Safe for a file name — a domain-qualified account carries a backslash.</summary>
    public static string FileSafeName { get; } =
        string.Join("_", Resolve().Split(Path.GetInvalidFileNameChars()));

    private static string Resolve()
    {
        var user = Environment.UserName;
        var domain = Environment.UserDomainName;

        // A workgroup machine reports the machine name as the domain, which adds nothing.
        return string.IsNullOrEmpty(domain) || domain.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            ? user
            : $"{domain}\\{user}";
    }
}
