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

		public bool ShowUsageTips
		{
			get => this.Settings.ShowUsageTips;
			set
			{
				this.Settings.ShowUsageTips = value;
				this.OnPropertyChanged();
			}
		}

		public bool ShowUsageExamples
		{
			get => this.Settings.ShowUsageExamples;
			set
			{
				this.Settings.ShowUsageExamples = value;
				this.OnPropertyChanged();
			}
		}

		public bool ShowUsageWarnings
		{
			get => this.Settings.ShowUsageWarnings;
			set
			{
				this.Settings.ShowUsageWarnings = value;
				this.OnPropertyChanged();
			}
		}

		public bool AllowSuccessNotifications
		{
			get => this.Settings.AllowSuccessNotifications;
			set
			{
				this.Settings.AllowSuccessNotifications = value;
				this.OnPropertyChanged();
			}
		}

		public bool AllowErrorNotifications
		{
			get => this.Settings.AllowErrorNotifications;
			set
			{
				this.Settings.AllowErrorNotifications = value;
				this.OnPropertyChanged();
			}
		}
	}
}