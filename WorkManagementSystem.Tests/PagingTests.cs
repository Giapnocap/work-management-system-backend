using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.Tests;

public class PagingTests
{
    [Fact]
    public void Normalize_UsesDefaults_ForInvalidPageAndSize()
    {
        var paging = Paging.Normalize(0, 0);

        Assert.Equal(1, paging.Page);
        Assert.Equal(10, paging.Size);
    }

    [Fact]
    public void Normalize_CapsLargePageSize()
    {
        var paging = Paging.Normalize(1, 500);

        Assert.Equal(1, paging.Page);
        Assert.Equal(Paging.MaxPageSize, paging.Size);
    }

    [Fact]
    public void Normalize_UsesCustomDefaultSize()
    {
        var paging = Paging.Normalize(-5, -10, Paging.DefaultHistoryPageSize);

        Assert.Equal(1, paging.Page);
        Assert.Equal(20, paging.Size);
    }

    [Fact]
    public void Normalize_PreservesValidValues()
    {
        var paging = Paging.Normalize(3, 25);

        Assert.Equal(3, paging.Page);
        Assert.Equal(25, paging.Size);
    }
}
