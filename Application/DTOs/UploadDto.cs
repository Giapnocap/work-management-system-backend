namespace WorkManagementSystem.Application.DTOs
{
    public class UploadFileDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid? ProgressId { get; set; }
        public Guid? TaskId { get; set; }
        public Guid? UploadedBy { get; set; }
    }

    public class UploadFileDownloadDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }
}
