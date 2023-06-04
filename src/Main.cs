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
		private static SettingsViewModel? _viewModel;
		private PluginInitContext? _context;
		private Settings? _settings;

		internal TogglTrack? _togglTrack;

		public static string ExtractFromQuery(Query query, int index)
		{
			return (index == 1)
				// Expect slight performance improvement by using query.SecondToEndSearch directly
				? query.SecondToEndSearch
				: string.Join(" ", query.SearchTerms.Skip(index));
		}

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

			// Complete the API calls on background threads so plugin initialisation can proceed as soon as possible
			// Cache is not immediately needed so no need to block for it to fulfil
			await Task.Run(() =>
			{
				_ = Task.Run(async () =>
				{
					if (!await this._togglTrack!.VerifyApiToken())
					{
						return;
					}

					this._togglTrack.RefreshCache();
				});
			});
		}

		/// <summary>
		/// Runs on each query and displays the results to the user.
		/// </summary>
		/// <param name="query"></param>
		/// <returns></returns>
		public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
		{
			if (string.IsNullOrWhiteSpace(this._settings!.ApiToken))
			{
				return this._togglTrack!.NotifyMissingToken();
			}
			else if (!InternetAvailability.IsInternetAvailable())
			{
				return this._togglTrack!.NotifyNetworkUnavailable();
			}
			else if (!await this._togglTrack!.VerifyApiToken())
			{
				return this._togglTrack.NotifyInvalidToken();
			}

			if (string.IsNullOrWhiteSpace(query.Search))
			{
				return await this._togglTrack.GetDefaultHotKeys();
			}

			return (query.FirstSearch.ToLower()) switch
			{
				Settings.StartCommand => await this._togglTrack.RequestStartEntry(token, query),
				Settings.EditCommand => await this._togglTrack.RequestEditEntry(token, query),
				Settings.StopCommand => await this._togglTrack.RequestStopEntry(token, query),
				Settings.DeleteCommand => await this._togglTrack.RequestDeleteEntry(token),
				Settings.ContinueCommand => await this._togglTrack.RequestContinueEntry(token, query),
				Settings.ReportsCommand => await this._togglTrack.RequestViewReports(token, query),
				_ => (await this._togglTrack.GetDefaultHotKeys())
					.FindAll(result =>
					{
						return this._context!.API.FuzzySearch(query.Search, result.Title).Score > 0;
					}),
			};
		}

		/// <summary>
		/// Creates the settings panel.
		/// </summary>
		/// <returns></returns>
		public Control CreateSettingPanel()
		{
			return new TogglTrackSettings(Main._viewModel!);
		}
	}
}