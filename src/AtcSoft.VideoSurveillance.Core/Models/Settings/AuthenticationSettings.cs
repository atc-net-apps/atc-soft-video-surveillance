namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Represents authentication settings for a network camera (Core POCO).
/// </summary>
public class AuthenticationSettings
{
    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <inheritdoc />
    public override string ToString()
        => $"AuthenticationSettings {{ UserName='{UserName ?? "(null)"}' }}";

    public AuthenticationSettings Clone()
        => new()
        {
            UserName = UserName,
            Password = Password,
        };

    public void CopyFrom(AuthenticationSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        UserName = source.UserName;
        Password = source.Password;
    }

    public bool ValueEquals(AuthenticationSettings? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(UserName, other.UserName, StringComparison.Ordinal) &&
               string.Equals(Password, other.Password, StringComparison.Ordinal);
    }
}