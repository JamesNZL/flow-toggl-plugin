using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Humanizer;
using TimeSpanParserUtil;
using Flow.Launcher.Plugin.TogglTrack.TogglApi;

namespace Flow.Launcher.Plugin.TogglTrack
{
	internal class TogglTrack
	{
		private PluginInitContext _context { get; set; }
		private Settings _settings { get; set; }

		internal IconProvider _iconProvider;

		private TogglClient _client;

		private readonly (
			SemaphoreSlim Token,
			SemaphoreSlim Me,
			SemaphoreSlim RunningTimeEntries,
			SemaphoreSlim TimeEntries,
			SemaphoreSlim SummaryReports,
			SemaphoreSlim DetailedReports
		) _semaphores = (
			new SemaphoreSlim(1, 1),
			new SemaphoreSlim(1, 1),
			new SemaphoreSlim(1, 1),
			new SemaphoreSlim(1, 1),
			new SemaphoreSlim(1, 1),
			new SemaphoreSlim(1, 1)
		);

		private NullableCache _cache = new NullableCache();
		private (
			List<string> Summary,
			List<string> Detailed
		) _cacheKeys = (
			new List<string>(),
			new List<string>()
		);

		private enum ExclusiveResultsSource
		{
			Commands,
			Start,
			Stop,
			Edit,
			Delete,
			Reports,
		}
		private (
			(bool IsValid, string Token) LastToken,
			(ExclusiveResultsSource? Source, bool Locked) ResultsSource,
			(long TimeEntry, long? Project, long? Client) SelectedIds,
			bool ReportsShowDetailed
		) _state = (
			(false, string.Empty),
			(null, false),
			(-1, -1, -1),
			false
		);

		private enum ReportsSpanDatesError
		{
			None,
			ParsingError,
			TooLongError,
			EndBeforeStartError,
		}

		internal TogglTrack(PluginInitContext context, Settings settings)
		{
			this._context = context;
			this._settings = settings;

			this._client = new TogglClient(this._settings.ApiToken);
			this._iconProvider = new IconProvider(this._context);
		}

		private async ValueTask<MeResponse?> _GetMe(CancellationToken token, bool force = false)
		{
			const string cacheKey = "Me";

			bool hasWaited = (this._semaphores.Me.CurrentCount == 0);
			await this._semaphores.Me.WaitAsync();

			if (token.IsCancellationRequested)
			{
				this._semaphores.Me.Release();
				token.ThrowIfCancellationRequested();
			}

			if ((!force || hasWaited) && this._cache.Contains(cacheKey))
			{
				this._semaphores.Me.Release();
				return (MeResponse?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching me");

				var me = await this._client.GetMe();

				this._cache.Set(cacheKey, me, DateTimeOffset.Now.AddDays(3));

				if (token.IsCancellationRequested)
				{
					this._semaphores.Me.Release();
					token.ThrowIfCancellationRequested();
				}

				this._semaphores.Me.Release();
				return me;
			}
			catch (Exception exception)
			{
				token.ThrowIfCancellationRequested();

				this._context.API.LogException("TogglTrack", "Failed to fetch me", exception);

				this._semaphores.Me.Release();
				return null;
			}
		}

		private async ValueTask<TimeEntryResponse?> _GetRunningTimeEntry(CancellationToken token, bool force = false)
		{
			const string cacheKey = "RunningTimeEntry";

			bool hasWaited = (this._semaphores.RunningTimeEntries.CurrentCount == 0);
			await this._semaphores.RunningTimeEntries.WaitAsync();

			if (token.IsCancellationRequested)
			{
				this._semaphores.RunningTimeEntries.Release();
				token.ThrowIfCancellationRequested();
			}

			if ((!force || hasWaited) && this._cache.Contains(cacheKey))
			{
				this._semaphores.RunningTimeEntries.Release();
				return (TimeEntryResponse?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching running time entry");

				var runningTimeEntry = await this._client.GetRunningTimeEntry();

				this._cache.Set(cacheKey, runningTimeEntry, DateTimeOffset.Now.AddSeconds(60));

				if (token.IsCancellationRequested)
				{
					this._semaphores.RunningTimeEntries.Release();
					token.ThrowIfCancellationRequested();
				}

				this._semaphores.RunningTimeEntries.Release();
				return runningTimeEntry;
			}
			catch (Exception exception)
			{
				token.ThrowIfCancellationRequested();

				this._context.API.LogException("TogglTrack", "Failed to fetch running time entry", exception);

				this._semaphores.RunningTimeEntries.Release();
				return null;
			}
		}

		private async ValueTask<List<TimeEntryResponse>?> _GetTimeEntries(CancellationToken token, bool force = false)
		{
			const string cacheKey = "TimeEntries";

			bool hasWaited = (this._semaphores.TimeEntries.CurrentCount == 0);
			await this._semaphores.TimeEntries.WaitAsync();

			if (token.IsCancellationRequested)
			{
				this._semaphores.TimeEntries.Release();
				token.ThrowIfCancellationRequested();
			}

			if ((!force || hasWaited) && this._cache.Contains(cacheKey))
			{
				this._semaphores.TimeEntries.Release();
				return (List<TimeEntryResponse>?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching time entries");

				var timeEntries = await this._client.GetTimeEntries();

				this._cache.Set(cacheKey, timeEntries, DateTimeOffset.Now.AddSeconds(60));

				if (token.IsCancellationRequested)
				{
					this._semaphores.TimeEntries.Release();
					token.ThrowIfCancellationRequested();
				}

				this._semaphores.TimeEntries.Release();
				return timeEntries;
			}
			catch (Exception exception)
			{
				token.ThrowIfCancellationRequested();

				this._context.API.LogException("TogglTrack", "Failed to fetch time entries", exception);

				this._semaphores.TimeEntries.Release();
				return null;
			}
		}

		private async ValueTask<SummaryReportResponse?> _GetSummaryReport(
			CancellationToken token,
			long workspaceId,
			long userId,
			Settings.ReportsGroupingKey reportGrouping,
			DateTimeOffset start,
			DateTimeOffset? end,
			bool force = false
		)
		{
			string cacheKey = $"SummaryReport{workspaceId}{userId}{(int)reportGrouping}{start.ToString("yyyy-MM-dd")}{end?.ToString("yyyy-MM-dd")}";

			bool hasWaited = (this._semaphores.SummaryReports.CurrentCount == 0);
			await this._semaphores.SummaryReports.WaitAsync();

			if (token.IsCancellationRequested)
			{
				this._semaphores.SummaryReports.Release();
				token.ThrowIfCancellationRequested();
			}

			if ((!force || hasWaited) && this._cache.Contains(cacheKey))
			{
				this._semaphores.SummaryReports.Release();
				return (SummaryReportResponse?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching summary reports");

				var summary = await this._client.GetSummaryReport(
					workspaceId: workspaceId,
					userId: userId,
					reportGrouping: reportGrouping,
					start: start,
					end: end
				);

				this._cache.Set(cacheKey, summary, DateTimeOffset.Now.AddSeconds(60));
				this._cacheKeys.Summary.Add(cacheKey);

				if (token.IsCancellationRequested)
				{
					this._semaphores.SummaryReports.Release();
					token.ThrowIfCancellationRequested();
				}

				this._semaphores.SummaryReports.Release();
				return summary;
			}
			catch (Exception exception)
			{
				token.ThrowIfCancellationRequested();

				this._context.API.LogException("TogglTrack", "Failed to fetch summary reports", exception);

				this._semaphores.SummaryReports.Release();
				return null;
			}
		}

		private void _ClearSummaryReportCache()
		{
			this._context.API.LogInfo("TogglTrack", "Clearing summary reports cache");

			this._cacheKeys.Summary.ForEach(key => this._cache.Remove(key));
			this._cacheKeys.Summary.Clear();
		}

		private async ValueTask<List<DetailedReportTimeEntryGroupResponse>?> _GetDetailedReport(
			CancellationToken token,
			long workspaceId,
			long userId,
			List<long?>? projectIds,
			DateTimeOffset start,
			DateTimeOffset? end,
			bool force = false
		)
		{
			string cacheKey = $"DetailedReport{workspaceId}{userId}{string.Join(",", projectIds ?? new List<long?>())}{start.ToString("yyyy-MM-dd")}{end?.ToString("yyyy-MM-dd")}";

			bool hasWaited = (this._semaphores.DetailedReports.CurrentCount == 0);
			await this._semaphores.DetailedReports.WaitAsync();

			if (token.IsCancellationRequested)
			{
				this._semaphores.DetailedReports.Release();
				token.ThrowIfCancellationRequested();
			}

			if ((!force || hasWaited) && this._cache.Contains(cacheKey))
			{
				this._semaphores.DetailedReports.Release();
				return (List<DetailedReportTimeEntryGroupResponse>?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching detailed reports");

				var report = await this._client.GetDetailedReport(
					workspaceId: workspaceId,
					userId: userId,
					projectIds: projectIds,
					start: start,
					end: end
				);

				this._cache.Set(cacheKey, report, DateTimeOffset.Now.AddSeconds(60));
				this._cacheKeys.Detailed.Add(cacheKey);

				if (token.IsCancellationRequested)
				{
					this._semaphores.DetailedReports.Release();
					token.ThrowIfCancellationRequested();
				}

				this._semaphores.DetailedReports.Release();
				return report;
			}
			catch (Exception exception)
			{
				token.ThrowIfCancellationRequested();

				this._context.API.LogException("TogglTrack", "Failed to fetch detailed reports", exception);

				this._semaphores.DetailedReports.Release();
				return null;
			}
		}

		private void _ClearDetailedReportCache()
		{
			this._context.API.LogInfo("TogglTrack", "Clearing detailed reports cache");

			this._cacheKeys.Detailed.ForEach(key => this._cache.Remove(key));
			this._cacheKeys.Detailed.Clear();
		}
		private async ValueTask<SummaryReportResponse?> _GetMaxReportTimeEntries(
			CancellationToken token,
			bool force = false,
			bool refreshMe = false
		)
		{
			var me = (await this._GetMe(token, refreshMe))?.ToMe();
			if (me is null)
			{
				return null;
			}

			DateTimeOffset reportsNow;
			try
			{
				reportsNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, me.ReportsTimeZoneId);
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", $"Failed to convert time to reports time zone '{me.ReportsTimeZoneId}'", exception);
				// Use local time instead
				reportsNow = DateTimeOffset.Now;
			}
			return await this._GetSummaryReport(
				token,
				workspaceId: me.DefaultWorkspaceId,
				userId: me.Id,
				reportGrouping: Settings.ReportsGroupingKey.Entries,
				start: reportsNow.AddYears(-1), // API has a maximum reports duration of 1 year
				end: reportsNow,
				force
			);
		}

		internal void RefreshCache(bool force = true, bool refreshMe = false)
		{
			_ = Task.Run(() =>
			{
				this._context.API.LogInfo("TogglTrack", $"Refreshing cache, {force}, {refreshMe}");

				// This is the main one that needs to be run
				_ = this._GetMe(CancellationToken.None, force: refreshMe);
				_ = this._GetRunningTimeEntry(CancellationToken.None, force: force);
				_ = this._GetTimeEntries(CancellationToken.None, force: force);

				if (force)
				{
					this._ClearSummaryReportCache();
					this._ClearDetailedReportCache();
				}

				_ = this._GetMaxReportTimeEntries(CancellationToken.None, force: force, refreshMe: refreshMe);
			});
		}

		internal async ValueTask<bool> VerifyApiToken(CancellationToken token)
		{
			if (!InternetAvailability.IsInternetAvailable())
			{
				return false;
			}

			await this._semaphores.Token.WaitAsync();

			if (token.IsCancellationRequested)
			{
				this._semaphores.Token.Release();
				token.ThrowIfCancellationRequested();
			}

			if (this._settings.ApiToken.Equals(this._state.LastToken.Token))
			{
				this._semaphores.Token.Release();
				return this._state.LastToken.IsValid;
			}

			// Clear the cache if the token has changed (#15)
			this._cache.Clear();

			if (string.IsNullOrWhiteSpace(this._settings.ApiToken))
			{
				this._semaphores.Token.Release();
				return this._state.LastToken.IsValid = false;
			}

			this._client.UpdateToken(this._settings.ApiToken);

			// ! This must be CancellationToken.None as internal state has already been changed by the call to TogglClient#UpdateToken() above
			this._state.LastToken.IsValid = (await this._GetMe(CancellationToken.None, true))?.ToMe().ApiToken?.Equals(this._settings.ApiToken) ?? false;
			this._state.LastToken.Token = this._settings.ApiToken;

			this._semaphores.Token.Release();

			if (this._state.LastToken.IsValid)
			{
				this.RefreshCache(refreshMe: true);
			}

			return this._state.LastToken.IsValid;
		}

		internal void ShowSuccessMessage(string title, string subTitle = "", string iconPath = "")
		{
			if (!this._settings.AllowSuccessNotifications)
			{
				return;
			}

			this._context.API.ShowMsg(title, subTitle, iconPath);
		}

		internal void ShowErrorMessage(string title, string subTitle = "")
		{
			if (!this._settings.AllowErrorNotifications)
			{
				return;
			}

			this._context.API.ShowMsgError(title, subTitle);
		}

		internal List<Result> NotifyMissingToken()
		{
			return new List<Result>
			{
				new Result
				{
					Title = $"{Settings.ErrorPrefix}Missing API token",
					SubTitle = "Configure Toggl Track API token in Flow Launcher settings.",
					IcoPath = IconProvider.UsageErrorIcon,
					Action = _ =>
					{
						this._context.API.OpenSettingDialog();
						return true;
					},
				},
				new Result
				{
					Title = "Open Toggl Track profile settings",
					SubTitle = "Retrieve your API token from your Toggl Track profile settings.",
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
					Action = _ =>
					{
						this._context.API.OpenUrl(new Uri(@"https://track.toggl.com/profile#api-token"));
						return true;
					},
				},
			};
		}

		internal List<Result> NotifyNetworkUnavailable()
		{
			return new List<Result>
			{
				new Result
				{
					Title = $"{Settings.ErrorPrefix}No network connection",
					SubTitle = "Connect to the internet to use Toggl Track.",
					IcoPath = IconProvider.UsageErrorIcon,
					Action = _ =>
					{
						return true;
					},
				},
			};
		}

		internal List<Result> NotifyInvalidToken()
		{
			return new List<Result>
			{
				new Result
				{
					Title = $"{Settings.ErrorPrefix}Invalid API token",
					SubTitle = $"{this._settings.ApiToken} is not a valid API token.",
					IcoPath = IconProvider.UsageErrorIcon,
					Action = _ =>
					{
						this._context.API.OpenSettingDialog();
						return true;
					},
				},
				new Result
				{
					Title = "Open Toggl Track profile settings",
					SubTitle = "Retrieve your API token from your Toggl Track profile settings.",
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
					Action = _ =>
					{
						this._context.API.OpenUrl(new Uri(@"https://track.toggl.com/profile#api-token"));
						return true;
					},
				},
			};
		}

		internal List<Result> NotifyUnknownError()
		{
			return new List<Result>
			{
				new Result
				{
					Title = $"{Settings.ErrorPrefix}Unknown error",
					SubTitle = "An unexpected error has occurred.",
					IcoPath = IconProvider.UsageErrorIcon,
					Action = _ =>
					{
						return true;
					},
				},
			};
		}

		internal async ValueTask<List<Result>> RequestResults(CancellationToken token, Query query)
		{
			if (string.IsNullOrEmpty(query.Search))
			{
				this.RefreshCache(force: false);

				this._state.ResultsSource = (null, false);
				this._state.SelectedIds = (-1, -1, -1);
				this._state.ReportsShowDetailed = false;
			}
			else if (!this._state.ResultsSource.Locked)
			{
				this._state.ResultsSource.Source = (query.FirstSearch.ToLower()) switch
				{
					Settings.StopCommand => TogglTrack.ExclusiveResultsSource.Stop,
					Settings.EditCommand => TogglTrack.ExclusiveResultsSource.Edit,
					Settings.DeleteCommand => TogglTrack.ExclusiveResultsSource.Delete,
					Settings.ReportsCommand => TogglTrack.ExclusiveResultsSource.Reports,
					Settings.BrowserCommand => TogglTrack.ExclusiveResultsSource.Commands,
					Settings.HelpCommand => TogglTrack.ExclusiveResultsSource.Commands,
					Settings.RefreshCommand => TogglTrack.ExclusiveResultsSource.Commands,
					_ => null,
				};
			}

			bool includeAllResults = (this._state.ResultsSource.Source is null);

			if (includeAllResults)
			{
				var results = new List<Result>();

				// Add commands
				results.AddRange(await this._GetCommands(token, query));

				// Add results to start time entry
				results.AddRange(await this._GetStartResults(token, query));

				// Add previously matching time entries
				results.AddRange(await this._GetContinueResults(token, query));

				return results;
			}

			return (this._state.ResultsSource.Source) switch
			{
				TogglTrack.ExclusiveResultsSource.Commands => await this._GetCommands(token, query),
				TogglTrack.ExclusiveResultsSource.Start => await this._GetStartResults(token, query),
				TogglTrack.ExclusiveResultsSource.Stop => await this._GetStopResults(token, query),
				TogglTrack.ExclusiveResultsSource.Edit => await this._GetEditResults(token, query),
				TogglTrack.ExclusiveResultsSource.Delete => await this._GetDeleteResults(token, query),
				TogglTrack.ExclusiveResultsSource.Reports => await this._GetReportsResults(token, query),
				_ => await this._GetStartResults(token, query),
			};
		}

		internal async ValueTask<List<Result>> _GetCommands(CancellationToken token, Query query)
		{
			var results = new List<Result>
			{
				new Result
				{
					Title = Settings.EditCommand,
					SubTitle = "Edit a previous time entry",
					IcoPath = IconProvider.EditIcon,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.EditCommand} ",
					Score = 6000,
					Action = _ =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.EditCommand} ");
						return false;
					}
				},
				new Result
				{
					Title = Settings.DeleteCommand,
					SubTitle = "Delete a previous time entry",
					IcoPath = IconProvider.DeleteIcon,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} ",
					Score = 4000,
					Action = _ =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} ");
						return false;
					}
				},
				new Result
				{
					Title = Settings.ReportsCommand,
					SubTitle = "View tracked time reports",
					IcoPath = IconProvider.ReportsIcon,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ReportsCommand} ",
					Score = 2000,
					Action = _ =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ReportsCommand} ");
						return false;
					},
				},
				new Result
				{
					Title = Settings.BrowserCommand,
					SubTitle = "Open Toggl Track in browser",
					IcoPath = IconProvider.BrowserIcon,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.BrowserCommand} ",
					Score = 100,
					Action = _ =>
					{
						this._context.API.OpenUrl(new Uri(@"https://track.toggl.com/timer"));
						return true;
					},
				},
				new Result
				{
					Title = Settings.HelpCommand,
					SubTitle = "Open plugin command reference",
					IcoPath = IconProvider.HelpIcon,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.HelpCommand} ",
					Score = 75,
					Action = _ =>
					{
						this._context.API.OpenUrl(new Uri(@"https://github.com/JamesNZL/flow-toggl-plugin/blob/main/README.md#command-reference"));
						return true;
					},
				},
				new Result
				{
					Title = Settings.RefreshCommand,
					SubTitle = "Refresh plugin cache",
					IcoPath = IconProvider.RefreshIcon,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.RefreshCommand} ",
					Score = 5,
					Action = _ =>
					{
						this.RefreshCache(refreshMe: true);
						return true;
					},
				},
			};

			var me = (await this._GetMe(token))?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var runningTimeEntry = (await this._GetRunningTimeEntry(token))?.ToTimeEntry(me);
			if (runningTimeEntry is null)
			{
				return (string.IsNullOrEmpty(query.FirstSearch))
					? results
					: results.FindAll(result => this._context.API.FuzzySearch(query.FirstSearch, result.Title).Score > 0);
			}

			results.Add(new Result
			{
				Title = $"Stop {runningTimeEntry.GetDescription()}",
				TitleHighlightData = (runningTimeEntry.GetDescription() == Settings.EmptyDescription)
					? new List<int>()
					: Enumerable.Range("Stop ".Length, runningTimeEntry.GetDescription().Length).ToList(),
				SubTitle = $"{runningTimeEntry.Project?.WithClientName ?? Settings.NoProjectName} | {runningTimeEntry.HumanisedElapsed} ({runningTimeEntry.DetailedElapsed})",
				IcoPath = this._iconProvider.GetColourIcon(runningTimeEntry.Project?.Colour, IconProvider.StopIcon),
				AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} ",
				Score = 15050,
				Action = context =>
				{
					if (!context.SpecialKeyState.AltPressed)
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} ");
						return false;
					}

					// Alt key modifier will stop the time entry now
					Task.Run(async delegate
					{
						try
						{
							this._context.API.LogInfo("TogglTrack", $"{this._state.SelectedIds.Project}, {runningTimeEntry.Id}, {runningTimeEntry.WorkspaceId}, {runningTimeEntry.StartDate}, {runningTimeEntry.DetailedElapsed}, from commands");

							var stoppedTimeEntry = (await this._client.StopTimeEntry(
								workspaceId: runningTimeEntry.WorkspaceId,
								id: runningTimeEntry.Id
							))?.ToTimeEntry(me);

							if (stoppedTimeEntry?.Id is null)
							{
								throw new Exception("An API error was encountered.");
							}

							this.ShowSuccessMessage($"Stopped {stoppedTimeEntry.GetRawDescription()}", $"{runningTimeEntry.DetailedElapsed} elapsed", IconProvider.StopIcon);

							// Update cached running time entry state
							this.RefreshCache();
						}
						catch (Exception exception)
						{
							this._context.API.LogException("TogglTrack", "Failed to stop time entry", exception);
							this.ShowErrorMessage("Failed to stop time entry.", exception.Message);
						}
					});

					return true;
				}
			});

			return (string.IsNullOrEmpty(query.FirstSearch))
				? results
				: results.FindAll(result => this._context.API.FuzzySearch(query.FirstSearch, result.Title).Score > 0);
		}

		private async ValueTask<List<Result>> _GetStartResults(CancellationToken token, Query query)
		{
			if (!string.IsNullOrEmpty(query.Search) && Settings.Commands.Any(command => command.StartsWith(query.Search)))
			{
				return Main.NoResults;
			}

			var me = (await this._GetMe(token))?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var transformedQuery = new TransformedQuery(query);

			if (transformedQuery.HasProjectPrefix())
			{
				if (this._state.ResultsSource.Source != TogglTrack.ExclusiveResultsSource.Start)
				{
					this._state.ResultsSource = (TogglTrack.ExclusiveResultsSource.Start, true);

					this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search}", true);
					return Main.NoResults;
				}

				var projects = new List<Result>();

				string? projectQuery = transformedQuery.ExtractProject();

				if (string.IsNullOrEmpty(projectQuery) || this._context.API.FuzzySearch(projectQuery, Settings.NoProjectName).Score > 0)
				{
					projects.Add(new Result
					{
						Title = Settings.NoProjectName,
						IcoPath = IconProvider.StartIcon,
						AutoCompleteText = $"{query.ActionKeyword} {transformedQuery.ReplaceProject(TransformedQuery.PrefixProject(Settings.NoProjectName))}",
						Score = int.MaxValue - 1000,
						Action = context =>
						{
							if (!context.SpecialKeyState.AltPressed)
							{
								this._state.ResultsSource = (null, false);
								this._state.SelectedIds.Project = null;

								this._context.API.ChangeQuery($"{query.ActionKeyword} {transformedQuery.ReplaceProject(string.Empty, withTrailingSpace: true)}", true);
								return false;
							}

							// Alt key modifier will start the time entry now
							Task.Run(async delegate
							{
								long? projectId = null;
								long workspaceId = me.DefaultWorkspaceId;
								string description = transformedQuery.ReplaceProject(string.Empty, escapeIfEmpty: false, unescape: true);

								// Attempt to parse the time span flag if it exists
								TimeSpan startTimeSpan = TimeSpan.Zero;
								if (query.SearchTerms.Contains(Settings.TimeSpanFlag))
								{
									bool success = TimeSpanParser.TryParse(
										new TransformedQuery(query)
											.After(Settings.TimeSpanFlag)
											.ToString(),
										new TimeSpanParserOptions
										{
											UncolonedDefault = Units.Minutes,
											ColonedDefault = Units.Minutes,
										},
										out startTimeSpan
									);
									if (success)
									{
										description = new TransformedQuery(query)
											.To(Settings.TimeSpanFlag)
											.ReplaceProject(string.Empty, escapeIfEmpty: false, unescape: true);
									}
								}

								var startTime = DateTimeOffset.UtcNow + startTimeSpan;

								try
								{
									this._context.API.LogInfo("TogglTrack", $"{projectId}, {workspaceId}, {description}, {startTimeSpan.ToString()}, from project selection");

									var runningTimeEntry = (await this._GetRunningTimeEntry(CancellationToken.None, force: true))?.ToTimeEntry(me);
									if (runningTimeEntry is not null)
									{
										var stoppedTimeEntry = (await this._client.EditTimeEntry(
											workspaceId: runningTimeEntry.WorkspaceId,
											projectId: runningTimeEntry.ProjectId,
											id: runningTimeEntry.Id,
											stop: startTime,
											duration: runningTimeEntry.Duration,
											tags: runningTimeEntry.Tags,
											billable: runningTimeEntry.Billable
										))?.ToTimeEntry(me);

										if (stoppedTimeEntry?.Id is null)
										{
											throw new Exception("An API error was encountered.");
										}
									}

									var createdTimeEntry = (await this._client.CreateTimeEntry(
										workspaceId: workspaceId,
										projectId: projectId,
										description: description,
										start: startTime
									))?.ToTimeEntry(me);

									if (createdTimeEntry?.Id is null)
									{
										throw new Exception("An API error was encountered.");
									}

									this.ShowSuccessMessage(
										(startTimeSpan == TimeSpan.Zero)
											? $"Started {createdTimeEntry.GetRawDescription()}"
											: $"Started {createdTimeEntry.GetRawDescription(withTrailingSpace: true)}{startTime.Humanize()}",
										Settings.NoProjectName,
										IconProvider.StartIcon
									);

									// Update cached running time entry state
									this.RefreshCache();
								}
								catch (Exception exception)
								{
									this._context.API.LogException("TogglTrack", "Failed to start time entry", exception);
									this.ShowErrorMessage("Failed to start time entry.", exception.Message);
								}
								finally
								{
									this._state.ResultsSource = (null, false);
									this._state.SelectedIds.Project = -1;
								}
							});

							return true;
						},
					});
				};

				if (me.ActiveProjects is not null)
				{
					var filteredProjects = (string.IsNullOrEmpty(projectQuery))
						? me.ActiveProjects
						: me.ActiveProjects.Where(project => this._context.API.FuzzySearch(projectQuery, $"{project.Name} {project.Client?.Name ?? string.Empty}").Score > 0);

					projects.AddRange(filteredProjects
						.OrderBy(project => project.ActualHours ?? 0)
						.Select((project, index) => new Result
						{
							Title = project.Name,
							SubTitle = $"{((project.ClientId is not null) ? $"{project.Client!.Name} | " : string.Empty)}{project.ElapsedString}",
							IcoPath = this._iconProvider.GetColourIcon(project.Colour, IconProvider.StartIcon),
							AutoCompleteText = $"{query.ActionKeyword} {transformedQuery.ReplaceProject(TransformedQuery.PrefixProject(project.Name))}",
							Score = index,
							Action = context =>
							{
								if (!context.SpecialKeyState.AltPressed)
								{
									this._state.ResultsSource = (null, false);
									this._state.SelectedIds.Project = project.Id;

									this._context.API.ChangeQuery($"{query.ActionKeyword} {transformedQuery.ReplaceProject(string.Empty, withTrailingSpace: true)}", true);
									return false;
								}

								// Alt key modifier will start the time entry now
								Task.Run(async delegate
								{
									long projectId = project.Id;
									long workspaceId = project.WorkspaceId;
									string description = transformedQuery.ReplaceProject(string.Empty, escapeIfEmpty: false, unescape: true);

									// Attempt to parse the time span flag if it exists
									TimeSpan startTimeSpan = TimeSpan.Zero;
									if (query.SearchTerms.Contains(Settings.TimeSpanFlag))
									{
										bool success = TimeSpanParser.TryParse(
											new TransformedQuery(query)
												.After(Settings.TimeSpanFlag)
												.ToString(),
											new TimeSpanParserOptions
											{
												UncolonedDefault = Units.Minutes,
												ColonedDefault = Units.Minutes,
											},
											out startTimeSpan
										);
										if (success)
										{
											description = new TransformedQuery(query)
												.To(Settings.TimeSpanFlag)
												.ReplaceProject(string.Empty, escapeIfEmpty: false, unescape: true);
										}
									}

									var startTime = DateTimeOffset.UtcNow + startTimeSpan;

									try
									{
										this._context.API.LogInfo("TogglTrack", $"{projectId}, {workspaceId}, {description}, {startTimeSpan.ToString()}, from project selection");

										var runningTimeEntry = (await this._GetRunningTimeEntry(CancellationToken.None, force: true))?.ToTimeEntry(me);
										if (runningTimeEntry is not null)
										{
											var stoppedTimeEntry = (await this._client.EditTimeEntry(
												workspaceId: runningTimeEntry.WorkspaceId,
												projectId: runningTimeEntry.ProjectId,
												id: runningTimeEntry.Id,
												stop: startTime,
												duration: runningTimeEntry.Duration,
												tags: runningTimeEntry.Tags,
												billable: runningTimeEntry.Billable
											))?.ToTimeEntry(me);

											if (stoppedTimeEntry?.Id is null)
											{
												throw new Exception("An API error was encountered.");
											}
										}

										var createdTimeEntry = (await this._client.CreateTimeEntry(
											workspaceId: workspaceId,
											projectId: projectId,
											description: description,
											start: startTime
										))?.ToTimeEntry(me);

										if (createdTimeEntry?.Id is null)
										{
											throw new Exception("An API error was encountered.");
										}

										this.ShowSuccessMessage(
											(startTimeSpan == TimeSpan.Zero)
												? $"Started {createdTimeEntry.GetRawDescription()}"
												: $"Started {createdTimeEntry.GetRawDescription(withTrailingSpace: true)}{startTime.Humanize()}",
											project.WithClientName,
											IconProvider.StartIcon
										);

										// Update cached running time entry state
										this.RefreshCache();
									}
									catch (Exception exception)
									{
										this._context.API.LogException("TogglTrack", "Failed to start time entry", exception);
										this.ShowErrorMessage("Failed to start time entry.", exception.Message);
									}
									finally
									{
										this._state.ResultsSource = (null, false);
										this._state.SelectedIds.Project = -1;
									}
								});

								return true;
							},
						})
					);
				}

				return projects;
			}

			long? projectId = (this._state.SelectedIds.Project == -1)
				? null
				: this._state.SelectedIds.Project;
			var project = me.GetProject(projectId);
			long workspaceId = project?.WorkspaceId ?? me.DefaultWorkspaceId;

			string projectName = project?.WithClientName ?? Settings.NoProjectName;
			string description = transformedQuery.ToString(TransformedQuery.Escaping.Unescaped);

			var results = new List<Result>();

			if (query.SearchTerms.Contains(Settings.ClearDescriptionFlag))
			{
				bool escapeIfEmpty = (this._state.SelectedIds.Project != -1);

				this._context.API.ChangeQuery($"{query.ActionKeyword} {((escapeIfEmpty) ? $"{Settings.EscapeCharacter} " : string.Empty)}");
				return Main.NoResults;
			}
			else if (this._settings.ShowUsageTips && !string.IsNullOrEmpty(query.Search))
			{
				results.Add(new Result
				{
					Title = Settings.UsageTipTitle,
					SubTitle = $"Use {Settings.ClearDescriptionFlag} to quickly clear the description",
					IcoPath = IconProvider.UsageTipIcon,
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.ClearDescriptionFlag} ",
					Score = int.MaxValue - 300000,
					Action = _ =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.ClearDescriptionFlag} ");
						return false;
					}
				});
			}

			if (this._settings.ShowUsageTips)
			{
				if (this._state.SelectedIds.Project == -1)
				{
					results.Add(new Result
					{
						Title = Settings.UsageTipTitle,
						SubTitle = $"Use {Settings.ProjectPrefix} to set the project for this time entry",
						IcoPath = IconProvider.UsageTipIcon,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search}{Settings.ProjectPrefix}",
						Score = int.MaxValue - 150000,
						Action = _ =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search}{Settings.ProjectPrefix}");
							return false;
						}
					});
				}

				if (string.IsNullOrEmpty(description))
				{
					results.Add(new Result
					{
						Title = Settings.UsageTipTitle,
						SubTitle = $"Keep typing to add a time entry description",
						IcoPath = IconProvider.UsageTipIcon,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
						Score = int.MaxValue - 100000,
					});
				}
			}

			if (!query.SearchTerms.Contains(Settings.TimeSpanFlag))
			{
				results.Add(new Result
				{
					Title = $"Start {((string.IsNullOrEmpty(description) ? Settings.EmptyTimeEntry : description))} now",
					TitleHighlightData = (string.IsNullOrEmpty(description))
						? new List<int>()
						: Enumerable.Range("Start ".Length, description.Length).ToList(),
					SubTitle = projectName,
					IcoPath = this._iconProvider.GetColourIcon(project?.Colour, IconProvider.StartIcon),
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Score = int.MaxValue - 10000,
					Action = _ =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{projectId}, {workspaceId}, {description}");

								var runningTimeEntry = (await this._GetRunningTimeEntry(CancellationToken.None, force: true))?.ToTimeEntry(me);
								if (runningTimeEntry is not null)
								{
									var stoppedTimeEntry = (await this._client.StopTimeEntry(
										workspaceId: runningTimeEntry.WorkspaceId,
										id: runningTimeEntry.Id
									))?.ToTimeEntry(me);

									if (stoppedTimeEntry?.Id is null)
									{
										throw new Exception("An API error was encountered.");
									}
								}

								var createdTimeEntry = (await this._client.CreateTimeEntry(
									workspaceId: workspaceId,
									projectId: projectId,
									description: description,
									start: DateTimeOffset.UtcNow
								))?.ToTimeEntry(me);

								if (createdTimeEntry?.Id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this.ShowSuccessMessage($"Started {createdTimeEntry.GetRawDescription()}", projectName, IconProvider.StartIcon);

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to start time entry", exception);
								this.ShowErrorMessage("Failed to start time entry.", exception.Message);
							}
							finally
							{
								this._state.SelectedIds.Project = -1;
							}
						});

						return true;
					},
				});

				if (this._settings.ShowUsageTips)
				{
					results.Add(new Result
					{
						Title = Settings.UsageTipTitle,
						SubTitle = $"Use {Settings.TimeSpanFlag} after the description to specify the start time",
						IcoPath = IconProvider.UsageTipIcon,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ",
						Score = int.MaxValue - 200000,
						Action = _ =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ");
							return false;
						}
					});
				}
			}
			else
			{
				(string queryToFlag, string flagQuery) = new TransformedQuery(query)
					.Split(Settings.TimeSpanFlag)
					.ToStrings();

				bool success = TimeSpanParser.TryParse(
					flagQuery,
					new TimeSpanParserOptions
					{
						UncolonedDefault = Units.Minutes,
						ColonedDefault = Units.Minutes,
					},
					out var startTimeSpan
				);
				if (!success)
				{
					if (this._settings.ShowUsageExamples)
					{
						results.Add(new Result
						{
							Title = Settings.UsageExampleTitle,
							SubTitle = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} -5 mins",
							IcoPath = IconProvider.UsageExampleIcon,
							AutoCompleteText = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} -5 mins",
							Score = int.MaxValue - 1000,
							Action = _ =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} -5 mins");
								return false;
							}
						});
					}

					return results;
				}

				var startTime = DateTimeOffset.UtcNow + startTimeSpan;

				// Remove -t flag from description
				string sanitisedDescription = new TransformedQuery(query)
					.To(Settings.TimeSpanFlag)
					.ToString(TransformedQuery.Escaping.Unescaped);

				results.Add(new Result
				{
					Title = $"Start {((string.IsNullOrEmpty(sanitisedDescription) ? Settings.EmptyTimeEntry : sanitisedDescription))} {startTime.Humanize()} at {startTime.ToLocalTime().ToString("t")}",
					TitleHighlightData = (string.IsNullOrEmpty(sanitisedDescription))
						? new List<int>()
						: Enumerable.Range("Start ".Length, sanitisedDescription.Length).ToList(),
					SubTitle = projectName,
					IcoPath = this._iconProvider.GetColourIcon(project?.Colour, IconProvider.StartIcon),
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Score = int.MaxValue - 1000,
					Action = _ =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{projectId}, {workspaceId}, {sanitisedDescription}, {startTimeSpan.ToString()}, time span flag");

								var runningTimeEntry = (await this._GetRunningTimeEntry(CancellationToken.None, force: true))?.ToTimeEntry(me);
								if (runningTimeEntry is not null)
								{
									var stoppedTimeEntry = (await this._client.EditTimeEntry(
										workspaceId: runningTimeEntry.WorkspaceId,
										projectId: runningTimeEntry.ProjectId,
										id: runningTimeEntry.Id,
										stop: startTime,
										duration: runningTimeEntry.Duration,
										tags: runningTimeEntry.Tags,
										billable: runningTimeEntry.Billable
									))?.ToTimeEntry(me);

									if (stoppedTimeEntry?.Id is null)
									{
										throw new Exception("An API error was encountered.");
									}
								}

								var createdTimeEntry = (await this._client.CreateTimeEntry(
									workspaceId: workspaceId,
									projectId: projectId,
									description: sanitisedDescription,
									start: startTime
								))?.ToTimeEntry(me);

								if (createdTimeEntry?.Id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this.ShowSuccessMessage($"Started {createdTimeEntry.GetRawDescription(withTrailingSpace: true)}{startTime.Humanize()}", projectName, IconProvider.StartIcon);

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to start time entry", exception);
								this.ShowErrorMessage("Failed to start time entry.", exception.Message);
							}
							finally
							{
								this._state.SelectedIds.Project = -1;
							}
						});

						return true;
					},
				});

				return results;
			}

			// Use cached time entries here to ensure responsiveness
			var likelyPastTimeEntry = (await this._GetTimeEntries(token))?.FirstOrDefault()?.ToTimeEntry(me);
			if ((likelyPastTimeEntry is null) || (likelyPastTimeEntry.Stop is null))
			{
				return results;
			}

			results.Add(new Result
			{
				Title = $"Start {((string.IsNullOrEmpty(description) ? Settings.EmptyTimeEntry : description))} {likelyPastTimeEntry.HumanisedStop} at previous stop time",
				TitleHighlightData = (string.IsNullOrEmpty(description))
					? new List<int>()
					: Enumerable.Range("Start ".Length, description.Length).ToList(),
				SubTitle = projectName,
				IcoPath = this._iconProvider.GetColourIcon(project?.Colour, IconProvider.StartIcon),
				AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
				Score = int.MaxValue - 10000,
				Action = _ =>
				{
					Task.Run(async delegate
					{
						try
						{
							this._context.API.LogInfo("TogglTrack", $"{projectId}, {workspaceId}, {description}, at previous stop time");

							// Force a new fetch to ensure correctness
							// User input has ended at this point so no responsiveness concerns
							var lastTimeEntry = (await this._GetTimeEntries(CancellationToken.None, force: true))?.FirstOrDefault()?.ToTimeEntry(me);
							if (lastTimeEntry is null)
							{
								throw new Exception("There is no previous time entry.");
							}
							else if (lastTimeEntry.StopDate is null)
							{
								throw new Exception("A time entry is currently running.");
							}

							var createdTimeEntry = (await this._client.CreateTimeEntry(
								workspaceId: workspaceId,
								projectId: projectId,
								description: description,
								start: (DateTimeOffset)lastTimeEntry.StopDate
							))?.ToTimeEntry(me);

							if (createdTimeEntry?.Id is null)
							{
								throw new Exception("An API error was encountered.");
							}

							this.ShowSuccessMessage($"Started {createdTimeEntry.GetRawDescription(withTrailingSpace: true)}at previous stop time", $"{projectName} | {createdTimeEntry.DetailedElapsed}", IconProvider.StartIcon);

							// Update cached running time entry state
							this.RefreshCache();
						}
						catch (Exception exception)
						{
							this._context.API.LogException("TogglTrack", "Failed to start time entry at previous stop time", exception);
							this.ShowErrorMessage("Failed to start time entry.", exception.Message);
						}
						finally
						{
							this._state.SelectedIds.Project = -1;
						}
					});

					return true;
				},
			});

			return results;
		}

		private async ValueTask<List<Result>> _GetStopResults(CancellationToken token, Query query)
		{
			var me = (await this._GetMe(token))?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var runningTimeEntry = (await this._GetRunningTimeEntry(token))?.ToTimeEntry(me);
			if (runningTimeEntry is null)
			{
				return new List<Result>
				{
					new Result
					{
						Title = $"No running time entry",
						SubTitle = "There is no current time entry to stop.",
						IcoPath = this._context.CurrentPluginMetadata.IcoPath,
						Action = _ =>
						{
							return true;
						},
					},
				};
			}

			string projectName = runningTimeEntry.Project?.WithClientName ?? Settings.NoProjectName;

			var results = new List<Result>();

			if (!query.SearchTerms.Contains(Settings.TimeSpanEndFlag))
			{
				results.Add(new Result
				{
					Title = $"Stop {runningTimeEntry.GetDescription()} now",
					TitleHighlightData = (runningTimeEntry.GetDescription() == Settings.EmptyDescription)
						? new List<int>()
						: Enumerable.Range("Stop ".Length, runningTimeEntry.GetDescription().Length).ToList(),
					SubTitle = $"{projectName} | {runningTimeEntry.HumanisedElapsed} ({runningTimeEntry.DetailedElapsed})",
					IcoPath = this._iconProvider.GetColourIcon(runningTimeEntry.Project?.Colour, IconProvider.StopIcon),
					AutoCompleteText = $"{query.ActionKeyword} {Settings.StopCommand} {runningTimeEntry.GetDescription(escapePotentialSymbols: true)} ",
					Score = 10000,
					Action = _ =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._state.SelectedIds.Project}, {runningTimeEntry.Id}, {runningTimeEntry.WorkspaceId}, {runningTimeEntry.StartDate}, {runningTimeEntry.DetailedElapsed}");

								var stoppedTimeEntry = (await this._client.StopTimeEntry(
									workspaceId: runningTimeEntry.WorkspaceId,
									id: runningTimeEntry.Id
								))?.ToTimeEntry(me);

								if (stoppedTimeEntry?.Id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this.ShowSuccessMessage($"Stopped {stoppedTimeEntry.GetRawDescription()}", $"{runningTimeEntry.DetailedElapsed} elapsed", IconProvider.StopIcon);

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to stop time entry", exception);
								this.ShowErrorMessage("Failed to stop time entry.", exception.Message);
							}
						});

						return true;
					},
				});

				if (!this._settings.ShowUsageTips)
				{
					return results;
				}

				results.Add(new Result
				{
					Title = Settings.UsageTipTitle,
					SubTitle = $"Use {Settings.TimeSpanEndFlag} to specify the stop time",
					IcoPath = IconProvider.UsageTipIcon,
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.TimeSpanEndFlag} ",
					Score = 1,
					Action = _ =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.TimeSpanEndFlag} ");
						return false;
					}
				});

				return results;
			}

			bool success = TimeSpanParser.TryParse(
				new TransformedQuery(query)
					.After(Settings.TimeSpanEndFlag)
					.ToString(),
				new TimeSpanParserOptions
				{
					UncolonedDefault = Units.Minutes,
					ColonedDefault = Units.Minutes,
				},
				out var stopTimeSpan
			);
			if (!success)
			{
				if (this._settings.ShowUsageExamples)
				{
					results.Add(new Result
					{
						Title = Settings.UsageExampleTitle,
						SubTitle = $"{query.ActionKeyword} {Settings.StopCommand} {Settings.TimeSpanEndFlag} -5 mins",
						IcoPath = IconProvider.UsageExampleIcon,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} -5 mins",
						Score = 100000,
						Action = _ =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} -5 mins");
							return false;
						}
					});
				}

				return results;
			}

			var stopTime = DateTimeOffset.UtcNow + stopTimeSpan;
			if (stopTime < runningTimeEntry.StartDate)
			{
				// Ensure stop is not before start
				stopTime = runningTimeEntry.StartDate;
			}

			var newElapsed = stopTime.Subtract(runningTimeEntry.StartDate);

			results.Add(new Result
			{
				Title = $"Stop {runningTimeEntry.GetDescription()} {stopTime.Humanize()} at {stopTime.ToLocalTime().ToString("t")}",
				TitleHighlightData = (runningTimeEntry.GetDescription() == Settings.EmptyDescription)
					? new List<int>()
					: Enumerable.Range("Stop ".Length, runningTimeEntry.GetDescription().Length).ToList(),
				SubTitle = $"{projectName} | {newElapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")})",
				IcoPath = this._iconProvider.GetColourIcon(runningTimeEntry.Project?.Colour, IconProvider.StopIcon),
				AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
				Score = 100000,
				Action = _ =>
				{
					Task.Run(async delegate
					{
						try
						{
							this._context.API.LogInfo("TogglTrack", $"{this._state.SelectedIds.Project}, {runningTimeEntry.Id}, {runningTimeEntry.WorkspaceId}, {runningTimeEntry.StartDate}, {(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")}, {stopTime}, time span flag");

							var stoppedTimeEntry = (await this._client.EditTimeEntry(
								workspaceId: runningTimeEntry.WorkspaceId,
								projectId: runningTimeEntry.ProjectId,
								id: runningTimeEntry.Id,
								stop: stopTime,
								duration: runningTimeEntry.Duration,
								tags: runningTimeEntry.Tags,
								billable: runningTimeEntry.Billable
							))?.ToTimeEntry(me);

							if (stoppedTimeEntry?.Id is null)
							{
								throw new Exception("An API error was encountered.");
							}

							this.ShowSuccessMessage($"Stopped {stoppedTimeEntry.GetRawDescription()}", $"{(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")} elapsed", IconProvider.StopIcon);

							// Update cached running time entry state
							this.RefreshCache();
						}
						catch (Exception exception)
						{
							this._context.API.LogException("TogglTrack", "Failed to stop time entry", exception);
							this.ShowErrorMessage("Failed to stop time entry.", exception.Message);
						}
					});

					return true;
				},
			});

			return results;
		}

		private async ValueTask<List<Result>> _GetContinueResults(CancellationToken token, Query query)
		{
			if (string.IsNullOrEmpty(query.Search) || Settings.Commands.Any(command => command.StartsWith(query.Search)))
			{
				return Main.NoResults;
			}

			var me = (await this._GetMe(token))?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var timeEntries = (await this._GetMaxReportTimeEntries(token))?.ToSummaryReport(me);
			if (timeEntries is null)
			{
				return Main.NoResults;
			}

			string entriesQuery = new TransformedQuery(query)
				.RemoveAll(Settings.ListPastFlag)
				.ToString(TransformedQuery.Escaping.Unescaped);

			bool emptyQuery = string.IsNullOrEmpty(entriesQuery);

			return timeEntries.Groups.Values.SelectMany(project =>
			{
				if (project.SubGroups is null)
				{
					return Enumerable.Empty<Result>();
				}

				return project.SubGroups.Values
					.Where(timeEntry => emptyQuery || (
						(
							project.Project?.Id != this._state.SelectedIds.Project ||
							timeEntry.GetRawTitle() != entriesQuery
						) &&
						(
							this._context.API.FuzzySearch(entriesQuery, timeEntry.GetTitle()).Score > 0)
						)
					)
					.Select(timeEntry => new Result
					{
						Title = timeEntry.GetTitle(),
						SubTitle = $"{project.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.HumanisedElapsed}",
						IcoPath = this._iconProvider.GetColourIcon(project.Project?.Colour, IconProvider.ContinueIcon),
						AutoCompleteText = $"{query.ActionKeyword} {timeEntry.GetTitle(escapeCommands: true, escapePotentialSymbols: true)}",
						Score = timeEntry.GetScoreByStart(),
						Action = context =>
						{
							if (!context.SpecialKeyState.AltPressed)
							{
								this._state.SelectedIds.Project = project.Project?.Id;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {timeEntry.GetRawTitle(withTrailingSpace: true, escapeCommands: true, escapePotentialSymbols: true)}");
								return false;
							}

							// Alt key modifier will continue the time entry now
							Task.Run(async delegate
							{
								long workspaceId = project.Project?.WorkspaceId ?? me.DefaultWorkspaceId;

								try
								{
									this._context.API.LogInfo("TogglTrack", $"{project.Id}, {workspaceId}, {timeEntry.GetRawTitle()}");

									var runningTimeEntry = (await this._GetRunningTimeEntry(CancellationToken.None, force: true))?.ToTimeEntry(me);
									if (runningTimeEntry is not null)
									{
										var stoppedTimeEntry = (await this._client.StopTimeEntry(
											workspaceId: runningTimeEntry.WorkspaceId,
											id: runningTimeEntry.Id
										))?.ToTimeEntry(me);

										if (stoppedTimeEntry?.Id is null)
										{
											throw new Exception("An API error was encountered.");
										}
									}

									var createdTimeEntry = (await this._client.CreateTimeEntry(
										workspaceId: workspaceId,
										projectId: project.Project?.Id,
										description: timeEntry.GetRawTitle(),
										start: DateTimeOffset.UtcNow
									))?.ToTimeEntry(me);

									if (createdTimeEntry?.Id is null)
									{
										throw new Exception("An API error was encountered.");
									}

									this.ShowSuccessMessage($"Continued {createdTimeEntry.GetRawDescription()}", project.Project?.WithClientName ?? Settings.NoProjectName, IconProvider.StartIcon);

									// Update cached running time entry state
									this.RefreshCache();
								}
								catch (Exception exception)
								{
									this._context.API.LogException("TogglTrack", "Failed to continue time entry", exception);
									this.ShowErrorMessage("Failed to continue time entry.", exception.Message);
								}
							});

							return true;
						}
					});
			}).ToList();
		}

		private async ValueTask<List<Result>> _GetEditResults(CancellationToken token, Query query)
		{
			var me = (await this._GetMe(token))?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var timeEntries = (await this._GetTimeEntries(token))?.ConvertAll(timeEntry => timeEntry.ToTimeEntry(me));
			var maxTimeEntries = (await this._GetMaxReportTimeEntries(token))?.ToSummaryReport(me);
			if (timeEntries is null || maxTimeEntries is null)
			{
				return new List<Result>
				{
					new Result
					{
						Title = $"No previous time entries",
						SubTitle = "There are no previous time entries to edit.",
						IcoPath = this._context.CurrentPluginMetadata.IcoPath,
						Action = _ =>
						{
							return true;
						},
					},
				};
			}

			var ArgumentIndices = new
			{
				Command = 0,
				Description = 1,
			};

			if (this._state.SelectedIds.TimeEntry == -1)
			{
				string entriesQuery = new TransformedQuery(query)
					.After(ArgumentIndices.Description)
					.ToString();
				var filteredTimeEntries = (string.IsNullOrEmpty(entriesQuery))
					? timeEntries
					: timeEntries.FindAll(timeEntry => this._context.API.FuzzySearch(entriesQuery, timeEntry.GetDescription()).Score > 0);

				return filteredTimeEntries.ConvertAll(timeEntry => new Result
				{
					Title = timeEntry.GetDescription(),
					SubTitle = $"{timeEntry.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.HumanisedElapsed} ({timeEntry.HumanisedStart})",
					IcoPath = this._iconProvider.GetColourIcon(timeEntry.Project?.Colour, IconProvider.EditIcon),
					AutoCompleteText = $"{query.ActionKeyword} {Settings.EditCommand} {timeEntry.GetDescription(escapePotentialSymbols: true)}",
					Score = timeEntry.GetScoreByStart(),
					Action = _ =>
					{
						this._state.SelectedIds.TimeEntry = timeEntry.Id;
						this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialSymbols: true)}", true);
						return false;
					},
				});
			}

			var timeEntry = timeEntries.Find(timeEntry => timeEntry.Id == this._state.SelectedIds.TimeEntry);
			if (timeEntry is null)
			{
				return this.NotifyUnknownError();
			}

			var transformedQuery = new TransformedQuery(query)
				.After(ArgumentIndices.Description);

			if (transformedQuery.HasProjectPrefix())
			{
				var projects = new List<Result>();

				string? projectQuery = transformedQuery.ExtractProject();

				if (string.IsNullOrEmpty(projectQuery) || this._context.API.FuzzySearch(projectQuery, "No Project").Score > 0)
				{
					projects.Add(new Result
					{
						Title = Settings.NoProjectName,
						IcoPath = IconProvider.EditIcon,
						AutoCompleteText = $"{query.ActionKeyword} {Settings.EditCommand} {transformedQuery.ReplaceProject(TransformedQuery.PrefixProject(Settings.NoProjectName), escapeIfEmpty: false)}",
						Score = int.MaxValue - 1000,
						Action = _ =>
						{
							this._state.SelectedIds.Project = null;

							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} {transformedQuery.ReplaceProject(string.Empty, escapeIfEmpty: false, withTrailingSpace: true)}", true);
							return false;
						},
					});
				};

				if (me.ActiveProjects is not null)
				{
					var filteredProjects = (string.IsNullOrEmpty(projectQuery))
						? me.ActiveProjects
						: me.ActiveProjects.Where(project => this._context.API.FuzzySearch(projectQuery, $"{project.Name} {project.Client?.Name ?? string.Empty}").Score > 0);

					projects.AddRange(filteredProjects
						.OrderBy(project => project.ActualHours ?? 0)
						.Select((project, index) => new Result
						{
							Title = project.Name,
							SubTitle = $"{((project.ClientId is not null) ? $"{project.Client!.Name} | " : string.Empty)}{project.ElapsedString}",
							IcoPath = this._iconProvider.GetColourIcon(project.Colour, IconProvider.EditIcon),
							AutoCompleteText = $"{query.ActionKeyword} {Settings.EditCommand} {transformedQuery.ReplaceProject(TransformedQuery.PrefixProject(project.Name), escapeIfEmpty: false)}",
							Score = index,
							Action = _ =>
							{
								this._state.SelectedIds.Project = project.Id;

								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} {transformedQuery.ReplaceProject(string.Empty, escapeIfEmpty: false, withTrailingSpace: true)}", true);
								return false;
							},
						})
					);
				}

				return projects;
			}

			var results = new List<Result>();

			if (this._settings.ShowUsageTips && this._state.SelectedIds.Project == -1)
			{
				results.Add(new Result
				{
					Title = Settings.UsageTipTitle,
					SubTitle = $"Use {Settings.ProjectPrefix} to edit the project for this time entry",
					IcoPath = IconProvider.UsageTipIcon,
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.ProjectPrefix}",
					Score = 100,
					Action = _ =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.ProjectPrefix}");
						return false;
					}
				});
			}

			if (query.SearchTerms.Contains(Settings.ClearDescriptionFlag))
			{
				string queryToDescription = new TransformedQuery(query)
					.To(ArgumentIndices.Description)
					.ToString();

				this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToDescription} ");
				return Main.NoResults;
			}
			else if (this._settings.ShowUsageTips)
			{
				results.Add(new Result
				{
					Title = Settings.UsageTipTitle,
					SubTitle = $"Use {Settings.ClearDescriptionFlag} to quickly clear the description",
					IcoPath = IconProvider.UsageTipIcon,
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.ClearDescriptionFlag} ",
					Score = 10,
					Action = _ =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.ClearDescriptionFlag} ");
						return false;
					}
				});
			}

			long? projectId = (this._state.SelectedIds.Project == -1)
				? timeEntry.ProjectId
				: this._state.SelectedIds.Project;
			var project = me.GetProject(projectId);

			string projectName = project?.WithClientName ?? Settings.NoProjectName;
			string description = transformedQuery
				.ToString(TransformedQuery.Escaping.Unescaped);

			if (this._settings.ShowUsageWarnings && string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(timeEntry.GetRawDescription()))
			{
				results.Add(new Result
				{
					Title = Settings.UsageWarningTitle,
					SubTitle = $"Time entry description will be cleared if nothing is entered!",
					IcoPath = IconProvider.UsageWarningIcon,
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialSymbols: true)}",
					Score = 1000,
					Action = _ =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialSymbols: true)}");
						return false;
					}
				});
			}

			var timeSpanFlags = new string[] { Settings.TimeSpanFlag, Settings.TimeSpanEndFlag };
			bool hasTimeSpanFlag = query.SearchTerms.Contains(Settings.TimeSpanFlag);
			bool hasTimeSpanEndFlag = query.SearchTerms.Contains(Settings.TimeSpanEndFlag);
			bool hasResumeFlag = query.SearchTerms.Contains(Settings.ResumeFlag);

			if (this._settings.ShowUsageTips)
			{
				if (!hasTimeSpanFlag)
				{
					results.Add(new Result
					{
						Title = Settings.UsageTipTitle,
						SubTitle = $"Use {Settings.TimeSpanFlag} after the description to edit the start time",
						IcoPath = IconProvider.UsageTipIcon,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ",
						Score = 70,
						Action = _ =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ");
							return false;
						}
					});
				}

				if (!hasTimeSpanEndFlag && !hasResumeFlag)
				{
					results.Add(new Result
					{
						Title = Settings.UsageTipTitle,
						SubTitle = $"Use {Settings.TimeSpanEndFlag} after the description to edit the stop time",
						IcoPath = IconProvider.UsageTipIcon,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.TimeSpanEndFlag} ",
						Score = 50,
						Action = _ =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.TimeSpanEndFlag} ");
							return false;
						}
					});
				}

				if (!timeEntry.IsRunning && !hasResumeFlag && !hasTimeSpanEndFlag)
				{
					results.Add(new Result
					{
						Title = Settings.UsageTipTitle,
						SubTitle = $"Use {Settings.ResumeFlag} to resume this time entry",
						IcoPath = IconProvider.UsageTipIcon,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.ResumeFlag} ",
						Score = 30,
						Action = _ =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.ResumeFlag} ");
							return false;
						}
					});
				}
			}

			if (!hasTimeSpanFlag && !hasTimeSpanEndFlag && !hasResumeFlag)
			{
				results.Add(new Result
				{
					Title = (string.IsNullOrEmpty(description))
						? timeEntry.GetDescription()
						: description,
					SubTitle = $"{projectName} | {timeEntry.HumanisedElapsed} ({timeEntry.DetailedElapsed})",
					IcoPath = this._iconProvider.GetColourIcon(project?.Colour, IconProvider.EditIcon),
					AutoCompleteText = $"{query.ActionKeyword} {(string.IsNullOrEmpty(description) ? ($"{query.Search} {timeEntry.GetDescription(escapePotentialSymbols: true)}") : query.Search)} ",
					Score = int.MaxValue - 1000,
					Action = _ =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{projectId}, {timeEntry.Id}, {timeEntry.Duration}, {timeEntry.Start}, {timeEntry.WorkspaceId}, {description}");

								var editedTimeEntry = (await this._client.EditTimeEntry(
									workspaceId: timeEntry.WorkspaceId,
									projectId: projectId,
									id: timeEntry.Id,
									description: description,
									duration: timeEntry.Duration,
									tags: timeEntry.Tags,
									billable: timeEntry.Billable
								))?.ToTimeEntry(me);

								if (editedTimeEntry?.Id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this.ShowSuccessMessage($"Edited {editedTimeEntry.GetRawDescription()}", $"{projectName} | {timeEntry.DetailedElapsed}", IconProvider.EditIcon);

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to edit time entry", exception);
								this.ShowErrorMessage("Failed to edit time entry.", exception.Message);
							}
							finally
							{
								this._state.SelectedIds.TimeEntry = -1;
								this._state.SelectedIds.Project = -1;
							}
						});

						return true;
					},
				});

				if (!string.IsNullOrEmpty(description))
				{
					results.AddRange(maxTimeEntries.Groups.Values.SelectMany(pastProject =>
					{
						if (pastProject.SubGroups is null)
						{
							return Enumerable.Empty<Result>();
						}

						return pastProject.SubGroups.Values
							.Where(pastTimeEntry => (
									(
										pastProject.Project?.Id != timeEntry.Project?.Id ||
										pastTimeEntry.GetRawTitle() != description
									) &&
									(
										this._context.API.FuzzySearch(description, pastTimeEntry.GetTitle()).Score > 0
									)
								)
							)
							.Select(pastTimeEntry => new Result
							{
								Title = pastTimeEntry.GetTitle(),
								SubTitle = $"{pastProject.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.HumanisedElapsed} ({timeEntry.DetailedElapsed})",
								IcoPath = this._iconProvider.GetColourIcon(pastProject.Project?.Colour, IconProvider.EditIcon),
								AutoCompleteText = $"{query.ActionKeyword} {pastTimeEntry.GetTitle(escapePotentialSymbols: true)}",
								Score = pastTimeEntry.GetScoreByStart(),
								Action = _ =>
								{
									Task.Run(async delegate
									{
										try
										{
											this._context.API.LogInfo("TogglTrack", $"past time entry {pastProject.Project?.Id}, {pastTimeEntry.Id}, {timeEntry.Duration}, {timeEntry.Start}, {projectId}, {timeEntry.WorkspaceId}, {description}, {pastTimeEntry.GetTitle()}");

											var editedTimeEntry = (await this._client.EditTimeEntry(
												workspaceId: timeEntry.WorkspaceId,
												projectId: pastProject.Project?.Id,
												id: timeEntry.Id,
												description: pastTimeEntry.GetRawTitle(),
												duration: timeEntry.Duration,
												tags: timeEntry.Tags,
												billable: timeEntry.Billable
											))?.ToTimeEntry(me);

											if (editedTimeEntry?.Id is null)
											{
												throw new Exception("An API error was encountered.");
											}

											this.ShowSuccessMessage($"Edited {editedTimeEntry.GetRawDescription()}", $"{pastProject.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.DetailedElapsed}", IconProvider.EditIcon);

											// Update cached running time entry state
											this.RefreshCache();
										}
										catch (Exception exception)
										{
											this._context.API.LogException("TogglTrack", "Failed to edit time entry", exception);
											this.ShowErrorMessage("Failed to edit time entry.", exception.Message);
										}
										finally
										{
											this._state.SelectedIds.TimeEntry = -1;
											this._state.SelectedIds.Project = -1;
										}
									});

									return true;
								},
							});
					}));
				}
			}
			else
			{
				// TimeSpanFlag and/or TimeSpanEndFlag and/or ResumeFlag is present
				int firstFlag = Array.IndexOf(query.SearchTerms, Settings.ResumeFlag);
				for (int i = 0; i < query.SearchTerms.Length; i++)
				{
					if (!timeSpanFlags.Contains(query.SearchTerms[i]))
					{
						continue;
					}

					firstFlag = i;
					break;
				}

				if (hasTimeSpanEndFlag && hasResumeFlag)
				{
					string sanitisedQuery = new TransformedQuery(query)
						.To(Settings.TimeSpanEndFlag)
						.RemoveAll(Settings.ResumeFlag)
						.ToString();

					results.Add(new Result
					{
						Title = $"{Settings.ErrorPrefix}Conflicting flags",
						SubTitle = $"You may not use both {Settings.TimeSpanEndFlag} and {Settings.ResumeFlag} at the same time.",
						IcoPath = IconProvider.UsageErrorIcon,
						AutoCompleteText = $"{query.ActionKeyword} {sanitisedQuery} ",
						Score = 300000,
						Action = _ =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {sanitisedQuery} ");
							return false;
						}
					});

					return results;
				}

				TimeSpan? startTimeSpan = null;
				TimeSpan? endTimeSpan = null;
				TimeSpan newElapsed = timeEntry.Elapsed;

				if (hasTimeSpanFlag)
				{
					(string queryToFlag, string flagQuery) = new TransformedQuery(query)
						.Split(Settings.TimeSpanFlag)
						.ToStrings();

					bool success = TimeSpanParser.TryParse(
						flagQuery,
						new TimeSpanParserOptions
						{
							UncolonedDefault = Units.Minutes,
							ColonedDefault = Units.Minutes,
						},
						out var parsedStartTimeSpan
					);
					if (success)
					{
						startTimeSpan = parsedStartTimeSpan;
						newElapsed -= parsedStartTimeSpan;
					}
					else
					{
						if (this._settings.ShowUsageExamples)
						{
							results.Add(new Result
							{
								Title = Settings.UsageExampleTitle,
								SubTitle = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} 5 mins",
								IcoPath = IconProvider.UsageExampleIcon,
								AutoCompleteText = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} 5 mins",
								Score = 100000,
								Action = _ =>
								{
									this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} 5 mins");
									return false;
								}
							});
						}

						return results;
					}
				}
				if (hasTimeSpanEndFlag)
				{
					(string queryToFlag, string flagQuery) = new TransformedQuery(query)
						.Split(Settings.TimeSpanEndFlag)
						.ToStrings();

					bool success = TimeSpanParser.TryParse(
						flagQuery,
						new TimeSpanParserOptions
						{
							UncolonedDefault = Units.Minutes,
							ColonedDefault = Units.Minutes,
						},
						out var parsedEndTimeSpan
					);
					if (success)
					{
						endTimeSpan = parsedEndTimeSpan;
						newElapsed += parsedEndTimeSpan;
					}
					else
					{
						if (this._settings.ShowUsageExamples)
						{
							results.Add(new Result
							{
								Title = Settings.UsageExampleTitle,
								SubTitle = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanEndFlag} 5 mins",
								IcoPath = IconProvider.UsageExampleIcon,
								AutoCompleteText = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanEndFlag} 5 mins",
								Score = 100000,
								Action = _ =>
								{
									this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanEndFlag} 5 mins");
									return false;
								}
							});
						}

						return results;
					}
				}

				// Remove flags from description
				string sanitisedDescription = new TransformedQuery(query)
					.Between(ArgumentIndices.Description, firstFlag)
					.RemoveAll(Settings.ResumeFlag)
					.ToString(TransformedQuery.Escaping.Unescaped);

				if (this._settings.ShowUsageWarnings && string.IsNullOrEmpty(sanitisedDescription) && !string.IsNullOrEmpty(timeEntry.GetRawDescription()))
				{
					results.Add(new Result
					{
						Title = Settings.UsageWarningTitle,
						SubTitle = $"Time entry description will be cleared if nothing is entered!",
						IcoPath = IconProvider.UsageWarningIcon,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialSymbols: true)}",
						Score = 1000,
						Action = _ =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialSymbols: true)}");
							return false;
						}
					});
				}

				var startTime = (timeEntry.StartDate + startTimeSpan) ?? timeEntry.StartDate;
				var stopTime = (!hasResumeFlag)
					? ((timeEntry.StopDate ?? DateTimeOffset.UtcNow) + endTimeSpan) ?? timeEntry.StopDate
					: null;

				if (hasResumeFlag)
				{
					newElapsed = DateTimeOffset.UtcNow.Subtract(startTime);
				}

				results.Add(new Result
				{
					Title = (string.IsNullOrEmpty(sanitisedDescription))
						? timeEntry.GetDescription()
						: sanitisedDescription,
					SubTitle = $"{projectName} | {newElapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")})",
					IcoPath = this._iconProvider.GetColourIcon(project?.Colour, IconProvider.EditIcon),
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Score = int.MaxValue - 1000,
					Action = _ =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{projectId}, {timeEntry.Id}, {timeEntry.Duration}, {timeEntry.Start}, {timeEntry.WorkspaceId}, {sanitisedDescription}, {startTime.ToString("yyyy-MM-ddTHH:mm:ssZ")}, {startTimeSpan.ToString()}, {stopTime?.ToString("yyyy-MM-ddTHH:mm:ssZ")}, {endTimeSpan.ToString()}, edit start time");

								var editedTimeEntry = (await this._client.EditTimeEntry(
									workspaceId: timeEntry.WorkspaceId,
									projectId: projectId,
									id: timeEntry.Id,
									description: sanitisedDescription,
									start: startTime,
									stop: stopTime,
									duration: timeEntry.Duration,
									tags: timeEntry.Tags,
									billable: timeEntry.Billable
								))?.ToTimeEntry(me);

								if (editedTimeEntry?.Id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this.ShowSuccessMessage($"Edited {editedTimeEntry.GetRawDescription()}", $"{projectName} | {(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")}", IconProvider.EditIcon);

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to edit time entry", exception);
								this.ShowErrorMessage("Failed to edit time entry.", exception.Message);
							}
							finally
							{
								this._state.SelectedIds.TimeEntry = -1;
								this._state.SelectedIds.Project = -1;
							}
						});

						return true;
					},
				});

				if (!string.IsNullOrEmpty(sanitisedDescription))
				{
					results.AddRange(maxTimeEntries.Groups.Values.SelectMany(pastProject =>
					{
						if (pastProject.SubGroups is null)
						{
							return Enumerable.Empty<Result>();
						}

						return pastProject.SubGroups.Values
							.Where(pastTimeEntry => (
									(
										pastProject.Project?.Id != timeEntry.Project?.Id ||
										pastTimeEntry.GetRawTitle() != sanitisedDescription
									) &&
									(
										this._context.API.FuzzySearch(sanitisedDescription, pastTimeEntry.GetTitle()).Score > 0
									)
								)
							)
							.Select(pastTimeEntry => new Result
							{
								Title = pastTimeEntry.GetTitle(),
								SubTitle = $"{pastProject.Project?.WithClientName ?? Settings.NoProjectName} | {newElapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")})",
								IcoPath = this._iconProvider.GetColourIcon(pastProject.Project?.Colour, IconProvider.EditIcon),
								AutoCompleteText = $"{query.ActionKeyword} {pastTimeEntry.GetTitle(escapePotentialSymbols: true)}",
								Score = pastTimeEntry.GetScoreByStart(),
								Action = _ =>
								{
									Task.Run(async delegate
									{
										try
										{
											this._context.API.LogInfo("TogglTrack", $"past time entry {pastProject.Project?.Id}, {pastTimeEntry.Id}, {timeEntry.Duration}, {timeEntry.Start}, {projectId}, {timeEntry.WorkspaceId}, {sanitisedDescription}, {pastTimeEntry.GetTitle()}");

											var editedTimeEntry = (await this._client.EditTimeEntry(
												workspaceId: timeEntry.WorkspaceId,
												projectId: pastProject.Project?.Id,
												id: timeEntry.Id,
												description: pastTimeEntry.GetRawTitle(),
												start: startTime,
												stop: stopTime,
												duration: timeEntry.Duration,
												tags: timeEntry.Tags,
												billable: timeEntry.Billable
											))?.ToTimeEntry(me);

											if (editedTimeEntry?.Id is null)
											{
												throw new Exception("An API error was encountered.");
											}

											this.ShowSuccessMessage($"Edited {editedTimeEntry.GetRawDescription()}", $"{pastProject.Project?.WithClientName ?? Settings.NoProjectName} | {(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")}", IconProvider.EditIcon);

											// Update cached running time entry state
											this.RefreshCache();
										}
										catch (Exception exception)
										{
											this._context.API.LogException("TogglTrack", "Failed to edit time entry", exception);
											this.ShowErrorMessage("Failed to edit time entry.", exception.Message);
										}
										finally
										{
											this._state.SelectedIds.TimeEntry = -1;
											this._state.SelectedIds.Project = -1;
										}
									});

									return true;
								},
							});
					}));
				}
			}

			return results;
		}

		private async ValueTask<List<Result>> _GetDeleteResults(CancellationToken token, Query query)
		{
			var me = (await this._GetMe(token))?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var timeEntries = (await this._GetTimeEntries(token))?.ConvertAll(timeEntry => timeEntry.ToTimeEntry(me));
			if (timeEntries is null)
			{
				return new List<Result>
				{
					new Result
					{
						Title = $"No previous time entries",
						SubTitle = "There are no previous time entries to delete.",
						IcoPath = this._context.CurrentPluginMetadata.IcoPath,
						Action = _ =>
						{
							return true;
						},
					},
				};
			}

			var ArgumentIndices = new
			{
				Command = 0,
				Description = 1,
			};

			if (this._state.SelectedIds.TimeEntry == -1)
			{
				string entriesQuery = new TransformedQuery(query)
					.After(ArgumentIndices.Description)
					.ToString();
				var filteredTimeEntries = (string.IsNullOrEmpty(entriesQuery))
					? timeEntries
					: timeEntries.FindAll(timeEntry => this._context.API.FuzzySearch(entriesQuery, timeEntry.GetDescription()).Score > 0);

				return filteredTimeEntries.ConvertAll(timeEntry => new Result
				{
					Title = timeEntry.GetDescription(),
					SubTitle = $"{timeEntry.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.HumanisedElapsed} ({timeEntry.HumanisedStart})",
					IcoPath = this._iconProvider.GetColourIcon(timeEntry.Project?.Colour, IconProvider.DeleteIcon),
					AutoCompleteText = $"{query.ActionKeyword} {Settings.DeleteCommand} {timeEntry.GetDescription(escapePotentialSymbols: true)}",
					Score = timeEntry.GetScoreByStart(),
					Action = _ =>
					{
						this._state.SelectedIds.TimeEntry = timeEntry.Id;
						this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.DeleteCommand} ", true);
						return false;
					},
				});
			}

			var timeEntry = timeEntries.Find(timeEntry => timeEntry.Id == this._state.SelectedIds.TimeEntry);
			if (timeEntry is null)
			{
				return this.NotifyUnknownError();
			}

			return new List<Result>
			{
				new Result
				{
					Title = $"Delete {timeEntry.GetDescription()}",
					TitleHighlightData = (timeEntry.GetDescription() == Settings.EmptyDescription)
						? new List<int>()
						: Enumerable.Range("Delete ".Length, timeEntry.GetDescription().Length).ToList(),
					SubTitle = $"{timeEntry.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.HumanisedElapsed} ({timeEntry.DetailedElapsed})",
					IcoPath = this._iconProvider.GetColourIcon(timeEntry.Project?.Colour, IconProvider.DeleteIcon) ,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} {timeEntry.GetDescription(escapePotentialSymbols: true)}",
					Action = _ =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._state.SelectedIds.Project}, {timeEntry.Id}, {timeEntry.WorkspaceId}, {timeEntry.StartDate}, {timeEntry.DetailedElapsed}");

								var statusCode = await this._client.DeleteTimeEntry(
									workspaceId: timeEntry.WorkspaceId,
									id: timeEntry.Id
								);

								if (statusCode is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this.ShowSuccessMessage($"Deleted {timeEntry.GetRawDescription()}", $"{timeEntry.DetailedElapsed} elapsed", IconProvider.DeleteIcon);

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to delete time entry", exception);
								this.ShowErrorMessage("Failed to delete time entry.", exception.Message);
							}
							finally
							{
								this._state.SelectedIds.TimeEntry = -1;
							}
						});

						return true;
					},
				},
			};
		}

		private async ValueTask<List<Result>> _GetReportsResults(CancellationToken token, Query query)
		{
			var me = (await this._GetMe(token))?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var ArgumentIndices = new
			{
				Command = 0,
				Span = 1,
				Grouping = 2,
				GroupingName = 3,
				SubGroupingName = 4,
			};

			if (query.SearchTerms.Length == ArgumentIndices.Span)
			{
				// Start fetch for running time entries asynchronously in the background
				_ = Task.Run(() =>
				{
					_ = this._GetRunningTimeEntry(token, force: true);
				});
			}
			else if (query.SearchTerms.Length == ArgumentIndices.GroupingName)
			{
				this._state.SelectedIds.Project = -1;
				this._state.SelectedIds.Client = -1;
			}

			/* 
			 * Report span selection --- tgl view [day | week | month | year]
			 */

			// Parsed:
			// `null` — error.
			// `(DateTimeOffset, null)` — start date, no end date.
			// `(DateTimeOffset, DateTimeOffset) — start date, end date.
			// ? This is a delegate with out parameters so:
			// ?   1) This delegate can be called only when the span argument exists (through conditional short-circuiting)
			// ?   2) The 'useful' outputs are returned as out parameters so we can:
			// ?      - check for validity inside a conditional, but
			// ?	  - save the parsed output (if valid) so we do not need to re-parse later when we want it
			var parseSpanDates = (string spanQuery, out (DateTimeOffset, DateTimeOffset?)? parsed, out TogglTrack.ReportsSpanDatesError error) =>
			{
				Match datesMatch = Settings.ReportsSpanDatesRegex.Match(spanQuery.Replace(Settings.FauxWhitespace, " "));
				if (!datesMatch.Success)
				{
					parsed = null;
					error = TogglTrack.ReportsSpanDatesError.ParsingError;
					return false;
				}

				bool success = DateTimeOffset.TryParse(datesMatch.Groups[1].Value, out var start);
				if (!success)
				{
					parsed = null;
					error = TogglTrack.ReportsSpanDatesError.ParsingError;
					return false;
				}

				DateTimeOffset end;
				if (string.IsNullOrEmpty(datesMatch.Groups[2].Value))
				{
					if (!spanQuery.Contains(Settings.DateSeparator))
					{
						parsed = (start, null);
						error = TogglTrack.ReportsSpanDatesError.None;
						return true;
					}

					end = DateTimeOffset.Now;
				}
				else
				{
					success = DateTimeOffset.TryParse(datesMatch.Groups[2].Value, out end);
					if (!success)
					{
						parsed = null;
						error = TogglTrack.ReportsSpanDatesError.ParsingError;
						return false;
					}
				}

				// Check that start date is before end date
				if (start > end)
				{
					parsed = null;
					error = TogglTrack.ReportsSpanDatesError.EndBeforeStartError;
					return false;
				}

				// Check span duration is not longer than one year (the API limit)
				if (start.AddYears(1) < end)
				{
					parsed = null;
					error = TogglTrack.ReportsSpanDatesError.TooLongError;
					return false;
				}

				parsed = (start, end);
				error = TogglTrack.ReportsSpanDatesError.None;
				return true;
			};

			/* 
			   NO_SPAN | NO_VALID_ARG | NO_VALID_DATE | SELECTING_SPAN
			   -------------------------------------------------------
				  0           0               0         X — Not possible
				  0           0               1         0 — Valid argument
				  0           1               0         0 — Valid date
				  0           1               1         1 — No valid span
				  1           X               X         1 — No span argument
			 */
			(DateTimeOffset Start, DateTimeOffset? End)? parsedDates = null;
			TogglTrack.ReportsSpanDatesError parsedDatesError = TogglTrack.ReportsSpanDatesError.None;
			bool selectingSpan = (
				(query.SearchTerms.Length == ArgumentIndices.Span) ||
				(
					(!Settings.ReportsSpanArguments.Exists(span => Regex.IsMatch(query.SearchTerms[ArgumentIndices.Span], $"{span.Argument}({Settings.ReportsSpanOffsetRegex})?"))) &&
					!parseSpanDates(query.SearchTerms[ArgumentIndices.Span], out parsedDates, out parsedDatesError)
				)
			);
			if (selectingSpan)
			{
				(string queryToSpan, string spanQuery) = new TransformedQuery(query)
					.Split(ArgumentIndices.Span)
					.ToStrings();

				// Implementation of eg '-5' to set span to be 5 [days | weeks | months | years] ago
				Match spanOffsetMatch = Settings.ReportsSpanOffsetRegex.Match(spanQuery);
				int spanOffset = (spanOffsetMatch.Success)
					? int.Parse(spanOffsetMatch.Groups[1].Value)
					: 0;

				string sanitisedSpanQuery = Settings.ReportsSpanOffsetRegex.Replace(spanQuery, string.Empty).Replace("-", string.Empty);
				var filteredSpans = (string.IsNullOrEmpty(sanitisedSpanQuery))
					? Settings.ReportsSpanArguments
					: Settings.ReportsSpanArguments.FindAll(span => this._context.API.FuzzySearch(sanitisedSpanQuery, span.Argument).Score > 0);

				var spans = filteredSpans.ConvertAll(span =>
				{
					string argument = (spanOffsetMatch.Success)
						? $"{span.Argument}{spanOffsetMatch.Value}"
						: span.Argument;

					return new Result
					{
						Title = span.Argument,
						SubTitle = $"View tracked time report for {span.Interpolation(spanOffset)}",
						IcoPath = IconProvider.ReportsIcon,
						AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} {argument} ",
						Score = span.Score,
						Action = _ =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} {argument} ", true);
							return false;
						},
					};
				});

				// Display usage results for arbitrary date(span) (#55)
				if (parsedDates is null)
				{
					Result? usageResult = null;

					// ? TogglTrack.ReportsSpanDatesError.None means either:
					// ?   - Parsing was not attempted, or
					// ?   - Parsing was successful (but we won't be in this if-block because parsedDates would be non-null)
					// ? If any other enum type, parsing was both attempted and unsuccessful
					if (parsedDatesError != TogglTrack.ReportsSpanDatesError.None)
					{
						switch (parsedDatesError)
						{
							case (TogglTrack.ReportsSpanDatesError.ParsingError):
								{
									if (!this._settings.ShowUsageExamples)
									{
										break;
									}

									string example = spanQuery.Contains(Settings.DateSeparator)
										? $"{DateTimeOffset.Now.AddDays(-5).ToString("d")}>{DateTimeOffset.Now.ToString("d")}"
										: DateTimeOffset.Now.ToString("d");

									usageResult = new Result
									{
										Title = Settings.UsageExampleTitle,
										SubTitle = $"{query.ActionKeyword} {queryToSpan} {example}",
										IcoPath = IconProvider.UsageExampleIcon,
										AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} {example} ",
										Score = 300000,
										Action = _ =>
										{
											this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} {example} ");
											return false;
										}
									};

									break;
								}
							case (TogglTrack.ReportsSpanDatesError.TooLongError):
								{
									usageResult = new Result
									{
										Title = $"{Settings.ErrorPrefix}Span is too large",
										SubTitle = "The reports span must not exceed 1 year.",
										IcoPath = IconProvider.UsageErrorIcon,
										AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} ",
										Score = 300000,
										Action = _ =>
										{
											this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} ");
											return false;
										}
									};

									break;
								}
							case (TogglTrack.ReportsSpanDatesError.EndBeforeStartError):
								{
									usageResult = new Result
									{
										Title = $"{Settings.ErrorPrefix}Invalid reports span",
										SubTitle = "The end date must be after the start date.",
										IcoPath = IconProvider.UsageErrorIcon,
										AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} ",
										Score = 300000,
										Action = _ =>
										{
											this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} ");
											return false;
										}
									};

									break;
								}
						}
					}
					else if (this._settings.ShowUsageTips)
					{
						usageResult = new Result
						{
							Title = Settings.UsageTipTitle,
							SubTitle = "Use [start] or [start]>[end] to specify your own span",
							IcoPath = IconProvider.UsageTipIcon,
							AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} {DateTimeOffset.Now.AddDays(-4).ToString("d")}>{DateTimeOffset.Now.ToString("d")} ",
							Score = 1,
							Action = _ =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} {DateTimeOffset.Now.AddDays(-5).ToString("d")}>{DateTimeOffset.Now.ToString("d")} ");
								return false;
							}
						};
					}

					if (usageResult is not null)
					{
						spans.Add(usageResult);
					}
				}

				if ((this._settings.ShowUsageTips || this._settings.ShowUsageExamples) && !spanOffsetMatch.Success)
				{
					bool attemptedOffsetQuery = Settings.ReportsSpanPartialOffsetRegex.IsMatch(spanQuery);

					Result? usageResult = null;
					if (this._settings.ShowUsageExamples && attemptedOffsetQuery)
					{
						usageResult = new Result
						{
							Title = Settings.UsageExampleTitle,
							SubTitle = $"{query.ActionKeyword} {queryToSpan} -1",
							IcoPath = IconProvider.UsageExampleIcon,
							AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} -1 ",
							Score = 100000,
							Action = _ =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} -1 ");
								return false;
							}
						};
					}
					else if (this._settings.ShowUsageTips && !attemptedOffsetQuery)
					{
						usageResult = new Result
						{
							Title = Settings.UsageTipTitle,
							SubTitle = $"Use -[number] to view older reports relative to now",
							IcoPath = IconProvider.UsageTipIcon,
							AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} -",
							Score = 300,
							Action = _ =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} -");
								return false;
							}
						};
					}

					if (usageResult is not null)
					{
						spans.Add(usageResult);
					}
				}

				return spans;
			}

			/* 
			 * Report grouping selection --- tgl view [span] [projects | clients | entries]
			 */
			if ((query.SearchTerms.Length == ArgumentIndices.Grouping) || !Settings.ReportsGroupingArguments.Exists(grouping => grouping.Argument == query.SearchTerms[ArgumentIndices.Grouping]))
			{
				(string queryToGrouping, string groupingsQuery) = new TransformedQuery(query)
					.Split(ArgumentIndices.Grouping)
					.ToStrings();

				var filteredGroupings = (string.IsNullOrEmpty(groupingsQuery))
					? Settings.ReportsGroupingArguments
					: Settings.ReportsGroupingArguments.FindAll(grouping => this._context.API.FuzzySearch(groupingsQuery, grouping.Argument).Score > 0);

				return filteredGroupings.ConvertAll(grouping => new Result
				{
					// ! see #79... also Flow-Launcher/Flow.Launcher#2201 and Flow-Launcher/Flow.Launcher#2202
					Title = $"{grouping.Argument}{new string('\u200B', queryToGrouping.Length)}",
					SubTitle = grouping.Interpolation,
					IcoPath = IconProvider.ReportsIcon,
					AutoCompleteText = $"{query.ActionKeyword} {queryToGrouping} {grouping.Argument} ",
					Score = grouping.Score,
					Action = _ =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToGrouping} {grouping.Argument} ", true);
						return false;
					},
				});
			}

			string spanArgument = query.SearchTerms[ArgumentIndices.Span];
			string groupingArgument = query.SearchTerms[ArgumentIndices.Grouping];

			var groupingConfiguration = Settings.ReportsGroupingArguments.Find(grouping => grouping.Argument == groupingArgument);

			DateTimeOffset start, end;
			string reportSpanInterpolation;
			if (parsedDates is null)
			{
				var spanConfiguration = Settings.ReportsSpanArguments.Find(span => Regex.IsMatch(spanArgument, $"{span.Argument}({Settings.ReportsSpanOffsetRegex})?"));

				if (spanConfiguration is null)
				{
					return this.NotifyUnknownError();
				}

				Match spanArgumentOffsetMatch = Settings.ReportsSpanOffsetRegex.Match(spanArgument);
				int spanArgumentOffset = (spanArgumentOffsetMatch.Success)
					? int.Parse(spanArgumentOffsetMatch.Groups[1].Value)
					: 0;

				DateTimeOffset reportsNow;
				try
				{
					reportsNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, me.ReportsTimeZoneId);
				}
				catch (Exception exception)
				{
					this._context.API.LogException("TogglTrack", $"Failed to convert time to reports time zone '{me.ReportsTimeZoneId}'", exception);
					// Use local time instead
					reportsNow = DateTimeOffset.Now;
				}

				start = spanConfiguration.Start(reportsNow, me.BeginningOfWeek, spanArgumentOffset);
				end = spanConfiguration.End(reportsNow, me.BeginningOfWeek, spanArgumentOffset);

				reportSpanInterpolation = spanConfiguration.Interpolation(spanArgumentOffset);
			}
			else
			{
				try
				{
					start = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(parsedDates.Value.Start, me.ReportsTimeZoneId);
					end = (parsedDates.Value.End is not null)
						? TimeZoneInfo.ConvertTimeBySystemTimeZoneId((DateTimeOffset)parsedDates.Value.End, me.ReportsTimeZoneId)
						: start;
				}
				catch (Exception exception)
				{
					this._context.API.LogException("TogglTrack", $"Failed to convert time to reports time zone '{me.ReportsTimeZoneId}'", exception);

					// Use local time instead
					start = parsedDates.Value.Start;
					end = (parsedDates.Value.End is not null)
						? (DateTimeOffset)parsedDates.Value.End
						: start;
				}

				string abbreviatedMonthDayPattern = DateTimeFormatInfo.CurrentInfo.MonthDayPattern.Replace("MMMM", "MMM");
				// TODO: start time
				if (parsedDates.Value.End is null)
				{
					reportSpanInterpolation = $"on {start.ToString("ddd")} {start.ToString(abbreviatedMonthDayPattern)} {start.ToString("yyyy")}";
				}
				else if (start.Year != end.Year)
				{
					reportSpanInterpolation = $"from {start.ToString("ddd")} {start.ToString(abbreviatedMonthDayPattern)} {start.ToString("yyyy")} to {end.ToString("ddd")} {end.ToString(abbreviatedMonthDayPattern)} {end.ToString("yyyy")}";
				}
				else if (start.Month == end.Month)
				{
					// Check whether the month or the date is written first in the present culture
					reportSpanInterpolation = (abbreviatedMonthDayPattern.IndexOf("M") < abbreviatedMonthDayPattern.IndexOf("d"))
						? $"from {start.ToString("ddd")} {start.ToString(abbreviatedMonthDayPattern)} to {end.ToString("ddd")} {end.ToString(abbreviatedMonthDayPattern.Replace("MMM", string.Empty)).Trim()} {end.ToString("yyyy")}"
						: $"from {start.ToString("ddd")} {start.ToString(abbreviatedMonthDayPattern.Replace("MMM", string.Empty)).Trim()} to {end.ToString("ddd")} {end.ToString(abbreviatedMonthDayPattern)} {end.ToString("yyyy")}";
				}
				else
				{
					reportSpanInterpolation = $"from {start.ToString("ddd")} {start.ToString(abbreviatedMonthDayPattern)} to {end.ToString("ddd")} {end.ToString(abbreviatedMonthDayPattern)} {end.ToString("yyyy")}";
				}
			}

			if (groupingConfiguration is null)
			{
				return this.NotifyUnknownError();
			}

			this._context.API.LogInfo("TogglTrack", $"{spanArgument}, {groupingArgument}, {start.ToString("yyyy-MM-dd")}, {end.ToString("yyyy-MM-dd")}");

			var summary = (await this._GetSummaryReport(
				token,
				workspaceId: me.DefaultWorkspaceId,
				userId: me.Id,
				reportGrouping: groupingConfiguration.Grouping,
				// TODO: startTime
				start: start,
				end: end
			))?.ToSummaryReport(me);

			// Use cached time entry here to improve responsiveness
			var runningTimeEntry = (await this._GetRunningTimeEntry(token))?.ToTimeEntry(me);
			if (runningTimeEntry is not null)
			{
				DateTimeOffset runningEntryStart;
				try
				{
					runningEntryStart = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(runningTimeEntry.StartDate, me.ReportsTimeZoneId);
				}
				catch (Exception exception)
				{
					this._context.API.LogException("TogglTrack", $"Failed to convert time to reports time zone '{me.ReportsTimeZoneId}'", exception);
					// Use local time instead
					runningEntryStart = runningTimeEntry.StartDate.ToLocalTime();
				}

				this._context.API.LogInfo("TogglTrack", $"{start.Date}, {end.Date}, {runningEntryStart.Date}");

				// TODO: check this logic: will need to updated if we allow specifing a time
				if (runningEntryStart.Date >= start.Date && runningEntryStart.Date <= end.Date)
				{
					summary = summary?.InsertRunningTimeEntry(runningTimeEntry, groupingConfiguration.Grouping);
				}
			}

			var results = new List<Result>();

			string groupQuery = new TransformedQuery(query)
				.After(ArgumentIndices.GroupingName)
				.ToString();

			if (string.IsNullOrEmpty(groupQuery))
			{
				var total = summary?.Elapsed ?? TimeSpan.Zero;

				results.Add(new Result
				{
					Title = $"{total.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {reportSpanInterpolation} ({(int)total.TotalHours}:{total.ToString(@"mm\:ss")})",
					IcoPath = IconProvider.ReportsIcon,
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
					Score = int.MaxValue - 100000,
				});
			};

			if (summary is null)
			{
				return results;
			}

			switch (groupingConfiguration.Grouping)
			{
				case (Settings.ReportsGroupingKey.Projects):
					{
						if (this._state.SelectedIds.Project == -1)
						{
							var filteredGroups = (string.IsNullOrEmpty(groupQuery))
								? summary.Groups.Values
								: summary.Groups.Values.Where(group => this._context.API.FuzzySearch(groupQuery, group.Project?.Name ?? Settings.NoProjectName).Score > 0);

							results.AddRange(
								filteredGroups.Select(group => new Result
								{
									Title = group.Project?.Name ?? Settings.NoProjectName,
									SubTitle = $"{((group.Project?.ClientId is not null) ? $"{group.Project.Client!.Name} | " : string.Empty)}{group.HumanisedElapsed} ({group.DetailedElapsed})",
									IcoPath = this._iconProvider.GetColourIcon(group.Project?.Colour, IconProvider.ReportsIcon),
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} ",
									Score = group.GetScoreByDuration(),
									Action = _ =>
									{
										this._state.SelectedIds.Project = group.Project?.Id;
										this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {group.Project?.KebabName ?? "no-project"} ", true);
										return false;
									}
								})
							);

							break;
						}

						var selectedProjectGroup = summary.GetGroup(this._state.SelectedIds.Project);

						if (selectedProjectGroup?.SubGroups is null)
						{
							break;
						}

						var project = me.GetProject(selectedProjectGroup.Id);

						IEnumerable<Result> subResults = Enumerable.Empty<Result>();
						var total = TimeSpan.Zero;

						string subGroupQuery = new TransformedQuery(query)
							.After(ArgumentIndices.SubGroupingName)
							.ToString();

						if (this._state.ReportsShowDetailed)
						{
							var report = (await this._GetDetailedReport(
								token,
								workspaceId: me.DefaultWorkspaceId,
								userId: me.Id,
								projectIds: new List<long?> { this._state.SelectedIds.Project },
								start: start,
								end: end
							))?.ConvertAll(timeEntryGroup => timeEntryGroup.ToDetailedReportTimeEntryGroup(me));

							if (report is null)
							{
								break;
							}

							if (runningTimeEntry is not null && runningTimeEntry.ProjectId == project?.Id)
							{
								DateTimeOffset runningEntryStart;
								try
								{
									runningEntryStart = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(runningTimeEntry.StartDate, me.ReportsTimeZoneId);
								}
								catch (Exception exception)
								{
									this._context.API.LogException("TogglTrack", $"Failed to convert time to reports time zone '{me.ReportsTimeZoneId}'", exception);
									// Use local time instead
									runningEntryStart = runningTimeEntry.StartDate.ToLocalTime();
								}

								this._context.API.LogInfo("TogglTrack", $"{start.Date}, {end.Date}, {runningEntryStart.Date}");

								if (runningEntryStart.Date >= start.Date && runningEntryStart.Date <= end.Date)
								{
									report.Add(new DetailedReportTimeEntryGroup(runningTimeEntry, me));
								}
							}

							subGroupQuery = new TransformedQuery(query)
								.After(ArgumentIndices.SubGroupingName)
								.RemoveAll(Settings.ShowStopFlag)
								.ToString(TransformedQuery.Escaping.Unescaped);
							bool hasShowStopFlag = query.SearchTerms.Contains(Settings.ShowStopFlag);

							var filteredTimeEntries = report.SelectMany(timeEntryGroup =>
							{
								return (string.IsNullOrEmpty(subGroupQuery))
									? timeEntryGroup.TimeEntries
									: timeEntryGroup.TimeEntries.FindAll(timeEntry => this._context.API.FuzzySearch(subGroupQuery, timeEntry.GetDescription()).Score > 0);
							});

							total = filteredTimeEntries.Aggregate(TimeSpan.Zero, (subTotal, timeEntry) => subTotal + timeEntry.Elapsed);

							subResults = subResults.Concat(filteredTimeEntries.Select(timeEntry =>
							{
								DateTimeOffset startDate = timeEntry.StartDate.ToLocalTime();
								DateTimeOffset? stopDate = timeEntry.StopDate?.ToLocalTime();
								string stopString = (stopDate is not null)
									? $"{stopDate?.ToString("t")} {stopDate?.ToString("d")}"
									: "now";

								return new Result
								{
									Title = timeEntry.GetDescription(),
									SubTitle = (hasShowStopFlag)
											? $"{timeEntry.DetailedElapsed} ({startDate.ToString("t")} {startDate.ToString("d")} to {stopString})"
											: $"{timeEntry.DetailedElapsed} ({timeEntry.HumanisedStart} at {startDate.ToString("t")} {startDate.ToString("ddd")} {startDate.ToString("m")})",
									IcoPath = this._iconProvider.GetColourIcon(project?.Colour, IconProvider.ReportsIcon),
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {project?.KebabName ?? "no-project"} {timeEntry.GetDescription(escapePotentialSymbols: true)}",
									Score = timeEntry.GetScoreByStart(),
									Action = _ =>
									{
										this._state.SelectedIds.Project = project?.Id;
										this._context.API.ChangeQuery($"{query.ActionKeyword} {timeEntry.GetRawDescription(withTrailingSpace: true, escapeCommands: true, escapePotentialSymbols: true)}");
										return false;
									},
								};
							}));

							if (this._settings.ShowUsageTips && !hasShowStopFlag)
							{
								subResults = subResults.Append(new Result
								{
									// ! see #79... also Flow-Launcher/Flow.Launcher#2201 and Flow-Launcher/Flow.Launcher#2202
									Title = $"{Settings.UsageTipTitle}{new string('\u200B', subGroupQuery.Length)}",
									SubTitle = $"Use {Settings.ShowStopFlag} to display time entry stop times",
									IcoPath = IconProvider.UsageTipIcon,
									AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.ShowStopFlag} ",
									Score = 1,
									Action = _ =>
									{
										this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.ShowStopFlag} ");
										return false;
									}
								});
							}
						}
						else
						{
							var filteredSubGroups = (string.IsNullOrEmpty(subGroupQuery))
								? selectedProjectGroup.SubGroups.Values
								: selectedProjectGroup.SubGroups.Values.Where(subGroup => this._context.API.FuzzySearch(subGroupQuery, subGroup.GetTitle()).Score > 0);

							total = filteredSubGroups.Aggregate(TimeSpan.Zero, (subTotal, subGroup) => subTotal + subGroup.Elapsed);

							subResults = subResults.Concat(filteredSubGroups.Select(subGroup => new Result
							{
								Title = subGroup.GetTitle(),
								SubTitle = $"{subGroup.HumanisedElapsed} ({subGroup.DetailedElapsed})",
								IcoPath = this._iconProvider.GetColourIcon(project?.Colour, IconProvider.ReportsIcon),
								AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {project?.KebabName ?? "no-project"} {subGroup.GetTitle(escapePotentialSymbols: true)}",
								Score = subGroup.GetScoreByDuration(),
								Action = _ =>
								{
									this._state.SelectedIds.Project = project?.Id;
									this._context.API.ChangeQuery($"{query.ActionKeyword} {subGroup.GetRawTitle(withTrailingSpace: true, escapeCommands: true, escapePotentialSymbols: true)}");
									return false;
								},
							}));
						}

						subResults = subResults.Append(new Result
						{
							// ! see #79... also Flow-Launcher/Flow.Launcher#2201 and Flow-Launcher/Flow.Launcher#2202
							Title = $"Display {((this._state.ReportsShowDetailed) ? "summary" : "detailed")} report{new string('\u200B', subGroupQuery.Length)}",
							IcoPath = IconProvider.ReportsIcon,
							AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
							Score = int.MaxValue - 1000,
							Action = _ =>
							{
								this._state.ReportsShowDetailed = !this._state.ReportsShowDetailed;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} ", true);
								return false;
							},
						});

						subResults = subResults.Append(new Result
						{
							Title = $"{total.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {reportSpanInterpolation} ({(int)total.TotalHours}:{total.ToString(@"mm\:ss")})",
							SubTitle = (!string.IsNullOrEmpty(subGroupQuery))
								? $"{project?.WithClientName ?? Settings.NoProjectName} | {subGroupQuery}"
								: project?.WithClientName ?? Settings.NoProjectName,
							IcoPath = IconProvider.ReportsIcon,
							AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
							Score = int.MaxValue - 100000,
						});

						return subResults.ToList();
					}
				case (Settings.ReportsGroupingKey.Clients):
					{
						if (this._state.SelectedIds.Client == -1)
						{
							var filteredGroups = (string.IsNullOrEmpty(groupQuery))
								? summary.Groups.Values
								: summary.Groups.Values.Where(group => this._context.API.FuzzySearch(groupQuery, group.Client?.Name ?? Settings.NoClientName).Score > 0);

							results.AddRange(
								filteredGroups.Select(group =>
								{
									var longestProject = me.GetProject(group.LongestSubGroup?.Id);

									return new Result
									{
										Title = group.Client?.Name ?? Settings.NoClientName,
										SubTitle = $"{group.HumanisedElapsed} ({group.DetailedElapsed})",
										IcoPath = this._iconProvider.GetColourIcon(longestProject?.Colour, IconProvider.ReportsIcon),
										AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} ",
										Score = group.GetScoreByDuration(),
										Action = _ =>
										{
											this._state.SelectedIds.Client = group.Client?.Id;
											this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {group.Client?.KebabName ?? "no-client"} ", true);
											return false;
										}
									};
								})
							);

							break;
						}

						var selectedClientGroup = summary.GetGroup(this._state.SelectedIds.Client);

						if (selectedClientGroup?.SubGroups is null)
						{
							break;
						}

						var client = me.GetClient(selectedClientGroup.Id);

						string subGroupQuery = new TransformedQuery(query)
							.After(ArgumentIndices.SubGroupingName)
							.ToString();

						var filteredSubGroups = (string.IsNullOrEmpty(subGroupQuery))
							? selectedClientGroup.SubGroups.Values
							: selectedClientGroup.SubGroups.Values.Where(subGroup =>
							{
								var project = me.GetProject(subGroup.Id);
								return this._context.API.FuzzySearch(subGroupQuery, project?.Name ?? Settings.NoProjectName).Score > 0;
							});

						var subResults = filteredSubGroups.Select(subGroup =>
						{
							var project = me.GetProject(subGroup.Id);

							return new Result
							{
								Title = project?.Name ?? Settings.NoProjectName,
								SubTitle = $"{((client?.Id is not null) ? $"{client.Name} | " : string.Empty)}{subGroup.HumanisedElapsed} ({subGroup.DetailedElapsed})",
								IcoPath = this._iconProvider.GetColourIcon(project?.Colour, IconProvider.ReportsIcon),
								AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {client?.KebabName ?? "no-client"} ",
								Score = subGroup.GetScoreByDuration(),
								Action = _ =>
								{
									this._state.SelectedIds.Client = -1;
									this._state.SelectedIds.Project = project?.Id;

									if (string.IsNullOrEmpty(groupingConfiguration.SubArgument))
									{
										throw new Exception("Invalid ViewGroupingCommandArgument configuration: Missing 'SubArgument' field.");
									}

									this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingConfiguration.SubArgument} {project?.KebabName ?? "no-project"} ", true);
									return false;
								}
							};
						});

						if (string.IsNullOrEmpty(subGroupQuery))
						{
							subResults = subResults.Append(new Result
							{
								Title = $"{selectedClientGroup.HumanisedElapsed} tracked {reportSpanInterpolation} ({selectedClientGroup.DetailedElapsed})",
								SubTitle = client?.Name ?? Settings.NoClientName,
								IcoPath = IconProvider.ReportsIcon,
								AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
								Score = int.MaxValue - 100000,
							});
						}

						return subResults.ToList();
					}
				case (Settings.ReportsGroupingKey.Entries):
					{

						IEnumerable<Result> subResults = Enumerable.Empty<Result>();
						var total = TimeSpan.Zero;

						if (this._state.ReportsShowDetailed)
						{
							var report = (await this._GetDetailedReport(
								token,
								workspaceId: me.DefaultWorkspaceId,
								userId: me.Id,
								projectIds: null,
								start: start,
								end: end
							))?.ConvertAll(timeEntryGroup => timeEntryGroup.ToDetailedReportTimeEntryGroup(me));

							if (report is null)
							{
								break;
							}

							if (runningTimeEntry is not null)
							{
								DateTimeOffset runningEntryStart;
								try
								{
									runningEntryStart = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(runningTimeEntry.StartDate, me.ReportsTimeZoneId);
								}
								catch (Exception exception)
								{
									this._context.API.LogException("TogglTrack", $"Failed to convert time to reports time zone '{me.ReportsTimeZoneId}'", exception);
									// Use local time instead
									runningEntryStart = runningTimeEntry.StartDate.ToLocalTime();
								}

								this._context.API.LogInfo("TogglTrack", $"{start.Date}, {end.Date}, {runningEntryStart.Date}");

								if (runningEntryStart.Date >= start.Date && runningEntryStart.Date <= end.Date)
								{
									report.Add(new DetailedReportTimeEntryGroup(runningTimeEntry, me));
								}
							}

							groupQuery = new TransformedQuery(query)
								.After(ArgumentIndices.GroupingName)
								.RemoveAll(Settings.ShowStopFlag)
								.ToString(TransformedQuery.Escaping.Unescaped);
							bool hasShowStopFlag = query.SearchTerms.Contains(Settings.ShowStopFlag);

							var filteredTimeEntries = report.SelectMany(timeEntryGroup =>
							{
								return (string.IsNullOrEmpty(groupQuery))
									? timeEntryGroup.TimeEntries
									: timeEntryGroup.TimeEntries.FindAll(timeEntry => this._context.API.FuzzySearch(groupQuery, timeEntry.GetDescription()).Score > 0);
							});

							total = filteredTimeEntries.Aggregate(TimeSpan.Zero, (subTotal, timeEntry) => subTotal + timeEntry.Elapsed);

							subResults = subResults.Concat(filteredTimeEntries.Select(timeEntry =>
							{
								DateTimeOffset startDate = timeEntry.StartDate.ToLocalTime();
								DateTimeOffset? stopDate = timeEntry.StopDate?.ToLocalTime();
								string stopString = (stopDate is not null)
									? $"{stopDate?.ToString("t")} {stopDate?.ToString("d")}"
									: "now";

								return new Result
								{
									Title = timeEntry.GetDescription(),
									SubTitle = (hasShowStopFlag)
										? $"{timeEntry.DetailedElapsed} ({startDate.ToString("t")} {startDate.ToString("d")} to {stopString})"
										: $"{timeEntry.DetailedElapsed} ({timeEntry.HumanisedStart} at {startDate.ToString("t")} {startDate.ToString("ddd")} {startDate.ToString("m")})",
									IcoPath = this._iconProvider.GetColourIcon(timeEntry.Project?.Colour, IconProvider.ReportsIcon),
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {timeEntry.GetDescription(escapePotentialSymbols: true)}",
									Score = timeEntry.GetScoreByStart(),
									Action = _ =>
									{
										this._state.SelectedIds.Project = timeEntry.Project?.Id;
										this._context.API.ChangeQuery($"{query.ActionKeyword} {timeEntry.GetRawDescription(withTrailingSpace: true, escapeCommands: true, escapePotentialSymbols: true)}");
										return false;
									},
								};
							}));

							if (this._settings.ShowUsageTips && !hasShowStopFlag)
							{
								subResults = subResults.Append(new Result
								{
									// ! see #79... also Flow-Launcher/Flow.Launcher#2201 and Flow-Launcher/Flow.Launcher#2202
									Title = $"{Settings.UsageTipTitle}{new string('\u200B', groupQuery.Length)}",
									SubTitle = $"Use {Settings.ShowStopFlag} to display time entry stop times",
									IcoPath = IconProvider.UsageTipIcon,
									AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.ShowStopFlag} ",
									Score = 1,
									Action = _ =>
									{
										this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.ShowStopFlag} ");
										return false;
									}
								});
							}
						}
						else
						{
							var filteredSubGroups = summary.Groups.Values.SelectMany(group =>
							{
								var filteredSubGroups = (string.IsNullOrEmpty(groupQuery))
									? group.SubGroups?.Values
									: group.SubGroups?.Values.Where(subGroup => this._context.API.FuzzySearch(groupQuery, subGroup.GetTitle()).Score > 0);

								return filteredSubGroups ?? Enumerable.Empty<SummaryReportSubGroup>();
							});

							total = filteredSubGroups.Aggregate(TimeSpan.Zero, (subTotal, subGroup) => subTotal + subGroup.Elapsed);

							subResults = subResults.Concat(filteredSubGroups.Select(subGroup => new Result
							{
								Title = subGroup.GetTitle(),
								SubTitle = $"{subGroup.Group.Project?.WithClientName ?? Settings.NoProjectName} | {subGroup.HumanisedElapsed} ({subGroup.DetailedElapsed})",
								IcoPath = this._iconProvider.GetColourIcon(subGroup.Group.Project?.Colour, IconProvider.ReportsIcon),
								AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {subGroup.GetTitle(escapePotentialSymbols: true)}",
								Score = subGroup.GetScoreByDuration(),
								Action = _ =>
								{
									this._state.SelectedIds.Project = subGroup.Group.Project?.Id;
									this._context.API.ChangeQuery($"{query.ActionKeyword} {subGroup.GetRawTitle(withTrailingSpace: true, escapeCommands: true, escapePotentialSymbols: true)}");
									return false;
								},
							}));
						}

						subResults = subResults.Append(new Result
						{
							// ! see #79... also Flow-Launcher/Flow.Launcher#2201 and Flow-Launcher/Flow.Launcher#2202
							Title = $"Display {((this._state.ReportsShowDetailed) ? "summary" : "detailed")} report{new string('\u200B', groupQuery.Length)}",
							IcoPath = IconProvider.ReportsIcon,
							AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
							Score = int.MaxValue - 1000,
							Action = _ =>
							{
								this._state.ReportsShowDetailed = !this._state.ReportsShowDetailed;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} ", true);
								return false;
							},
						});

						subResults = subResults.Append(new Result
						{
							Title = $"{total.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {reportSpanInterpolation} ({(int)total.TotalHours}:{total.ToString(@"mm\:ss")})",
							SubTitle = groupQuery,
							IcoPath = IconProvider.ReportsIcon,
							AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
							Score = int.MaxValue - 100000,
						});

						return subResults.ToList();
					}
			}

			return results;
		}

	}
}
