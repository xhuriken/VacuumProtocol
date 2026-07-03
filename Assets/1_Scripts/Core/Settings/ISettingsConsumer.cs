/// <summary>
/// Description: Interface for any system that consumes settings changes.
/// Context: Used by UI presenters, audio controllers, and input handlers to receive global configuration updates.
/// Justification: Prevents tight coupling between the SettingsManager (Singleton) and gameplay systems, allowing isolated testing and modularity.
/// </summary>
public interface ISettingsConsumer
{
    /// <summary>
    /// Description: Triggered when settings are initialized or updated.
    /// Context: Called by the SettingsManager when new data is loaded from disk or modified by the user.
    /// Justification: Provides a unified entry point for consumers to apply new configurations (e.g., updating volume or rebinding keys).
    /// </summary>
    /// <param name="settings">The current settings data.</param>
    void OnSettingsUpdated(SettingsData settings);
}
