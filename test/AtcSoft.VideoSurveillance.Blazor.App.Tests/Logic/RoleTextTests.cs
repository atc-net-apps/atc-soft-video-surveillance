namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class RoleTextTests
{
    [Fact]
    public void Each_Role_Is_Named_In_Words_And_One_The_Client_Does_Not_Know_As_It_Is()
    {
        RoleText.For(UserRole.Viewer).Should().Be("Viewer");
        RoleText.For(UserRole.Operator).Should().Be("Operator");
        RoleText.For(UserRole.Admin).Should().Be("Admin");
        RoleText.For(new UserRole("auditor")).Should().Be("auditor");
        RoleText.For(role: null).Should().BeEmpty();
    }

    [Fact]
    public void The_Roles_To_Choose_From_Go_From_The_Least_To_The_Most()
        => RoleText.Choices.Should().Equal(UserRole.Viewer, UserRole.Operator, UserRole.Admin);
}