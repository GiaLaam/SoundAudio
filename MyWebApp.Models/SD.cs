namespace MyWebApp.Models
{
    public static class SD
    {
        public const string Role_Admin = "Admin";
        public const string Role_User = "Người dùng";
        public const string Role_Author = "Nghệ sĩ";

        public static List<string> Roles => new()
        {
            Role_User,
            Role_Admin,
            Role_Author
        };
    }
}
