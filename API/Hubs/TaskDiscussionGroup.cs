namespace WorkManagementSystem.API.Hubs;

public static class TaskDiscussionGroup
{
    public static string For(Guid taskId) => $"task:{taskId:N}";
}
