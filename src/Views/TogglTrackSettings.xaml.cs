using System.Windows.Controls;
using Flow.Launcher.Plugin.TogglTrack.ViewModels;

namespace Flow.Launcher.Plugin.TogglTrack.Views
{
	/// <summary>
	/// Interaction logic for TogglTrackSettings.xaml.
	/// </summary>
	public partial class TogglTrackSettings : UserControl
	{
		private readonly SettingsViewModel _viewModel;
		private readonly Settings _settings;

		/// <Summary>
		/// Initalises settings view.
		/// </Summary>
		public TogglTrackSettings(SettingsViewModel viewModel)
		{
			this._viewModel = viewModel;
			this._settings = viewModel.Settings;
			this.DataContext = viewModel;
			this.InitializeComponent();
		}
	}
}