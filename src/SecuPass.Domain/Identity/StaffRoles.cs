namespace SecuPass.Domain.Identity;

// Every staff user holds exactly one of these. Volunteer arrives with the brief's F09.
public static class StaffRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string ClientViewer = "ClientViewer";
}