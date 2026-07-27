using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Application.Services
{
    public sealed class UploadFileValidator : IUploadFileValidator
    {
        public const long MaxFileSizeBytes = 10 * 1024 * 1024;
        private const long MaxOoxmlUncompressedBytes = 50 * 1024 * 1024;
        private const int MaxOoxmlEntries = 2048;
        private const int MaxOriginalFileNameLength = 200;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
            ".pdf",
            ".doc", ".docx",
            ".xls", ".xlsx",
            ".ppt", ".pptx",
            ".txt", ".csv"
        };

        private static readonly Dictionary<string, string[]> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new[] { "image/jpeg" },
            [".jpeg"] = new[] { "image/jpeg" },
            [".png"] = new[] { "image/png" },
            [".gif"] = new[] { "image/gif" },
            [".bmp"] = new[] { "image/bmp", "image/x-ms-bmp" },
            [".webp"] = new[] { "image/webp" },
            [".pdf"] = new[] { "application/pdf" },
            [".doc"] = new[] { "application/msword", "application/octet-stream" },
            [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/zip", "application/octet-stream" },
            [".xls"] = new[] { "application/vnd.ms-excel", "application/octet-stream" },
            [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/zip", "application/octet-stream" },
            [".ppt"] = new[] { "application/vnd.ms-powerpoint", "application/octet-stream" },
            [".pptx"] = new[] { "application/vnd.openxmlformats-officedocument.presentationml.presentation", "application/zip", "application/octet-stream" },
            [".txt"] = new[] { "text/plain", "application/octet-stream" },
            [".csv"] = new[] { "text/csv", "application/vnd.ms-excel", "text/plain", "application/octet-stream" }
        };

        public async Task<UploadFileValidationResult> ValidateAsync(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                throw new BusinessException("File is empty.");

            if (file.Length > MaxFileSizeBytes)
                throw new BusinessException("File vuot qua dung luong toi da 10MB.");

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
                throw new BusinessException($"Dinh dang file '{extension}' khong duoc phep.");

            var normalizedExtension = extension.ToLowerInvariant();
            ValidateContentType(file.ContentType, normalizedExtension);

            await using var stream = file.OpenReadStream();
            var header = new byte[Math.Min(512, file.Length)];
            var read = await stream.ReadAsync(
                header.AsMemory(0, header.Length),
                cancellationToken);
            if (!HasValidSignature(normalizedExtension, header.AsSpan(0, read)))
                throw new BusinessException("Noi dung file khong khop voi dinh dang duoc phep.");

            if (IsOoxmlExtension(normalizedExtension))
            {
                await using var packageStream = file.OpenReadStream();
                await ValidateOoxmlPackage(packageStream, normalizedExtension, cancellationToken);
            }

            return new UploadFileValidationResult(
                normalizedExtension,
                SanitizeOriginalFileName(file.FileName, normalizedExtension));
        }

        public string GetDownloadContentType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
        }

        private static void ValidateContentType(string? contentType, string extension)
        {
            var normalizedContentType = contentType?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedContentType) &&
                AllowedMimeTypes.TryGetValue(extension, out var allowedMimeTypes) &&
                !allowedMimeTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
            {
                throw new BusinessException("Content-Type cua file khong khop voi dinh dang duoc phep.");
            }
        }

        private static async Task ValidateOoxmlPackage(
            Stream source,
            string extension,
            CancellationToken cancellationToken)
        {
            Stream packageStream = source;
            MemoryStream? bufferedStream = null;
            if (!source.CanSeek)
            {
                bufferedStream = new MemoryStream();
                await source.CopyToAsync(bufferedStream, cancellationToken);
                bufferedStream.Position = 0;
                packageStream = bufferedStream;
            }

            try
            {
                using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);
                if (archive.Entries.Count == 0 || archive.Entries.Count > MaxOoxmlEntries)
                    throw new BusinessException("Cau truc file Office khong hop le.");

                long totalUncompressedBytes = 0;
                var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateArchiveEntryPath(entry.FullName);

                    totalUncompressedBytes = checked(totalUncompressedBytes + entry.Length);
                    if (totalUncompressedBytes > MaxOoxmlUncompressedBytes)
                        throw new BusinessException("File Office co dung luong giai nen vuot qua gioi han an toan.");

                    if (entry.FullName.EndsWith("/vbaProject.bin", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.Equals("vbaProject.bin", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new BusinessException("File Office chua macro va khong duoc phep.");
                    }

                    entryNames.Add(entry.FullName.Replace('\\', '/'));
                }

                var requiredDocumentEntry = extension switch
                {
                    ".docx" => "word/document.xml",
                    ".xlsx" => "xl/workbook.xml",
                    ".pptx" => "ppt/presentation.xml",
                    _ => throw new BusinessException("Dinh dang Office khong duoc ho tro.")
                };

                if (!entryNames.Contains("[Content_Types].xml") ||
                    !entryNames.Contains("_rels/.rels") ||
                    !entryNames.Contains(requiredDocumentEntry))
                {
                    throw new BusinessException("Cau truc file Office khong khop voi phan mo rong.");
                }
            }
            catch (InvalidDataException)
            {
                throw new BusinessException("Cau truc file Office khong hop le.");
            }
            catch (OverflowException)
            {
                throw new BusinessException("File Office co dung luong giai nen vuot qua gioi han an toan.");
            }
            finally
            {
                if (bufferedStream != null)
                    await bufferedStream.DisposeAsync();
            }
        }

        private static void ValidateArchiveEntryPath(string entryName)
        {
            var normalized = entryName.Replace('\\', '/');
            if (normalized.StartsWith('/') ||
                normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains(".."))
            {
                throw new BusinessException("File Office chua duong dan noi bo khong an toan.");
            }
        }

        private static string SanitizeOriginalFileName(string fileName, string extension)
        {
            var normalizedPath = fileName.Replace('\\', '/');
            var name = Path.GetFileName(normalizedPath);
            var cleaned = new string(name
                .Where(character => !char.IsControl(character))
                .ToArray())
                .Trim();

            if (string.IsNullOrWhiteSpace(cleaned))
                return $"file{extension}";

            if (cleaned.Length <= MaxOriginalFileNameLength)
                return cleaned;

            var baseName = Path.GetFileNameWithoutExtension(cleaned);
            var maxBaseNameLength = Math.Max(1, MaxOriginalFileNameLength - extension.Length);
            return $"{baseName[..Math.Min(baseName.Length, maxBaseNameLength)]}{extension}";
        }

        private static bool IsOoxmlExtension(string extension)
            => extension is ".docx" or ".xlsx" or ".pptx";

        private static bool HasValidSignature(string extension, ReadOnlySpan<byte> header)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => StartsWith(header, 0xFF, 0xD8, 0xFF),
                ".png" => StartsWith(header, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
                ".gif" => StartsWithAscii(header, "GIF87a") || StartsWithAscii(header, "GIF89a"),
                ".bmp" => StartsWithAscii(header, "BM"),
                ".webp" => header.Length >= 12 && StartsWithAscii(header, "RIFF") && StartsWithAscii(header[8..], "WEBP"),
                ".pdf" => StartsWithAscii(header, "%PDF-"),
                ".doc" or ".xls" or ".ppt" => StartsWith(header, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1),
                ".docx" or ".xlsx" or ".pptx" => StartsWith(header, 0x50, 0x4B, 0x03, 0x04) ||
                                                   StartsWith(header, 0x50, 0x4B, 0x05, 0x06) ||
                                                   StartsWith(header, 0x50, 0x4B, 0x07, 0x08),
                ".txt" or ".csv" => LooksLikeText(header),
                _ => false
            };
        }

        private static bool StartsWith(ReadOnlySpan<byte> header, params byte[] signature)
            => header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature);

        private static bool StartsWithAscii(ReadOnlySpan<byte> header, string signature)
        {
            if (header.Length < signature.Length) return false;
            for (var index = 0; index < signature.Length; index++)
            {
                if (header[index] != signature[index]) return false;
            }

            return true;
        }

        private static bool LooksLikeText(ReadOnlySpan<byte> header)
            => !header.IsEmpty && !header.Contains((byte)0);
    }
}
