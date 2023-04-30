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

		internal TogglTrack _togglTrack;

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

			this._togglTrack = new TogglTrack(this._context, this._settings);
			await this._togglTrack.VerifyApiToken();
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
				return this._togglTrack.NotifyMissingToken();
			}
			else if (!InternetAvailability.IsInternetAvailable())
			{
				return this._togglTrack.NotifyNetworkUnavailable();
			}
			else if (!await this._togglTrack.VerifyApiToken())
			{
				return this._togglTrack.NotifyInvalidToken();
			}

			if (string.IsNullOrWhiteSpace(query.Search))
			{
				return await this._togglTrack.GetDefaultHotKeys();
			}

			return query.FirstSearch.ToLower() switch
			{
				Settings.StartCommand => await this._togglTrack.RequestStartEntry(token, query),
				Settings.EditCommand => await this._togglTrack.RequestEditEntry(token, query),
				Settings.StopCommand => await this._togglTrack.RequestStopEntry(token),
				Settings.ContinueCommand => await this._togglTrack.RequestContinueEntry(token, query),
				_ => (await this._togglTrack.GetDefaultHotKeys())
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