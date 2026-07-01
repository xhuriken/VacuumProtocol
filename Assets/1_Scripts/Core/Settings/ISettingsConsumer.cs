/// <summary>
/// Interface for any system that consumes settings changes.
/// Prevents tight coupling between the SettingsManager and gameplay systems.
/// </summary>
public interface ISettingsConsumer
{
    /// <summary>
    /// Triggered when settings are initialized or updated.
    /// </summary>
    /// <param name="settings">The current settings data.</param>
    void OnSettingsUpdated(SettingsData settings);
}
