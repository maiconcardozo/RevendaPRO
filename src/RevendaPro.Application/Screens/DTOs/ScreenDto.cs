namespace RevendaPro.Application.Screens.DTOs
{
    /// <summary>One screen of the catalog, for the permission matrix.</summary>
    /// <param name="Code">Public identifier, used by the matrix to grant and revoke.</param>
    /// <param name="Key">Permission key.</param>
    /// <param name="Name">Label shown to the user, in Portuguese.</param>
    /// <param name="Icon">Lucide icon name.</param>
    /// <param name="Group">Section it belongs to.</param>
    /// <param name="Order">Position inside the group.</param>
    /// <param name="ShowInMenu">False means a permission without a menu item.</param>
    public sealed record ScreenDto(
        Guid Code,
        string Key,
        string Name,
        string? Icon,
        string Group,
        int Order,
        bool ShowInMenu);

    /// <summary>Screens grouped for the permission matrix.</summary>
    /// <param name="Group">Section header.</param>
    /// <param name="Screens">Screens inside it.</param>
    public sealed record ScreenGroupDto(string Group, IReadOnlyList<ScreenDto> Screens);
}
