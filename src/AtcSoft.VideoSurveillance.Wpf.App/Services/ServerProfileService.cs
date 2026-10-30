namespace AtcSoft.VideoSurveillance.Wpf.App.Services;

/// <summary>
/// Manages server profile persistence to a local JSON file.
/// Not DI-registered — instantiated directly in App.xaml.cs before host construction.
/// </summary>
public sealed class ServerProfileService : JsonFileServiceBase<ServerProfileData>
{
    private static readonly string ProfilesFilePath = AtcSoft.VideoSurveillance.Helpers.ApplicationPaths.GetLocalDataPath("VideoSurveillance", "servers.json");

    public ServerProfileService()
        : base(ProfilesFilePath)
    {
    }

    /// <summary>
    /// Gets all saved server profiles.
    /// </summary>
    public IList<ServerProfile> Profiles
        => Data.Profiles;

    /// <summary>
    /// Gets the last-used server profile, or null if none.
    /// </summary>
    public ServerProfile? GetLastUsedProfile()
    {
        if (Data.LastUsedProfileId is null)
        {
            return null;
        }

        return Data.Profiles.FirstOrDefault(p => p.Id == Data.LastUsedProfileId);
    }

    /// <summary>
    /// Adds a new profile or updates an existing one.
    /// </summary>
    public void AddOrUpdateProfile(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var existingIndex = -1;
        for (var i = 0; i < Data.Profiles.Count; i++)
        {
            if (Data.Profiles[i].Id == profile.Id)
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            Data.Profiles[existingIndex] = profile;
        }
        else
        {
            Data.Profiles.Add(profile);
        }
    }

    /// <summary>
    /// Deletes a profile by its ID.
    /// </summary>
    public void DeleteProfile(Guid id)
    {
        var toRemove = Data.Profiles.FirstOrDefault(p => p.Id == id);
        if (toRemove is not null)
        {
            Data.Profiles.Remove(toRemove);
        }

        if (Data.LastUsedProfileId == id)
        {
            Data.LastUsedProfileId = null;
        }
    }

    /// <summary>
    /// Sets the last-used profile ID and updates the connection timestamp.
    /// </summary>
    public void SetLastUsed(Guid profileId)
    {
        Data.LastUsedProfileId = profileId;

        var profile = Data.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile is not null)
        {
            profile.LastConnectedAt = DateTimeOffset.Now;
        }
    }
}