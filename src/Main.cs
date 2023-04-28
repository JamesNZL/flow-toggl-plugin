
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.TogglTrack.ViewModels;
using Flow.Launcher.Plugin.TogglTrack.Views;

namespace Flow.Launcher.Plugin.TogglTrack
{
	/// <summary>
	/// Flow Launcher Toggl Track plugin logic.
	/// </summary>
	public class Main : IPlugin, ISettingProvider
	{
		private static SettingsViewModel _viewModel;
		private PluginInitContext _context;
		private Settings _settings;

		internal TogglTrack togglTrack;

		/// <summary>
		/// Runs on plugin initialisation.
		/// Expensive operations should be performed here.
		/// </summary>
		/// <param name="context"></param>
		public void Init(PluginInitContext context)
		{
			this._context = context;
			this._settings = context.API.LoadSettingJsonStorage<Settings>();
			Main._viewModel = new SettingsViewModel(this._settings);

			this.togglTrack = new TogglTrack(this._context, this._settings);

			// TODO: inspect API key?
		}

		/// <summary>
		/// Runs on each query and displays the results to the user.
		/// </summary>
		/// <param name="query"></param>
		/// <returns></returns>
		public List<Result> Query(Query query)
		{
			// TODO: properly check valid API key
			if (string.IsNullOrWhiteSpace(this._settings.ApiToken))
			{
				return togglTrack.NotifyMissingToken();
			}

			if (string.IsNullOrWhiteSpace(query.Search))
			{
				return togglTrack.GetDefaultHotKeys();
			}

			return query.FirstSearch.ToLower() switch
			{
				_ => togglTrack.GetDefaultHotKeys()
					.Where(hotkey =>
					{
						return this._context.API.FuzzySearch(query.Search, hotkey.Title).Score > 0;
					}
					).ToList()
			};
		}

		/// <summary>
		/// Creates the settings panel.
		/// </summary>
		/// <returns></returns>
		public Control CreateSettingPanel()
		{
			return new TogglTrackSettings(Main._viewModel);
		}
	}
}