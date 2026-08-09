using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Tests;

public class DtoValidationTests
{
    [Fact]
    public void CreateProgressDto_RejectsEmptyTaskId()
    {
        var dto = new CreateProgressDto
        {
            TaskId = Guid.Empty,
            Percent = 50
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateProgressDto.TaskId)));
    }

    [Fact]
    public void CreateProjectDto_AllowsNullUnitId_ButRejectsEmptyUnitId()
    {
        var withNullUnit = new CreateProjectDto
        {
            Name = "Internal portal",
            UnitId = null
        };
        var withEmptyUnit = new CreateProjectDto
        {
            Name = "Internal portal",
            UnitId = Guid.Empty
        };

        Assert.DoesNotContain(Validate(withNullUnit), error => error.MemberNames.Contains(nameof(CreateProjectDto.UnitId)));
        Assert.Contains(Validate(withEmptyUnit), error => error.MemberNames.Contains(nameof(CreateProjectDto.UnitId)));
    }

    [Fact]
    public void CreateCommentDto_RejectsEmptyTaskIdAndContent()
    {
        var dto = new CreateCommentDto
        {
            TaskId = Guid.Empty,
            Content = string.Empty
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateCommentDto.TaskId)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateCommentDto.Content)));
    }

    [Fact]
    public void ChangePasswordDto_RejectsShortOrMismatchedPassword()
    {
        var dto = new ChangePasswordDto
        {
            OldPassword = "old-password",
            NewPassword = "123",
            ConfirmPassword = "456"
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ChangePasswordDto.NewPassword)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ChangePasswordDto.ConfirmPassword)));
    }

    [Fact]
    public void ProfileDto_RejectsBlankNameAndInvalidEmail()
    {
        var dto = new ProfileDto
        {
            FullName = "   ",
            Email = "invalid-email"
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ProfileDto.FullName)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ProfileDto.Email)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Admin")]
    [InlineData("manager")]
    public void UpdateUserDto_RejectsUnsupportedStaffRole(string role)
    {
        var dto = new UpdateUserDto
        {
            Role = role,
            UnitId = Guid.NewGuid()
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(UpdateUserDto.Role)));
    }

    [Fact]
    public void UpdateDtos_RejectEmptyRowVersion()
    {
        var dtos = new object[]
        {
            new UpdateTaskDto { Title = "Task" },
            new UpdateProjectDto { Name = "Project" },
            new UpdateUnitDto { Name = "Unit" },
            new UpdateUserDto { Role = "User" }
        };

        foreach (var dto in dtos)
        {
            var errors = Validate(dto);

            Assert.Contains(errors, error => error.MemberNames.Contains("RowVersion"));
        }
    }

    [Theory]
    [InlineData("Critical")]
    [InlineData("1")]
    public void TaskDtos_RejectUnsupportedPriority(string priority)
    {
        var createDto = new CreateTaskDto { Title = "Task", Priority = priority };
        var updateDto = new UpdateTaskDto
        {
            Title = "Task",
            Priority = priority,
            RowVersion = new byte[] { 1 }
        };

        Assert.Contains(
            Validate(createDto),
            error => error.MemberNames.Contains(nameof(CreateTaskDto.Priority)));
        Assert.Contains(
            Validate(updateDto),
            error => error.MemberNames.Contains(nameof(UpdateTaskDto.Priority)));
    }

    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }
}
