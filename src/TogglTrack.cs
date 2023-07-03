using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using TimeSpanParserUtil;
using Flow.Launcher.Plugin.TogglTrack.TogglApi;

namespace Flow.Launcher.Plugin.TogglTrack
{
	internal class TogglTrack
	{
		private PluginInitContext _context { get; set; }
		private Settings _settings { get; set; }

		internal ColourIconProvider _colourIconProvider;

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

		private enum EditProjectState
		{
			NoProjectChange,
			NoProjectSelected,
			ProjectSelected,
		}
		private (
			(bool IsValid, string Token) LastToken,
			(long TimeEntry, long? Project, long? Client) SelectedIds,
			EditProjectState EditProject,
			bool ReportsShowDetailed
		) _state = (
			(false, string.Empty),
			(-1, -1, -1),
			TogglTrack.EditProjectState.NoProjectChange,
			false
		);

		internal TogglTrack(PluginInitContext context, Settings settings)
		{
			this._context = context;
			this._settings = settings;

			this._client = new TogglClient(this._settings.ApiToken);
			this._colourIconProvider = new ColourIconProvider(this._context);
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
					Title = "ERROR: Missing API token",
					SubTitle = "Configure Toggl Track API token in Flow Launcher settings.",
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
					Action = c =>
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
					Action = c =>
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
					Title = "ERROR: No network connection",
					SubTitle = "Connect to the internet to use Toggl Track.",
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
					Action = c =>
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
					Title = "ERROR: Invalid API token",
					SubTitle = $"{this._settings.ApiToken} is not a valid API token.",
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
					Action = c =>
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
					Action = c =>
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
					Title = "ERROR: Unknown error",
					SubTitle = "An unexpected error has occurred.",
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
					Action = c =>
					{
						return true;
					},
				},
			};
		}

		internal async ValueTask<List<Result>> GetDefaultHotKeys(CancellationToken token, bool prefetch = false)
		{
			this._state.SelectedIds = (-1, -1, -1);
			this._state.EditProject = TogglTrack.EditProjectState.NoProjectChange;
			this._state.ReportsShowDetailed = false;

			if (prefetch)
			{
				this.RefreshCache(force: false);
			}

			var results = new List<Result>
			{
				new Result
				{
					Title = Settings.StartCommand,
					SubTitle = "Start a new time entry",
					IcoPath = "start.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} ",
					Score = 15000,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} ");
						return false;
					},
				},
				new Result
				{
					Title = Settings.ContinueCommand,
					SubTitle = "Continue previous time entry",
					IcoPath = "continue.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ContinueCommand} ",
					Score = 12500,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ContinueCommand} ");
						return false;
					},
				},
				new Result
				{
					Title = Settings.EditCommand,
					SubTitle = "Edit previous time entry",
					IcoPath = "edit.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.EditCommand} ",
					Score = 6000,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.EditCommand} ");
						return false;
					}
				},
				new Result
				{
					Title = Settings.DeleteCommand,
					SubTitle = "Delete previous time entry",
					IcoPath = "delete.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} ",
					Score = 4000,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} ");
						return false;
					}
				},
				new Result
				{
					Title = Settings.ReportsCommand,
					SubTitle = "View tracked time reports",
					IcoPath = "reports.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ReportsCommand} ",
					Score = 2000,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ReportsCommand} ");
						return false;
					},
				},
				new Result
				{
					Title = Settings.BrowserCommand,
					SubTitle = "Open Toggl Track in browser",
					IcoPath = "browser.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.BrowserCommand} ",
					Score = 100,
					Action = c =>
					{
						this._context.API.OpenUrl(new Uri(@"https://track.toggl.com/timer"));
						return true;
					},
				},
				new Result
				{
					Title = Settings.HelpCommand,
					SubTitle = "Open plugin command reference",
					IcoPath = "tip.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.HelpCommand} ",
					Score = 75,
					Action = c =>
					{
						this._context.API.OpenUrl(new Uri(@"https://github.com/JamesNZL/flow-toggl-plugin/blob/main/README.md#command-reference"));
						return true;
					},
				},
				new Result
				{
					Title = Settings.RefreshCommand,
					SubTitle = "Refresh plugin cache",
					IcoPath = "refresh.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.RefreshCommand} ",
					Score = 5,
					Action = c =>
					{
						this.RefreshCache(refreshMe: true);
						return true;
					},
				},
			};

			if (await this._GetRunningTimeEntry(token) is null)
			{
				return results;
			}

			results.Add(new Result
			{
				Title = Settings.StopCommand,
				SubTitle = "Stop current time entry",
				IcoPath = "stop.png",
				AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} ",
				Score = 15050,
				Action = c =>
				{
					this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} ");
					return false;
				}
			});

			return results;
		}

		internal async ValueTask<List<Result>> RequestStartEntry(CancellationToken token, Query query)
		{
			var me = (await this._GetMe(token))?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var ArgumentIndices = new
			{
				Command = 0,
				Project = 1,
				Description = 2,
			};

			if (query.SearchTerms.Length == ArgumentIndices.Project)
			{
				this._state.SelectedIds.Project = -1;

				// Start fetch for time entries asynchronously in the background
				_ = Task.Run(() =>
				{
					_ = this._GetTimeEntries(token, force: true);
				});
			}

			if (this._state.SelectedIds.Project == -1)
			{
				var projects = new List<Result>();

				string projectQuery = new TransformedQuery(query)
					.After(ArgumentIndices.Project)
					.ToString();

				if (string.IsNullOrEmpty(projectQuery) || this._context.API.FuzzySearch(projectQuery, "No Project").Score > 0)
				{
					projects.Add(new Result
					{
						Title = Settings.NoProjectName,
						IcoPath = "start.png",
						AutoCompleteText = $"{query.ActionKeyword} {Settings.StartCommand} ",
						Score = int.MaxValue - 1000,
						Action = c =>
						{
							this._state.SelectedIds.Project = null;
							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} no-project ", true);
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
							IcoPath = this._colourIconProvider.GetColourIcon(project.Colour, "start.png"),
							AutoCompleteText = $"{query.ActionKeyword} {Settings.StartCommand} ",
							Score = index,
							Action = c =>
							{
								this._state.SelectedIds.Project = project.Id;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project.KebabName} ", true);
								return false;
							},
						})
					);
				}

				return projects;
			}

			var project = me.GetProject(this._state.SelectedIds.Project);
			long workspaceId = project?.WorkspaceId ?? me.DefaultWorkspaceId;

			string projectName = project?.WithClientName ?? Settings.NoProjectName;
			string description = new TransformedQuery(query)
				.After(ArgumentIndices.Description)
				.ToString(TransformedQuery.Escaping.Unescaped);

			var results = new List<Result>();

			if (this._settings.ShowUsageTips && string.IsNullOrEmpty(description))
			{
				results.Add(new Result
				{
					Title = Settings.UsageTipTitle,
					SubTitle = $"Keep typing to specify the time entry description",
					IcoPath = "tip.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
					Score = 1000,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} ");
						return false;
					}
				});
			}

			if (!query.SearchTerms.Contains(Settings.TimeSpanFlag))
			{
				results.Add(new Result
				{
					Title = $"Start {description}{((string.IsNullOrEmpty(description) ? string.Empty : " "))}now",
					SubTitle = projectName,
					IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "start.png"),
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Score = 10000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._state.SelectedIds.Project}, {workspaceId}, {description}");

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
									projectId: this._state.SelectedIds.Project,
									description: description,
									start: DateTimeOffset.UtcNow
								))?.ToTimeEntry(me);

								if (createdTimeEntry?.Id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this.ShowSuccessMessage($"Started {createdTimeEntry.GetRawDescription()}", projectName, "start.png");

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
						IcoPath = "tip.png",
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ",
						Score = 1,
						Action = c =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ");
							return false;
						}
					});
				}
			}
			else
			{
				try
				{
					var startTimeSpan = TimeSpanParser.Parse(
						new TransformedQuery(query)
							.After(Settings.TimeSpanFlag)
							.ToString(),
						new TimeSpanParserOptions
						{
							UncolonedDefault = Units.Minutes,
							ColonedDefault = Units.Minutes,
						}
					);
					// If we get here, there will have been a valid time span
					// An exception will be thrown if a time span was not able to be parsed
					var startTime = DateTimeOffset.UtcNow + startTimeSpan;

					// Remove -t flag from description
					string sanitisedDescription = new TransformedQuery(query)
						.Between(ArgumentIndices.Description, Settings.TimeSpanFlag)
						.ToString(TransformedQuery.Escaping.Unescaped);

					results.Add(new Result
					{
						Title = $"Start {sanitisedDescription}{((string.IsNullOrEmpty(sanitisedDescription) ? string.Empty : " "))}{startTime.Humanize()} at {startTime.ToLocalTime().ToString("t")}",
						SubTitle = projectName,
						IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "start.png"),
						AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
						Score = 100000,
						Action = c =>
						{
							Task.Run(async delegate
							{
								try
								{
									this._context.API.LogInfo("TogglTrack", $"{this._state.SelectedIds.Project}, {workspaceId}, {sanitisedDescription}, {startTimeSpan.ToString()}, time span flag");

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
										projectId: this._state.SelectedIds.Project,
										description: sanitisedDescription,
										start: startTime
									))?.ToTimeEntry(me);

									if (createdTimeEntry?.Id is null)
									{
										throw new Exception("An API error was encountered.");
									}

									this.ShowSuccessMessage($"Started {createdTimeEntry.GetRawDescription(withTrailingSpace: true)}{startTime.Humanize()}", projectName, "start.png");

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
				catch
				{
					if (this._settings.ShowUsageExamples)
					{
						string queryToFlag = new TransformedQuery(query)
							.To(Settings.TimeSpanFlag)
							.ToString();

						results.Add(new Result
						{
							Title = Settings.UsageExampleTitle,
							SubTitle = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} -5 mins",
							IcoPath = "tip.png",
							AutoCompleteText = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} -5 mins",
							Score = 100000,
							Action = c =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} -5 mins");
								return false;
							}
						});
					}

					return results;
				}
			}

			// Use cached time entries here to ensure responsiveness
			var likelyPastTimeEntry = (await this._GetTimeEntries(token))?.FirstOrDefault()?.ToTimeEntry(me);
			if ((likelyPastTimeEntry is null) || (likelyPastTimeEntry.Stop is null))
			{
				return results;
			}

			results.Add(new Result
			{
				Title = $"Start {description}{((string.IsNullOrEmpty(description) ? string.Empty : " "))}{likelyPastTimeEntry.HumanisedStop} at previous stop time",
				SubTitle = projectName,
				IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "start.png"),
				AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
				Score = 10000,
				Action = c =>
				{
					Task.Run(async delegate
					{
						try
						{
							this._context.API.LogInfo("TogglTrack", $"{this._state.SelectedIds.Project}, {workspaceId}, {description}, at previous stop time");

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
								projectId: this._state.SelectedIds.Project,
								description: description,
								start: (DateTimeOffset)lastTimeEntry.StopDate
							))?.ToTimeEntry(me);

							if (createdTimeEntry?.Id is null)
							{
								throw new Exception("An API error was encountered.");
							}

							this.ShowSuccessMessage($"Started {createdTimeEntry.GetRawDescription(withTrailingSpace: true)}at previous stop time", $"{projectName} | {createdTimeEntry.DetailedElapsed}", "start.png");

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

		internal async ValueTask<List<Result>> RequestStopEntry(CancellationToken token, Query query)
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
						Action = c =>
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
					SubTitle = $"{projectName} | {runningTimeEntry.HumanisedElapsed} ({runningTimeEntry.DetailedElapsed})",
					IcoPath = this._colourIconProvider.GetColourIcon(runningTimeEntry.Project?.Colour, "stop.png"),
					AutoCompleteText = $"{query.ActionKeyword} {Settings.StopCommand} {runningTimeEntry.GetDescription(escapePotentialFlags: true)} ",
					Score = 10000,
					Action = c =>
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

								this.ShowSuccessMessage($"Stopped {stoppedTimeEntry.GetRawDescription()}", $"{runningTimeEntry.DetailedElapsed} elapsed", "stop.png");

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
					IcoPath = "tip.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.TimeSpanEndFlag} ",
					Score = 1,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.TimeSpanEndFlag} ");
						return false;
					}
				});

				return results;
			}

			try
			{
				var stopTimeSpan = TimeSpanParser.Parse(
					new TransformedQuery(query)
						.After(Settings.TimeSpanEndFlag)
						.ToString(),
					new TimeSpanParserOptions
					{
						UncolonedDefault = Units.Minutes,
						ColonedDefault = Units.Minutes,
					}
				);
				// An exception will be thrown if a time span was not able to be parsed
				// If we get here, there will have been a valid time span
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
					SubTitle = $"{projectName} | {newElapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")})",
					IcoPath = this._colourIconProvider.GetColourIcon(runningTimeEntry.Project?.Colour, "stop.png"),
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Score = 100000,
					Action = c =>
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

								this.ShowSuccessMessage($"Stopped {stoppedTimeEntry.GetRawDescription()}", $"{(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")} elapsed", "stop.png");

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
			}
			catch
			{
				if (this._settings.ShowUsageExamples)
				{
					results.Add(new Result
					{
						Title = Settings.UsageExampleTitle,
						SubTitle = $"{query.ActionKeyword} {Settings.StopCommand} {Settings.TimeSpanEndFlag} -5 mins",
						IcoPath = "tip.png",
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} -5 mins",
						Score = 100000,
						Action = c =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} -5 mins");
							return false;
						}
					});
				}
			}

			return results;
		}

		internal async ValueTask<List<Result>> RequestContinueEntry(CancellationToken token, Query query)
		{
			var me = (await this._GetMe(token))?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var timeEntries = (await this._GetMaxReportTimeEntries(token))?.ToSummaryReport(me);

			if (timeEntries is null)
			{
				return new List<Result>
				{
					new Result
					{
						Title = $"No previous time entries",
						SubTitle = "There are no previous time entries to continue.",
						IcoPath = this._context.CurrentPluginMetadata.IcoPath,
						Action = c =>
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

			if (query.SearchTerms.Length == ArgumentIndices.Description)
			{
				// Start fetch for time entries asynchronously in the background
				_ = Task.Run(() =>
				{
					_ = this._GetTimeEntries(token, force: true);
				});
			}

			string entriesQuery = new TransformedQuery(query)
				.After(ArgumentIndices.Description)
				.ToString();

			return timeEntries.Groups.Values.SelectMany(project =>
			{
				if (project.SubGroups is null)
				{
					return Enumerable.Empty<Result>();
				}

				var filteredTimeEntries = (string.IsNullOrEmpty(entriesQuery))
					? project.SubGroups.Values
					: project.SubGroups.Values.Where(timeEntry => this._context.API.FuzzySearch(entriesQuery, timeEntry.GetTitle()).Score > 0);

				return filteredTimeEntries.Select(timeEntry => new Result
				{
					Title = timeEntry.GetTitle(),
					SubTitle = $"{project.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.HumanisedElapsed}",
					IcoPath = this._colourIconProvider.GetColourIcon(project.Project?.Colour, "continue.png"),
					AutoCompleteText = $"{query.ActionKeyword} {Settings.ContinueCommand} {timeEntry.GetTitle(escapePotentialFlags: true)}",
					Score = timeEntry.GetScoreByStart(),
					Action = c =>
					{
						this._state.SelectedIds.Project = project.Project?.Id;
						this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project.Project?.KebabName ?? "no-project"} {timeEntry.GetRawTitle(withTrailingSpace: true, escapePotentialFlags: true)}");
						return false;
					},
				});
			}).ToList();
		}

		internal async ValueTask<List<Result>> RequestEditEntry(CancellationToken token, Query query)
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
						Action = c =>
						{
							return true;
						},
					},
				};
			}

			var ArgumentIndices = new
			{
				Command = 0,
				// If it exists
				Project = 1,
				DescriptionWithoutProject = 1,
				DescriptionWithProject = 2,
			};

			if (this._state.SelectedIds.TimeEntry == -1)
			{
				string entriesQuery = new TransformedQuery(query)
					.After(ArgumentIndices.DescriptionWithoutProject)
					.ToString();
				var filteredTimeEntries = (string.IsNullOrEmpty(entriesQuery))
					? timeEntries
					: timeEntries.FindAll(timeEntry => this._context.API.FuzzySearch(entriesQuery, timeEntry.GetDescription()).Score > 0);

				return filteredTimeEntries.ConvertAll(timeEntry => new Result
				{
					Title = timeEntry.GetDescription(),
					SubTitle = $"{timeEntry.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.HumanisedElapsed} ({timeEntry.HumanisedStart})",
					IcoPath = this._colourIconProvider.GetColourIcon(timeEntry.Project?.Colour, "edit.png"),
					AutoCompleteText = $"{query.ActionKeyword} {Settings.EditCommand} {timeEntry.GetDescription(escapePotentialFlags: true)}",
					Score = timeEntry.GetScoreByStart(),
					Action = c =>
					{
						this._state.SelectedIds.TimeEntry = timeEntry.Id;
						this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialFlags: true)}", true);
						return false;
					},
				});
			}

			var timeEntry = timeEntries.Find(timeEntry => timeEntry.Id == this._state.SelectedIds.TimeEntry);
			if (timeEntry is null)
			{
				return this.NotifyUnknownError();
			}

			// Reset project selection if query emptied to 'tgl edit '
			if (query.SearchTerms.Length == (ArgumentIndices.Command + 1) && this._state.EditProject == TogglTrack.EditProjectState.ProjectSelected)
			{
				this._state.SelectedIds.Project = -1;
				this._state.EditProject = TogglTrack.EditProjectState.NoProjectChange;
			}

			if (this._state.EditProject == TogglTrack.EditProjectState.NoProjectChange)
			{
				// Firstly set to current project
				this._state.SelectedIds.Project = timeEntry.ProjectId;

				// If the -p flag exists, set up next request for project selection
				if (query.SearchTerms.Contains(Settings.EditProjectFlag))
				{
					this._state.SelectedIds.Project = -1;
					this._state.EditProject = TogglTrack.EditProjectState.NoProjectSelected;
					this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} ");
				}
			}

			if (this._state.SelectedIds.Project == -1)
			{
				var projects = new List<Result>();

				string projectQuery = new TransformedQuery(query)
					.After(ArgumentIndices.Project)
					.ToString();

				if (string.IsNullOrEmpty(projectQuery) || this._context.API.FuzzySearch(projectQuery, "No Project").Score > 0)
				{
					projects.Add(new Result
					{
						Title = Settings.NoProjectName,
						IcoPath = "edit.png",
						AutoCompleteText = $"{query.ActionKeyword} {Settings.EditCommand} ",
						Score = int.MaxValue - 1000,
						Action = c =>
						{
							this._state.SelectedIds.Project = null;
							this._state.EditProject = TogglTrack.EditProjectState.ProjectSelected;
							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} no-project {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialFlags: true)}", true);
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
							IcoPath = this._colourIconProvider.GetColourIcon(project.Colour, "edit.png"),
							AutoCompleteText = $"{query.ActionKeyword} {Settings.EditCommand} ",
							Score = index,
							Action = c =>
							{
								this._state.SelectedIds.Project = project.Id;
								this._state.EditProject = TogglTrack.EditProjectState.ProjectSelected;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} {project.KebabName} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialFlags: true)}", true);
								return false;
							},
						})
					);
				}

				return projects;
			}

			var results = new List<Result>();

			if (query.SearchTerms.Contains(Settings.ClearDescriptionFlag))
			{
				string queryToDescription = new TransformedQuery(query)
					.To((this._state.EditProject == TogglTrack.EditProjectState.ProjectSelected)
						? ArgumentIndices.DescriptionWithProject
						: ArgumentIndices.DescriptionWithoutProject
					)
					.ToString();

				this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToDescription} ");
			}
			else if (this._settings.ShowUsageTips)
			{
				results.Add(new Result
				{
					Title = Settings.UsageTipTitle,
					SubTitle = $"Use {Settings.ClearDescriptionFlag} to quickly clear the description",
					IcoPath = "tip.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.ClearDescriptionFlag} ",
					Score = 2,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.ClearDescriptionFlag} ");
						return false;
					}
				});
			}

			var project = me.GetProject(this._state.SelectedIds.Project);

			string projectName = project?.WithClientName ?? Settings.NoProjectName;
			string description = new TransformedQuery(query)
				.After((this._state.EditProject == TogglTrack.EditProjectState.ProjectSelected)
					? ArgumentIndices.DescriptionWithProject
					: ArgumentIndices.DescriptionWithoutProject
				)
				.ToString(TransformedQuery.Escaping.Unescaped);

			if (this._settings.ShowUsageWarnings && string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(timeEntry.GetRawDescription()))
			{
				results.Add(new Result
				{
					Title = Settings.UsageWarningTitle,
					SubTitle = $"Time entry description will be cleared if nothing is entered!",
					IcoPath = "tip-warning.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialFlags: true)}",
					Score = 1000,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialFlags: true)}");
						return false;
					}
				});
			}

			var timeSpanFlags = new string[] { Settings.TimeSpanFlag, Settings.TimeSpanEndFlag };
			bool hasTimeSpanFlag = query.SearchTerms.Contains(Settings.TimeSpanFlag);
			bool hasTimeSpanEndFlag = query.SearchTerms.Contains(Settings.TimeSpanEndFlag);

			if (!hasTimeSpanFlag && !hasTimeSpanEndFlag)
			{
				results.Add(new Result
				{
					Title = (string.IsNullOrEmpty(description))
						? timeEntry.GetDescription()
						: description,
					SubTitle = $"{projectName} | {timeEntry.HumanisedElapsed} ({timeEntry.DetailedElapsed})",
					IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "edit.png"),
					AutoCompleteText = $"{query.ActionKeyword} {(string.IsNullOrEmpty(description) ? ($"{query.Search} {timeEntry.GetDescription(escapePotentialFlags: true)}") : query.Search)} ",
					Score = int.MaxValue - 1000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._state.SelectedIds.Project}, {timeEntry.Id}, {timeEntry.Duration}, {timeEntry.Start}, {this._state.SelectedIds.Project}, {timeEntry.WorkspaceId}, {description}");

								var editedTimeEntry = (await this._client.EditTimeEntry(
									workspaceId: timeEntry.WorkspaceId,
									projectId: this._state.SelectedIds.Project,
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

								this.ShowSuccessMessage($"Edited {editedTimeEntry.GetRawDescription()}", $"{projectName} | {timeEntry.DetailedElapsed}", "edit.png");

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
								this._state.EditProject = TogglTrack.EditProjectState.NoProjectChange;
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
								IcoPath = this._colourIconProvider.GetColourIcon(pastProject.Project?.Colour, "edit.png"),
								AutoCompleteText = $"{query.ActionKeyword} {pastTimeEntry.GetTitle(escapePotentialFlags: true)}",
								Score = pastTimeEntry.GetScoreByStart(),
								Action = c =>
								{
									Task.Run(async delegate
									{
										try
										{
											this._context.API.LogInfo("TogglTrack", $"past time entry {pastProject.Project?.Id}, {pastTimeEntry.Id}, {timeEntry.Duration}, {timeEntry.Start}, {this._state.SelectedIds.Project}, {timeEntry.WorkspaceId}, {description}, {pastTimeEntry.GetTitle()}");

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

											this.ShowSuccessMessage($"Edited {editedTimeEntry.GetRawDescription()}", $"{pastProject.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.DetailedElapsed}", "edit.png");

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
											this._state.EditProject = TogglTrack.EditProjectState.NoProjectChange;
										}
									});

									return true;
								},
							});
					}));
				}

				if (this._settings.ShowUsageTips)
				{
					if (!hasTimeSpanFlag)
					{
						results.Add(new Result
						{
							Title = Settings.UsageTipTitle,
							SubTitle = $"Use {Settings.TimeSpanFlag} after the description to edit the start time",
							IcoPath = "tip.png",
							AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ",
							Score = 10,
							Action = c =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ");
								return false;
							}
						});
					}

					if (!hasTimeSpanEndFlag)
					{
						results.Add(new Result
						{
							Title = Settings.UsageTipTitle,
							SubTitle = $"Use {Settings.TimeSpanEndFlag} after the description to edit the stop time",
							IcoPath = "tip.png",
							AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.TimeSpanEndFlag} ",
							Score = 5,
							Action = c =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.TimeSpanEndFlag} ");
								return false;
							}
						});
					}
				}
			}
			else
			{
				// TimeSpanFlag and/or TimeSpanEndFlag is present
				int firstFlag = -1;
				for (int i = 0; i < query.SearchTerms.Length; i++)
				{
					if (!timeSpanFlags.Contains(query.SearchTerms[i]))
					{
						continue;
					}

					firstFlag = i;
					break;
				}

				try
				{
					TimeSpan? startTimeSpan = null;
					TimeSpan? endTimeSpan = null;
					TimeSpan newElapsed = timeEntry.Elapsed;

					if (hasTimeSpanFlag)
					{
						try
						{
							startTimeSpan = TimeSpanParser.Parse(
								new TransformedQuery(query)
									.After(Settings.TimeSpanFlag)
									.ToString(),
								new TimeSpanParserOptions
								{
									UncolonedDefault = Units.Minutes,
									ColonedDefault = Units.Minutes,
								}
							);
							// An exception will be thrown if a time span was not able to be parsed
							// If we get here, there will have been a valid time span
							newElapsed = newElapsed.Subtract((TimeSpan)startTimeSpan);
						}
						catch
						{
							throw new ArgumentException(Settings.TimeSpanFlag);
						}
					}
					if (hasTimeSpanEndFlag)
					{
						try
						{
							endTimeSpan = TimeSpanParser.Parse(
								new TransformedQuery(query)
									.After(Settings.TimeSpanEndFlag)
									.ToString(),
								new TimeSpanParserOptions
								{
									UncolonedDefault = Units.Minutes,
									ColonedDefault = Units.Minutes,
								}
							);
							// An exception will be thrown if a time span was not able to be parsed
							// If we get here, there will have been a valid time span
							newElapsed = newElapsed.Add((TimeSpan)endTimeSpan);
						}
						catch
						{
							throw new ArgumentException(Settings.TimeSpanEndFlag);
						}
					}

					// Remove flags from description
					string sanitisedDescription = new TransformedQuery(query)
						.Between(
							(this._state.EditProject == TogglTrack.EditProjectState.ProjectSelected)
								? ArgumentIndices.DescriptionWithProject
								: ArgumentIndices.DescriptionWithoutProject,
							firstFlag
						)
						.ToString(TransformedQuery.Escaping.Unescaped);

					if (this._settings.ShowUsageWarnings && string.IsNullOrEmpty(sanitisedDescription) && !string.IsNullOrEmpty(timeEntry.GetRawDescription()))
					{
						results.Add(new Result
						{
							Title = Settings.UsageWarningTitle,
							SubTitle = $"Time entry description will be cleared if nothing is entered!",
							IcoPath = "tip-warning.png",
							AutoCompleteText = $"{query.ActionKeyword} {query.Search} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialFlags: true)}",
							Score = 1000,
							Action = c =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialFlags: true)}");
								return false;
							}
						});
					}

					var startTime = (timeEntry.StartDate + startTimeSpan) ?? timeEntry.StartDate;
					var stopTime = ((timeEntry.StopDate ?? DateTimeOffset.UtcNow) + endTimeSpan);

					results.Add(new Result
					{
						Title = (string.IsNullOrEmpty(sanitisedDescription))
							? timeEntry.GetDescription()
							: sanitisedDescription,
						SubTitle = $"{projectName} | {newElapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")})",
						IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "edit.png"),
						AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
						Score = int.MaxValue - 1000,
						Action = c =>
						{
							Task.Run(async delegate
							{
								try
								{
									this._context.API.LogInfo("TogglTrack", $"{this._state.SelectedIds.Project}, {timeEntry.Id}, {timeEntry.Duration}, {timeEntry.Start}, {this._state.SelectedIds.Project}, {timeEntry.WorkspaceId}, {sanitisedDescription}, {startTime.ToString("yyyy-MM-ddTHH:mm:ssZ")}, {startTimeSpan.ToString()}, {stopTime?.ToString("yyyy-MM-ddTHH:mm:ssZ")}, {endTimeSpan.ToString()}, edit start time");

									var editedTimeEntry = (await this._client.EditTimeEntry(
										workspaceId: timeEntry.WorkspaceId,
										projectId: this._state.SelectedIds.Project,
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

									this.ShowSuccessMessage($"Edited {editedTimeEntry.GetRawDescription()}", $"{projectName} | {(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")}", "edit.png");

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
									this._state.EditProject = TogglTrack.EditProjectState.NoProjectChange;
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
									IcoPath = this._colourIconProvider.GetColourIcon(pastProject.Project?.Colour, "edit.png"),
									AutoCompleteText = $"{query.ActionKeyword} {pastTimeEntry.GetTitle(escapePotentialFlags: true)}",
									Score = pastTimeEntry.GetScoreByStart(),
									Action = c =>
									{
										Task.Run(async delegate
										{
											try
											{
												this._context.API.LogInfo("TogglTrack", $"past time entry {pastProject.Project?.Id}, {pastTimeEntry.Id}, {timeEntry.Duration}, {timeEntry.Start}, {this._state.SelectedIds.Project}, {timeEntry.WorkspaceId}, {sanitisedDescription}, {pastTimeEntry.GetTitle()}");

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

												this.ShowSuccessMessage($"Edited {editedTimeEntry.GetRawDescription()}", $"{pastProject.Project?.WithClientName ?? Settings.NoProjectName} | {(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")}", "edit.png");

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
												this._state.EditProject = TogglTrack.EditProjectState.NoProjectChange;
											}
										});

										return true;
									},
								});
						}));
					}
				}
				catch (ArgumentException exception)
				{
					if (this._settings.ShowUsageExamples)
					{
						string flag = exception.Message;

						string queryToFlag = new TransformedQuery(query)
							.To(flag)
							.ToString();

						results.Add(new Result
						{
							Title = Settings.UsageExampleTitle,
							SubTitle = $"{query.ActionKeyword} {queryToFlag} {flag} 5 mins",
							IcoPath = "tip.png",
							AutoCompleteText = $"{query.ActionKeyword} {queryToFlag} {flag} 5 mins",
							Score = 100000,
							Action = c =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToFlag} {flag} 5 mins");
								return false;
							}
						});
					}
				}
			}

			if (this._state.EditProject != TogglTrack.EditProjectState.NoProjectChange)
			{
				return results;
			}

			if (!this._settings.ShowUsageTips)
			{
				return results;
			}

			results.Add(new Result
			{
				Title = Settings.UsageTipTitle,
				SubTitle = $"Use {Settings.EditProjectFlag} to edit the project for this time entry",
				IcoPath = "tip.png",
				AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.EditProjectFlag} ",
				Score = 1,
				Action = c =>
				{
					this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.EditProjectFlag} ");
					return false;
				}
			});

			return results;
		}

		internal async ValueTask<List<Result>> RequestDeleteEntry(CancellationToken token, Query query)
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
						Action = c =>
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
					IcoPath = this._colourIconProvider.GetColourIcon(timeEntry.Project?.Colour, "delete.png"),
					AutoCompleteText = $"{query.ActionKeyword} {Settings.DeleteCommand} {timeEntry.GetDescription(escapePotentialFlags: true)}",
					Score = timeEntry.GetScoreByStart(),
					Action = c =>
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
					SubTitle = $"{timeEntry.Project?.WithClientName ?? Settings.NoProjectName} | {timeEntry.HumanisedElapsed} ({timeEntry.DetailedElapsed})",
					IcoPath = this._colourIconProvider.GetColourIcon(timeEntry.Project?.Colour, "delete.png") ,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} {timeEntry.GetDescription(escapePotentialFlags: true)}",
					Action = c =>
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

								this.ShowSuccessMessage($"Deleted {timeEntry.GetRawDescription()}", $"{timeEntry.DetailedElapsed} elapsed", "delete.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to delete time entry", exception, "RequestDeleteEntry");
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

		internal async ValueTask<List<Result>> RequestViewReports(CancellationToken token, Query query)
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

			if ((query.SearchTerms.Length == ArgumentIndices.Span) || !Settings.ReportsSpanArguments.Exists(span => Regex.IsMatch(query.SearchTerms[ArgumentIndices.Span], $"{span.Argument}({Settings.ReportsSpanOffsetRegex})?")))
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
						IcoPath = "reports.png",
						AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} {argument} ",
						Score = span.Score,
						Action = c =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} {argument} ", true);
							return false;
						},
					};
				});

				if ((this._settings.ShowUsageTips || this._settings.ShowUsageExamples) && !spanOffsetMatch.Success)
				{
					bool queryContainsDash = spanQuery.Contains("-");

					Result? usageResult = null;
					if (this._settings.ShowUsageExamples && queryContainsDash)
					{
						usageResult = new Result
						{
							Title = Settings.UsageExampleTitle,
							SubTitle = $"{query.ActionKeyword} {queryToSpan} -1",
							IcoPath = "tip.png",
							AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} -1 ",
							Score = 100000,
							Action = c =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} -1 ");
								return false;
							}
						};
					}
					else if (this._settings.ShowUsageTips && !queryContainsDash)
					{
						usageResult = new Result
						{
							Title = Settings.UsageTipTitle,
							SubTitle = $"Use -[number] to view older reports",
							IcoPath = "tip.png",
							AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} -",
							Score = 1,
							Action = c =>
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
					IcoPath = "reports.png",
					AutoCompleteText = $"{query.ActionKeyword} {queryToGrouping} {grouping.Argument} ",
					Score = grouping.Score,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToGrouping} {grouping.Argument} ", true);
						return false;
					},
				});
			}

			string spanArgument = query.SearchTerms[ArgumentIndices.Span];
			string groupingArgument = query.SearchTerms[ArgumentIndices.Grouping];

			var spanConfiguration = Settings.ReportsSpanArguments.Find(span => Regex.IsMatch(spanArgument, $"{span.Argument}({Settings.ReportsSpanOffsetRegex})?"));
			var groupingConfiguration = Settings.ReportsGroupingArguments.Find(grouping => grouping.Argument == groupingArgument);

			if ((spanConfiguration is null) || (groupingConfiguration is null))
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

			var start = spanConfiguration.Start(reportsNow, me.BeginningOfWeek, spanArgumentOffset);
			var end = spanConfiguration.End(reportsNow, me.BeginningOfWeek, spanArgumentOffset);

			this._context.API.LogInfo("TogglTrack", $"{spanArgument}, {groupingArgument}, {start.ToString("yyyy-MM-dd")}, {end.ToString("yyyy-MM-dd")}");

			var summary = (await this._GetSummaryReport(
				token,
				workspaceId: me.DefaultWorkspaceId,
				userId: me.Id,
				reportGrouping: groupingConfiguration.Grouping,
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
					Title = $"{total.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {spanConfiguration.Interpolation(spanArgumentOffset)} ({(int)total.TotalHours}:{total.ToString(@"mm\:ss")})",
					IcoPath = "reports.png",
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
									IcoPath = this._colourIconProvider.GetColourIcon(group.Project?.Colour, "reports.png"),
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} ",
									Score = group.GetScoreByDuration(),
									Action = c =>
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
									IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "reports.png"),
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {project?.KebabName ?? "no-project"} {timeEntry.GetDescription(escapePotentialFlags: true)}",
									Score = timeEntry.GetScoreByStart(),
									Action = c =>
									{
										this._state.SelectedIds.Project = project?.Id;
										this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project?.KebabName ?? "no-project"} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialFlags: true)}");
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
									IcoPath = "tip.png",
									AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.ShowStopFlag} ",
									Score = 1,
									Action = c =>
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
								IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "reports.png"),
								AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {project?.KebabName ?? "no-project"} {subGroup.GetTitle(escapePotentialFlags: true)}",
								Score = subGroup.GetScoreByDuration(),
								Action = c =>
								{
									this._state.SelectedIds.Project = project?.Id;
									this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project?.KebabName ?? "no-project"} {subGroup.GetRawTitle(withTrailingSpace: true, escapePotentialFlags: true)}");
									return false;
								},
							}));
						}

						subResults = subResults.Append(new Result
						{
							// ! see #79... also Flow-Launcher/Flow.Launcher#2201 and Flow-Launcher/Flow.Launcher#2202
							Title = $"Display {((this._state.ReportsShowDetailed) ? "summary" : "detailed")} report{new string('\u200B', subGroupQuery.Length)}",
							IcoPath = "reports.png",
							AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
							Score = int.MaxValue - 1000,
							Action = c =>
							{
								this._state.ReportsShowDetailed = !this._state.ReportsShowDetailed;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} ", true);
								return false;
							},
						});

						subResults = subResults.Append(new Result
						{
							Title = $"{total.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {spanConfiguration.Interpolation(spanArgumentOffset)} ({(int)total.TotalHours}:{total.ToString(@"mm\:ss")})",
							SubTitle = (!string.IsNullOrEmpty(subGroupQuery))
								? $"{project?.WithClientName ?? Settings.NoProjectName} | {subGroupQuery}"
								: project?.WithClientName ?? Settings.NoProjectName,
							IcoPath = "reports.png",
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
										IcoPath = this._colourIconProvider.GetColourIcon(longestProject?.Colour, "reports.png"),
										AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} ",
										Score = group.GetScoreByDuration(),
										Action = c =>
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
								IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "reports.png"),
								AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {client?.KebabName ?? "no-client"} ",
								Score = subGroup.GetScoreByDuration(),
								Action = c =>
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
								Title = $"{selectedClientGroup.HumanisedElapsed} tracked {spanConfiguration.Interpolation(spanArgumentOffset)} ({selectedClientGroup.DetailedElapsed})",
								SubTitle = client?.Name ?? Settings.NoClientName,
								IcoPath = "reports.png",
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
									IcoPath = this._colourIconProvider.GetColourIcon(timeEntry.Project?.Colour, "reports.png"),
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {timeEntry.GetDescription(escapePotentialFlags: true)}",
									Score = timeEntry.GetScoreByStart(),
									Action = c =>
									{
										this._state.SelectedIds.Project = timeEntry.Project?.Id;
										this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {timeEntry.Project?.KebabName ?? "no-project"} {timeEntry.GetRawDescription(withTrailingSpace: true, escapePotentialFlags: true)}");
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
									IcoPath = "tip.png",
									AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.ShowStopFlag} ",
									Score = 1,
									Action = c =>
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
								IcoPath = this._colourIconProvider.GetColourIcon(subGroup.Group.Project?.Colour, "reports.png"),
								AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {subGroup.GetTitle(escapePotentialFlags: true)}",
								Score = subGroup.GetScoreByDuration(),
								Action = c =>
								{
									this._state.SelectedIds.Project = subGroup.Group.Project?.Id;
									this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {subGroup.Group.Project?.KebabName ?? "no-project"} {subGroup.GetRawTitle(withTrailingSpace: true, escapePotentialFlags: true)}");
									return false;
								},
							}));
						}

						subResults = subResults.Append(new Result
						{
							// ! see #79... also Flow-Launcher/Flow.Launcher#2201 and Flow-Launcher/Flow.Launcher#2202
							Title = $"Display {((this._state.ReportsShowDetailed) ? "summary" : "detailed")} report{new string('\u200B', groupQuery.Length)}",
							IcoPath = "reports.png",
							AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
							Score = int.MaxValue - 1000,
							Action = c =>
							{
								this._state.ReportsShowDetailed = !this._state.ReportsShowDetailed;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} ", true);
								return false;
							},
						});

						subResults = subResults.Append(new Result
						{
							Title = $"{total.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {spanConfiguration.Interpolation(spanArgumentOffset)} ({(int)total.TotalHours}:{total.ToString(@"mm\:ss")})",
							SubTitle = groupQuery,
							IcoPath = "reports.png",
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