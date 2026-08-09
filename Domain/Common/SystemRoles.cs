namespace WorkManagementSystem.Domain.Common;

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";

    public const string AdminOrManager = Admin + "," + Manager;
    public const string ManagerOrAdmin = Manager + "," + Admin;
}
