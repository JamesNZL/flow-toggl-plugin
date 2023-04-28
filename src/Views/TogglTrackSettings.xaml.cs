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
			_viewModel = viewModel;
			_settings = viewModel.Settings;
			DataContext = viewModel;
			InitializeComponent();
		}
	}
}