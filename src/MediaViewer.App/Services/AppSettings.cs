namespace MediaViewer.App.Services;

/// <summary>Persisted app settings: server URL, device token, selected profile.</summary>
public sealed class AppSettings
{
    private const string ServerUrlKey = "server_url";
    private const string DeviceTokenKey = "device_token";
    private const string ProfileIdKey = "profile_id";
    private const string ProfileNameKey = "profile_name";

    public string? ServerUrl
    {
        get => Preferences.Default.Get<string?>(ServerUrlKey, null);
        set
        {
            if (value is null) Preferences.Default.Remove(ServerUrlKey);
            else Preferences.Default.Set(ServerUrlKey, value.TrimEnd('/'));
        }
    }

    public string? DeviceToken
    {
        get => Preferences.Default.Get<string?>(DeviceTokenKey, null);
        set
        {
            if (value is null) Preferences.Default.Remove(DeviceTokenKey);
            else Preferences.Default.Set(DeviceTokenKey, value);
        }
    }

    public int? ProfileId
    {
        get
        {
            var v = Preferences.Default.Get(ProfileIdKey, -1);
            return v < 0 ? null : v;
        }
        set
        {
            if (value is null) Preferences.Default.Remove(ProfileIdKey);
            else Preferences.Default.Set(ProfileIdKey, value.Value);
        }
    }

    public string? ProfileName
    {
        get => Preferences.Default.Get<string?>(ProfileNameKey, null);
        set
        {
            if (value is null) Preferences.Default.Remove(ProfileNameKey);
            else Preferences.Default.Set(ProfileNameKey, value);
        }
    }

    public bool IsPaired => !string.IsNullOrEmpty(ServerUrl) && !string.IsNullOrEmpty(DeviceToken);

    public void Unpair()
    {
        DeviceToken = null;
        ProfileId = null;
        ProfileName = null;
    }
}
