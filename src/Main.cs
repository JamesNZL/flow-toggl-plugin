
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Text;
using System.Collections.Generic;
using Flow.Launcher.Plugin.TogglTrack.ViewModels;
using Flow.Launcher.Plugin.TogglTrack.Views;

namespace Flow.Launcher.Plugin.TogglTrack
{
	/// <summary>
	/// Flow Launcher Toggl Track plugin logic.
	/// </summary>
	public class TogglTrack : IPlugin, ISettingProvider
	{
		private PluginInitContext _context;
		private Settings _settings;
		private static SettingsViewModel _viewModel;

		/// <summary>
		/// Runs on plugin initialisation.
		/// Expensive operations should be performed here.
		/// </summary>
		/// <param name="context"></param>
		public void Init(PluginInitContext context)
		{
			_context = context;
			_settings = context.API.LoadSettingJsonStorage<Settings>();
			_viewModel = new SettingsViewModel(_settings);
		}

		/// <summary>
		/// Runs on each query and displays the results to the user.
		/// </summary>
		/// <param name="query"></param>
		/// <returns></returns>
		public List<Result> Query(Query query)
		{
			return new List<Result>();
		}

		/// <summary>
		/// Creates the settings panel.
		/// </summary>
		/// <returns></returns>
		public Control CreateSettingPanel()
		{
			return new TogglTrackSettings(_viewModel);
		}
	}
}