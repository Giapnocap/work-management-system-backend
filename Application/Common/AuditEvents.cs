namespace WorkManagementSystem.Application.Common
{
    public static class AuditEntityTypes
    {
        public const string Account = "Account";
        public const string Unit = "Unit";
        public const string Project = "Project";
        public const string KpiPeriod = "KpiPeriod";
    }

    public static class AuditActions
    {
        public const string Registered = "Registered";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string PasswordChanged = "PasswordChanged";
        public const string PasswordReset = "PasswordReset";
        public const string AssignmentChanged = "AssignmentChanged";
        public const string Deleted = "Deleted";
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string Archived = "Archived";
        public const string Locked = "Locked";
    }
}
