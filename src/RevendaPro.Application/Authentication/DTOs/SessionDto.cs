namespace RevendaPro.Application.Authentication.DTOs
{
    /// <summary>One item of the sidebar.</summary>
    /// <param name="Key">Screen key, which is also the permission.</param>
    /// <param name="Name">Label shown to the user, in Portuguese.</param>
    /// <param name="Route">Frontend route.</param>
    /// <param name="Icon">Lucide icon name.</param>
    /// <param name="Children">Submenu items.</param>
    public sealed record MenuItemDto(
        string Key,
        string Name,
        string Route,
        string? Icon,
        IReadOnlyList<MenuItemDto> Children);

    /// <summary>A section of the sidebar.</summary>
    /// <param name="Group">Section header.</param>
    /// <param name="Items">Items inside it.</param>
    public sealed record MenuGroupDto(string Group, IReadOnlyList<MenuItemDto> Items);

    /// <summary>The authenticated user.</summary>
    /// <param name="Code">Public identifier. The internal Id is never exposed.</param>
    /// <param name="Name">Full name.</param>
    /// <param name="Email">E-mail.</param>
    /// <param name="HasPhoto">Whether there is a photo to load.</param>
    public sealed record SessionUserDto(Guid Code, string Name, string Email, bool HasPhoto);

    /// <summary>
    /// Response of GET /api/auth/me.
    ///
    /// The menu arrives already filtered and ordered by the server: the frontend never
    /// receives the full catalog to hide items on the client. Hiding a menu item is
    /// presentation; the guard on every endpoint is the security. See ADR-0002.
    /// </summary>
    /// <param name="User">Who is signed in.</param>
    /// <param name="Roles">Role names, displayed to the user.</param>
    /// <param name="Screens">Every screen key allowed, including those outside the menu.</param>
    /// <param name="Menu">The sidebar, grouped and ordered.</param>
    /// <param name="Limits">Numbers the screen has to know to refuse before asking.</param>
    public sealed record SessionDto(
        SessionUserDto User,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Screens,
        IReadOnlyList<MenuGroupDto> Menu,
        SessionLimitsDto Limits);

    /// <summary>
    /// What this installation allows, so the screen can say it before the network does.
    ///
    /// The API refuses an oversized upload on its own, and that refusal is the real guard.
    /// What travels here is the same number, so the browser stops a file that would only be
    /// rejected after megabytes of pointless upload — and, on a very large one, after the
    /// connection is reset with no readable answer at all.
    /// </summary>
    /// <param name="MaxUploadSizeInBytes">Largest file accepted, per RNF-09.</param>
    public sealed record SessionLimitsDto(long MaxUploadSizeInBytes);

    /// <summary>Tokens handed to the client.</summary>
    /// <param name="AccessToken">Short lived, sent on every request.</param>
    /// <param name="AccessTokenExpiresAt">When the access token expires.</param>
    /// <param name="RefreshToken">Long lived, used to renew. Only its hash is stored.</param>
    /// <param name="RefreshTokenExpiresAt">When the refresh token expires.</param>
    public sealed record TokensDto(
        string AccessToken,
        DateTime AccessTokenExpiresAt,
        string RefreshToken,
        DateTime RefreshTokenExpiresAt);

    /// <summary>Result of signing in or renewing a session.</summary>
    /// <param name="Tokens">Tokens handed to the client.</param>
    /// <param name="Session">User, roles, screens and menu.</param>
    public sealed record AuthenticationResultDto(TokensDto Tokens, SessionDto Session);
}
