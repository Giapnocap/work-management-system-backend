namespace WorkManagementSystem.Application.Common;

public static class ConcurrencyToken
{
    public static byte[] Require(byte[]? rowVersion)
    {
        if (rowVersion is null || rowVersion.Length == 0)
            throw new BusinessException("RowVersion là bắt buộc. Vui lòng tải lại dữ liệu mới nhất và thử lại.");

        return rowVersion;
    }
}
