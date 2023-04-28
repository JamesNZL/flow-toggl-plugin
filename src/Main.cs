
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
			var results = new List<Result>();

			// TODO: properly check valid API key
			if (string.IsNullOrWhiteSpace(_settings.ApiToken))
			{
				results.Add(new Result
				{
					Title = "ERROR: Missing API token",
					SubTitle = "Configure Toggl Track API token in Flow Launcher settings",
					IcoPath = _context.CurrentPluginMetadata.IcoPath,
					Action = c =>
					{
						_context.API.OpenSettingDialog();
						return true;
					}
				});

				return results;
			}

			// TODO: switch selected result on 'start' and 'stop'
			results.Add(new Result
			{
				Title = "Start a new time entry",
				SubTitle = query.Search,
				IcoPath = _context.CurrentPluginMetadata.IcoPath,
			});
			results.Add(new Result
			{
				Title = "Stop current time entry",
				SubTitle = "0:52:43 Flow Launcher Toggl plugin",
				IcoPath = _context.CurrentPluginMetadata.IcoPath,
			});

			return results;
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