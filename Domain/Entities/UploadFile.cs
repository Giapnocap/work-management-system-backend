namespace WorkManagementSystem.Domain.Entities
{
    public class UploadFile
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StorageKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid? ProgressId { get; set; }
        public Guid TaskId { get; set; }
        public Guid? UploadedBy { get; set; }
    }
}
