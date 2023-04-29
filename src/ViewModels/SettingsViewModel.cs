namespace Flow.Launcher.Plugin.TogglTrack.ViewModels
{
	/// <Summary>
	/// GUI settings logic.
	/// </Summary>
	public class SettingsViewModel : BaseModel
	{
		/// <Summary>
		/// Initalises settings view with defaults.
		/// </Summary>
		public SettingsViewModel(Settings settings)
		{
			this.Settings = settings;
		}

		/// <Summary>
		/// Gets the setting.
		/// </Summary>
		public Settings Settings { get; init; }

		/// <Summary>
		/// Handles updates to ApiToken setting.
		/// </Summary>
		public string ApiToken
		{
			get => new string('*', this.Settings.ApiToken.Length);
			set
			{
				this.Settings.ApiToken = value;
				this.OnPropertyChanged();
			}
		}
	}
}