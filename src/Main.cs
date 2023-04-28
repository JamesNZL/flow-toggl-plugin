
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
		private PluginInitContext _contex;
		private Settings _settings;
		private static SettingsViewModel _viewModel;

		/// <summary>
		/// Runs on plugin initialisation.
		/// Expensive operations should be performed here.
		/// </summary>
		/// <param name="context"></param>
		public void Init(PluginInitContext context)
		{
			this._contex = context;
			this._settings = context.API.LoadSettingJsonStorage<Settings>();
			TogglTrack._viewModel = new SettingsViewModel(this._settings);
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
			if (string.IsNullOrWhiteSpace(this._settings.ApiToken))
			{
				results.Add(new Result
				{
					Title = "ERROR: Missing API token",
					SubTitle = "Configure Toggl Track API token in Flow Launcher settings",
					IcoPath = this._contex.CurrentPluginMetadata.IcoPath,
					Action = c =>
					{
						this._contex.API.OpenSettingDialog();
						return true;
					},
				});

				return results;
			}

			// TODO: switch selected result on 'start' and 'stop'
			results.Add(new Result
			{
				Title = "Start a new time entry",
				SubTitle = query.Search,
				IcoPath = this._contex.CurrentPluginMetadata.IcoPath,
			});
			results.Add(new Result
			{
				Title = "Stop current time entry",
				SubTitle = "0:52:43 Flow Launcher Toggl plugin",
				IcoPath = this._contex.CurrentPluginMetadata.IcoPath,
			});

			return results;
		}

		/// <summary>
		/// Creates the settings panel.
		/// </summary>
		/// <returns></returns>
		public Control CreateSettingPanel()
		{
			return new TogglTrackSettings(TogglTrack._viewModel);
		}
	}
}