
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.TogglTrack.ViewModels;
using Flow.Launcher.Plugin.TogglTrack.Views;

namespace Flow.Launcher.Plugin.TogglTrack
{
	/// <summary>
	/// Flow Launcher Toggl Track plugin logic.
	/// </summary>
	public class Main : IAsyncPlugin, ISettingProvider
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
		public async Task InitAsync(PluginInitContext context)
		{
			this._context = context;
			this._settings = context.API.LoadSettingJsonStorage<Settings>();
			Main._viewModel = new SettingsViewModel(this._settings);

			this.togglTrack = new TogglTrack(this._context, this._settings);

			await Task.CompletedTask;
		}

		/// <summary>
		/// Runs on each query and displays the results to the user.
		/// </summary>
		/// <param name="query"></param>
		/// <returns></returns>
		public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
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
				Settings.StopCommand => await togglTrack.RequestStopEntry(token),
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