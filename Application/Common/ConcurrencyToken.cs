namespace WorkManagementSystem.Application.Common;

public static class ConcurrencyToken
{
    public static byte[] Require(byte[]? rowVersion)
    {
        if (rowVersion is null || rowVersion.Length == 0)
            throw new BusinessException("RowVersion is required. Reload the latest data and try again.");

        return rowVersion;
    }
}
