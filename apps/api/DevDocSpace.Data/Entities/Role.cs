namespace DevDocSpace.Data.Entities;

public enum Role
{
    ExternalClient = 0,
    InternalDeveloper = 1,
    Admin = 2,
}

public static class RoleExtensions
{
    public static bool Satisfies(this Role userRole, Role required) => userRole >= required;
}
