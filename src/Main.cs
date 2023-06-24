using System.Windows.Controls;
using System.Text.RegularExpressions;
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

		private (
			string LastCommand,
			// ! this is needed as tuples must have minimum 2 elements
			bool? _null
		) _state = (
			LastCommand: string.Empty,
			null
		);

		public static string ExtractQueryTo(Query query, int index)
		{
			return string.Join(" ", query.SearchTerms.Take(index));
		}

		public static string ExtractQueryAfter(Query query, int index)
		{
			return (index == 1)
				// Expect slight performance improvement by using query.SecondToEndSearch directly
				? query.SecondToEndSearch
				: string.Join(" ", query.SearchTerms.Skip(index));
		}

		public static string ExtractQueryBetween(Query query, int after, int to)
		{
			return string.Join(" ", query.SearchTerms.Take(to).Skip(after));
		}

		public static string UnescapeSearch(string search)
		{
			return Regex.Replace(search, @"(\\(?!\\))", string.Empty);
		}

		public static string EscapeDescription(string description)
		{
			string escaped = Regex.Replace(description, @"(\\(?!\\))", @"\\");
			return Regex.Replace(escaped, @" -", @" \-");
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
				return await this._togglTrack.GetDefaultHotKeys(prefetch: true);
			}

			string command = query.FirstSearch;
			if (!Settings.Commands.Contains(command) && command == this._state.LastCommand)
			{
				command = (await this._togglTrack.GetDefaultHotKeys())
					.GroupBy(result => this._context!.API.FuzzySearch(query.FirstSearch, result.Title).Score)
					.MaxBy(group => group.Key)
					?.MaxBy(result => result.Score)
					?.Title
					?? query.FirstSearch;
				this._context!.API.ChangeQuery($"{query.ActionKeyword} {command} ");
			}
			this._state.LastCommand = command;

			return (command.ToLower()) switch
			{
				Settings.StartCommand => await this._togglTrack.RequestStartEntry(token, query),
				Settings.StopCommand => await this._togglTrack.RequestStopEntry(token, query),
				Settings.ContinueCommand => await this._togglTrack.RequestContinueEntry(token, query),
				Settings.EditCommand => await this._togglTrack.RequestEditEntry(token, query),
				Settings.DeleteCommand => await this._togglTrack.RequestDeleteEntry(token, query),
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