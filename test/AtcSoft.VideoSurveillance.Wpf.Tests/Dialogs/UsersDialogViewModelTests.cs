namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// An admin adds, changes and removes users and API keys, and reads the audit
/// log; what the server refused is shown in its own words.
/// </summary>
public sealed class UsersDialogViewModelTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Loading_Shows_The_Users_The_Api_Keys_And_The_Audit_Log()
    {
        var admin = new FakeUserAdministration();
        admin.Users.Add(new UserAccount(Guid.NewGuid(), "admin", UserRole.Admin, Created));
        admin.ApiKeys.Add(new ApiKey(Guid.NewGuid(), "home assistant", UserRole.Viewer, Created));
        admin.Audit.Add(new AuditEntry(Created, "admin", "createCamera", "/cameras"));
        var sut = new UsersDialogViewModel(admin, confirm: _ => true);

        await sut.LoadAsync();

        sut.Users.Select(x => x.UserName).Should().Equal("admin");
        sut.Users[0].RoleText.Should().Be("Admin");
        sut.ApiKeys.Select(x => x.Name).Should().Equal("home assistant");
        sut.AuditEntries.Select(x => x.Action).Should().Equal("createCamera");
    }

    [Fact]
    public void The_Roles_Are_Named_In_Words_Least_First()
    {
        var sut = new UsersDialogViewModel(new FakeUserAdministration(), confirm: _ => true);

        sut.RoleItems.Keys.Should().Equal(UserRole.Names.Viewer, UserRole.Names.Operator, UserRole.Names.Admin);
        sut.RoleItems.Values.Should().Equal("Viewer", "Operator", "Admin");
    }

    [Fact]
    public async Task With_No_User_Chosen_Saving_Adds_One()
    {
        var admin = new FakeUserAdministration();
        var sut = new UsersDialogViewModel(admin, confirm: _ => true)
        {
            UserName = "ops",
            Password = "a long password",
            SelectedRoleKey = UserRole.Names.Operator,
        };

        await sut.SaveUserAsync();

        admin.CreatedUsers.Should().Equal(("ops", "a long password", UserRole.Operator));
        sut.Users.Select(x => x.UserName).Should().Equal("ops");
        sut.Message.Should().Be(Translations.UserSaved);
        sut.Problem.Should().BeNull();
    }

    [Fact]
    public async Task A_Chosen_User_Keeps_The_Password_When_It_Is_Left_Empty()
    {
        var admin = new FakeUserAdministration();
        var user = new UserAccount(Guid.NewGuid(), "ops", UserRole.Viewer, Created);
        admin.Users.Add(user);
        var sut = new UsersDialogViewModel(admin, confirm: _ => true);
        await sut.LoadAsync();

        sut.SelectedUser = sut.Users[0];
        sut.UserName.Should().Be("ops");
        sut.SelectedRoleKey.Should().Be(UserRole.Names.Viewer);
        sut.SelectedRoleKey = UserRole.Names.Admin;
        await sut.SaveUserAsync();

        admin.Updated.Should().Equal((user.Id, (UserRole?)UserRole.Admin, (string?)null));
    }

    [Fact]
    public async Task A_Refused_Save_Shows_The_Servers_Reason()
    {
        var admin = new FakeUserAdministration { Refusal = "A user with that name already exists." };
        var sut = new UsersDialogViewModel(admin, confirm: _ => true)
        {
            UserName = "admin",
            Password = "a long password",
        };

        await sut.SaveUserAsync();

        sut.Problem.Should().Be("The user could not be saved. A user with that name already exists.");
        sut.Message.Should().BeNull();
    }

    [Fact]
    public async Task A_User_Is_Removed_Only_When_The_Admin_Says_Yes()
    {
        var admin = new FakeUserAdministration();
        var user = new UserAccount(Guid.NewGuid(), "ops", UserRole.Viewer, Created);
        admin.Users.Add(user);
        var answer = false;
        string? asked = null;
        var sut = new UsersDialogViewModel(admin, confirm: question =>
        {
            asked = question;
            return answer;
        });
        await sut.LoadAsync();
        sut.SelectedUser = sut.Users[0];

        await sut.RemoveUserAsync();
        admin.Users.Should().HaveCount(1);
        asked.Should().Contain("ops");

        answer = true;
        await sut.RemoveUserAsync();
        admin.Users.Should().BeEmpty();
        sut.Users.Should().BeEmpty();
        sut.Message.Should().Be(Translations.UserRemoved);
    }

    [Fact]
    public async Task A_New_Api_Key_Is_Shown_Once()
    {
        var admin = new FakeUserAdministration();
        var sut = new UsersDialogViewModel(admin, confirm: _ => true)
        {
            ApiKeyName = "home assistant",
            SelectedApiKeyRoleKey = UserRole.Names.Viewer,
        };

        await sut.CreateApiKeyAsync();

        sut.MadeApiKey.Should().StartWith("vsk_");
        sut.ApiKeys.Select(x => x.Name).Should().Equal("home assistant");

        sut.ApiKeyName = "another";
        await sut.CreateApiKeyAsync();
        sut.MadeApiKey.Should().Be(admin.LastKey);
    }

    [Fact]
    public async Task An_Api_Key_Is_Removed_When_The_Admin_Says_Yes()
    {
        var admin = new FakeUserAdministration();
        admin.ApiKeys.Add(new ApiKey(Guid.NewGuid(), "home assistant", UserRole.Viewer, Created));
        var sut = new UsersDialogViewModel(admin, confirm: _ => true);
        await sut.LoadAsync();
        sut.SelectedApiKey = sut.ApiKeys[0];

        await sut.RemoveApiKeyAsync();

        admin.ApiKeys.Should().BeEmpty();
        sut.ApiKeys.Should().BeEmpty();
        sut.Message.Should().Be(Translations.ApiKeyRemoved);
    }

    private sealed class FakeUserAdministration : IUserAdministration
    {
        public List<UserAccount> Users { get; } = [];

        public List<ApiKey> ApiKeys { get; } = [];

        public List<AuditEntry> Audit { get; } = [];

        public List<(string UserName, string Password, UserRole Role)> CreatedUsers { get; } = [];

        public List<(Guid Id, UserRole? Role, string? Password)> Updated { get; } = [];

        public string? Refusal { get; init; }

        public string? LastKey { get; private set; }

        public Task<IReadOnlyList<UserAccount>> GetUsersAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserAccount>>([.. Users]);

        public Task<UserAccount> CreateUserAsync(
            string userName,
            string password,
            UserRole role,
            CancellationToken cancellationToken = default)
        {
            Refuse();
            CreatedUsers.Add((userName, password, role));
            var user = new UserAccount(Guid.NewGuid(), userName, role, DateTimeOffset.UtcNow);
            Users.Add(user);
            return Task.FromResult(user);
        }

        public Task<UserAccount> UpdateUserAsync(
            Guid userId,
            UserRole? role,
            string? password,
            CancellationToken cancellationToken = default)
        {
            Refuse();
            Updated.Add((userId, role, password));
            var index = Users.FindIndex(x => x.Id == userId);
            Users[index] = Users[index] with { Role = role ?? Users[index].Role };
            return Task.FromResult(Users[index]);
        }

        public Task DeleteUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            Refuse();
            Users.RemoveAll(x => x.Id == userId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ApiKey>> GetApiKeysAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ApiKey>>([.. ApiKeys]);

        public Task<NewApiKey> CreateApiKeyAsync(
            string name,
            UserRole role,
            CancellationToken cancellationToken = default)
        {
            Refuse();
            LastKey = "vsk_" + Guid.NewGuid().ToString("N");
            var key = new NewApiKey(Guid.NewGuid(), name, role, DateTimeOffset.UtcNow, LastKey);
            ApiKeys.Add(new ApiKey(key.Id, name, role, key.CreatedAt));
            return Task.FromResult(key);
        }

        public Task DeleteApiKeyAsync(
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            Refuse();
            ApiKeys.RemoveAll(x => x.Id == keyId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEntry>> GetAuditEntriesAsync(
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditEntry>>([.. Audit]);

        private void Refuse()
        {
            if (Refusal is not null)
            {
                throw new HttpRequestException(Refusal, inner: null, HttpStatusCode.Conflict);
            }
        }
    }
}