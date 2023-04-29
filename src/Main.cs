
using System.Windows.Controls;
using System.Collections.Generic;
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
			await this.togglTrack.VerifyApiToken();
		}

		/// <summary>
		/// Runs on each query and displays the results to the user.
		/// </summary>
		/// <param name="query"></param>
		/// <returns></returns>
		public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
		{
			if (string.IsNullOrWhiteSpace(this._settings.ApiToken))
			{
				return togglTrack.NotifyMissingToken();
			}
			else if (!await this.togglTrack.VerifyApiToken())
			{
				return togglTrack.NotifyInvalidToken();
			}

			if (string.IsNullOrWhiteSpace(query.Search))
			{
				return await togglTrack.GetDefaultHotKeys();
			}

			return query.FirstSearch.ToLower() switch
			{
				Settings.StartCommand => await togglTrack.RequestStartEntry(token, query),
				Settings.StopCommand => await togglTrack.RequestStopEntry(token),
				Settings.ContinueCommand => await togglTrack.RequestContinueEntry(token, query),
				_ => (await togglTrack.GetDefaultHotKeys())
					.FindAll(result =>
					{
						return this._context.API.FuzzySearch(query.Search, result.Title).Score > 0;
					}),
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