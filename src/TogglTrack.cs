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

		private TogglClient _client;
		private (bool IsValid, string Token) _lastToken = (false, string.Empty);

		private readonly (
			SemaphoreSlim Token,
			SemaphoreSlim Me,
			SemaphoreSlim RunningTimeEntries,
			SemaphoreSlim TimeEntries
		) _semaphores = (
			new SemaphoreSlim(1, 1),
			new SemaphoreSlim(1, 1),
			new SemaphoreSlim(1, 1),
			new SemaphoreSlim(1, 1)
		);
		private NullableCache _cache = new NullableCache();
		private List<string> _summaryTimeEntriesCacheKeys = new List<string>();

		internal ColourIconProvider _colourIconProvider;

		private long? _selectedProjectId = -1;
		private long? _selectedClientId = -1;
		
		private enum EditProjectState
		{
			NoProjectChange,
			NoProjectSelected,
			ProjectSelected,
		}
		private EditProjectState _editProjectState = TogglTrack.EditProjectState.NoProjectChange;

		internal TogglTrack(PluginInitContext context, Settings settings)
		{
			this._context = context;
			this._settings = settings;

			this._client = new TogglClient(this._settings.ApiToken);
			this._colourIconProvider = new ColourIconProvider(this._context);
		}

		private async ValueTask<MeResponse?> _GetMe(bool force = false)
		{
			const string cacheKey = "Me";

			bool hasWaited = (this._semaphores.Me.CurrentCount == 0);
			await this._semaphores.Me.WaitAsync();

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

				this._semaphores.Me.Release();
				return me;
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch me", exception);

				this._semaphores.Me.Release();
				return null;
			}
		}

		private async ValueTask<TimeEntryResponse?> _GetRunningTimeEntry(bool force = false)
		{
			const string cacheKey = "RunningTimeEntry";

			bool hasWaited = (this._semaphores.RunningTimeEntries.CurrentCount == 0);
			await this._semaphores.RunningTimeEntries.WaitAsync();

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

				this._semaphores.RunningTimeEntries.Release();
				return runningTimeEntry;
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch running time entry", exception);

				this._semaphores.RunningTimeEntries.Release();
				return null;
			}
		}

		private async ValueTask<List<TimeEntryResponse>?> _GetTimeEntries(bool force = false)
		{
			const string cacheKey = "TimeEntries";

			bool hasWaited = (this._semaphores.TimeEntries.CurrentCount == 0);
			await this._semaphores.TimeEntries.WaitAsync();

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

				this._semaphores.TimeEntries.Release();
				return timeEntries;
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch time entries", exception);
				
				this._semaphores.TimeEntries.Release();
				return null;
			}
		}

		private async ValueTask<SummaryTimeEntryResponse?> _GetSummaryTimeEntries(
			long workspaceId,
			long userId,
			Settings.ReportsGroupingKeys reportGrouping,
			DateTimeOffset start,
			DateTimeOffset? end,
			bool force = false
		)
		{
			string cacheKey = $"SummaryTimeEntries{workspaceId}{userId}{(int)reportGrouping}{start.ToString("yyyy-MM-dd")}{end?.ToString("yyyy-MM-dd")}";

			if (!force && this._cache.Contains(cacheKey))
			{
				return (SummaryTimeEntryResponse?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching summary time entries for reports");
				
				var summary = await this._client.GetSummaryTimeEntries(
					workspaceId: workspaceId,
					userId: userId,
					reportGrouping: reportGrouping,
					start: start,
					end: end
				);

				this._cache.Set(cacheKey, summary, DateTimeOffset.Now.AddSeconds(60));
				this._summaryTimeEntriesCacheKeys.Add(cacheKey);

				return summary;
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch summary time entries for reports", exception);
				return null;
			}
		}

		private void _ClearSummaryTimeEntriesCache()
		{
			this._summaryTimeEntriesCacheKeys.ForEach(key => this._cache.Remove(key));
			this._summaryTimeEntriesCacheKeys.Clear();
		}

		internal void RefreshCache(bool refreshMe = false)
		{
			_ = Task.Run(() =>
			{
				// This is the main one that needs to be run
				_ = this._GetMe(refreshMe);
				_ = this._GetRunningTimeEntry(true);
				_ = this._GetTimeEntries(true);
				this._ClearSummaryTimeEntriesCache();
			});
		}

		internal async ValueTask<bool> VerifyApiToken()
		{
			if (!InternetAvailability.IsInternetAvailable())
			{
				return false;
			}

			await this._semaphores.Token.WaitAsync();

			if (this._settings.ApiToken.Equals(this._lastToken.Token))
			{
				this._semaphores.Token.Release();
				return this._lastToken.IsValid;
			}

			// Clear the cache if the token has changed (#15)
			this._cache.Clear();

			if (string.IsNullOrWhiteSpace(this._settings.ApiToken))
			{
				this._semaphores.Token.Release();
				return this._lastToken.IsValid = false;
			}

			this._client.UpdateToken(this._settings.ApiToken);

			this._lastToken.IsValid = (await this._GetMe(true))?.ToMe().ApiToken?.Equals(this._settings.ApiToken) ?? false;
			this._lastToken.Token = this._settings.ApiToken;

			this._semaphores.Token.Release();

			if (this._lastToken.IsValid)
			{
				this.RefreshCache(true);
			}

			return this._lastToken.IsValid;
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

		internal async ValueTask<List<Result>> GetDefaultHotKeys()
		{
			this._selectedProjectId = -1;
			this._selectedClientId = -1;
			this._editProjectState = TogglTrack.EditProjectState.NoProjectChange;

			var results = new List<Result>
			{
				new Result
				{
					Title = Settings.StartCommand,
					SubTitle = "Start a new time entry",
					IcoPath = "start.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} ",
					Score = 50,
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
					Score = 10,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ContinueCommand} ");
						return false;
					},
				},
				new Result
				{
					Title = Settings.ReportsCommand,
					SubTitle = "View tracked time reports",
					IcoPath = "reports.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ReportsCommand} ",
					Score = 5,
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
					Score = -50,
					Action = c =>
					{
						this._context.API.OpenUrl(new Uri(@"https://track.toggl.com/timer"));
						return true;
					},
				},
				new Result
				{
					Title = Settings.RefreshCommand,
					SubTitle = "Refresh plugin cache",
					IcoPath = "refresh.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.RefreshCommand} ",
					Score = -100,
					Action = c =>
					{
						this.RefreshCache(true);
						return true;
					},
				},
			};

			if (await this._GetRunningTimeEntry() is null)
			{
				return results;
			}

			results.Add(new Result
			{
				Title = Settings.StopCommand,
				SubTitle = "Stop current time entry",
				IcoPath = "stop.png",
				AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} ",
				Score = 100,
				Action = c =>
				{
					this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} ");
					return false;
				}
			});
			results.Add(new Result
			{
				Title = Settings.EditCommand,
				SubTitle = "Edit current time entry",
				IcoPath = "edit.png",
				AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.EditCommand} ",
				Score = 80,
				Action = c =>
				{
					this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.EditCommand} ");
					return false;
				}
			});
			results.Add(new Result
			{
				Title = Settings.DeleteCommand,
				SubTitle = "Delete current time entry",
				IcoPath = "delete.png",
				AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} ",
				Score = 60,
				Action = c =>
				{
					this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} ");
					return false;
				}
			});

			return results;
		}

		internal async ValueTask<List<Result>> RequestStartEntry(CancellationToken token, Query query)
		{
			if (token.IsCancellationRequested)
			{
				this._selectedProjectId = -1;
				return new List<Result>();
			}

			var me = (await this._GetMe())?.ToMe();
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
				this._selectedProjectId = -1;
				
				// Start fetch for time entries asynchronously in the background
				_ = Task.Run(() =>
				{
					_ = this._GetTimeEntries(true);
				});
			}

			if (this._selectedProjectId == -1)
			{
				var projects = new List<Result>
				{
					new Result
					{
						Title = "No Project",
						IcoPath = "start.png",
						AutoCompleteText = $"{query.ActionKeyword} {Settings.StartCommand} ",
						// Ensure is 1 greater than the top-priority project
						Score = (me.Projects?.Count ?? 0) + 1,
						Action = c =>
						{
							this._selectedProjectId = null;
							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} no-project ", true);
							return false;
						},
					},
				};

				if (me.ActiveProjects is not null)
				{
					me.ActiveProjects.Sort((projectOne, projectTwo) => (projectTwo.ActualHours ?? 0) - (projectOne.ActualHours ?? 0));

					projects.AddRange(
						me.ActiveProjects.ConvertAll(project => new Result
						{
							Title = project.Name,
							SubTitle = $"{((project.ClientId is not null) ? $"{project.Client!.Name} | " : string.Empty)}{project.ElapsedString}",
							IcoPath = this._colourIconProvider.GetColourIcon(project.Colour, "start.png"),
							AutoCompleteText = $"{query.ActionKeyword} {Settings.StartCommand} ",
							Score = me.ActiveProjects.Count - me.ActiveProjects.IndexOf(project),
							Action = c =>
							{
								this._selectedProjectId = project.Id;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project.KebabName} ", true);
								return false;
							},
						})
					);
				}

				string projectQuery = Main.ExtractFromQuery(query, ArgumentIndices.Project);
				return (string.IsNullOrWhiteSpace(projectQuery))
					? projects
					: projects.FindAll(result =>
					{
						return this._context.API.FuzzySearch(projectQuery, $"{result.Title} {Regex.Replace(result.SubTitle, @"(?: \| )?\d+ hours?$", string.Empty)}").Score > 0;
					});
			}

			var project = me.GetProject(this._selectedProjectId);
			long workspaceId = project?.WorkspaceId ?? me.DefaultWorkspaceId;

			string projectName = project?.WithClientName ?? "No Project";
			string description = Main.ExtractFromQuery(query, ArgumentIndices.Description);

			var results = new List<Result>
			{
				new Result
				{
					Title = $"Start {description}{((string.IsNullOrEmpty(description) ? string.Empty : " "))}now",
					SubTitle = projectName,
					IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "start.png") ,
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Score = 10000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {workspaceId}, {description}");
								
								var runningTimeEntry = (await this._GetRunningTimeEntry(true))?.ToTimeEntry(me);
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
									projectId: this._selectedProjectId,
									description: description,
									start: DateTimeOffset.UtcNow
								))?.ToTimeEntry(me);
								
								if (createdTimeEntry?.Id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this._context.API.ShowMsg($"Started {createdTimeEntry.RawDescription}", projectName, "start.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to start time entry", exception);
								this._context.API.ShowMsgError("Failed to start time entry.", exception.Message);
							}
							finally
							{
								this._selectedProjectId = -1;
							}
						});

						return true;
					},
				},
			};

			if (!query.SearchTerms.Contains(Settings.TimeSpanFlag))
			{
				results.Add(new Result
				{
					Title = "Usage Tip",
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
			else
			{
				try
				{
					var startTimeSpan = TimeSpanParser.Parse(
						Main.ExtractFromQuery(query, Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag) + 1),
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
					string sanitisedDescription = string.Join(" ", query.SearchTerms.Take(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag)).Skip(ArgumentIndices.Description));

					results.Add(new Result
					{
						Title = $"Start {sanitisedDescription}{((string.IsNullOrEmpty(sanitisedDescription) ? string.Empty : " "))}{startTime.Humanize()}",
						SubTitle = projectName,
						IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "start.png") ,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
						Score = 100000,
						Action = c =>
						{
							Task.Run(async delegate
							{
								try
								{
									this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {workspaceId}, {sanitisedDescription}, {startTimeSpan.ToString()}, time span flag");
									
									var runningTimeEntry = (await this._GetRunningTimeEntry(true))?.ToTimeEntry(me);
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
										projectId: this._selectedProjectId,
										description: sanitisedDescription,
										start: startTime
									))?.ToTimeEntry(me);

									if (createdTimeEntry?.Id is null)
									{
										throw new Exception("An API error was encountered.");
									}

									this._context.API.ShowMsg($"Started {createdTimeEntry.RawDescription}{((string.IsNullOrEmpty(sanitisedDescription) ? string.Empty : " "))}{startTime.Humanize()}", projectName, "start.png");

									// Update cached running time entry state
									this.RefreshCache();
								}
								catch (Exception exception)
								{
									this._context.API.LogException("TogglTrack", "Failed to start time entry", exception);
									this._context.API.ShowMsgError("Failed to start time entry.", exception.Message);
								}
								finally
								{
									this._selectedProjectId = -1;
								}
							});

							return true;
						},
					});
				}
				catch
				{
					var queryToFlag = string.Join(" ", query.SearchTerms.Take(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag)));

					results.Add(new Result
					{
						Title = "Usage Example",
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
			}

			// Use cached time entries here to ensure responsiveness
			var likelyPastTimeEntry = (await this._GetTimeEntries())?.FirstOrDefault()?.ToTimeEntry(me);
			if ((likelyPastTimeEntry is null) || (likelyPastTimeEntry.Stop is null))
			{
				return results;
			}

			results.Add(new Result
			{
				Title = $"Start {description}{((string.IsNullOrEmpty(description) ? string.Empty : " "))}at previous stop time",
				SubTitle = projectName,
				IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "start.png") ,
				AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
				Score = 10000,
				Action = c =>
				{
					Task.Run(async delegate
					{
						try
						{
							this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {workspaceId}, {description}, at previous stop time");

							// Force a new fetch to ensure correctness
							// User input has ended at this point so no responsiveness concerns
							var lastTimeEntry = (await this._GetTimeEntries(true))?.FirstOrDefault()?.ToTimeEntry(me);
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
								projectId: this._selectedProjectId,
								description: description,
								start: (DateTimeOffset)lastTimeEntry.StopDate
							))?.ToTimeEntry(me);

							if (createdTimeEntry?.Id is null)
							{
								throw new Exception("An API error was encountered.");
							}

							this._context.API.ShowMsg($"Started {createdTimeEntry.RawDescription}{((string.IsNullOrEmpty(description) ? string.Empty : " "))}at previous stop time", $"{projectName} | {createdTimeEntry.DetailedElapsed}", "start.png");

							// Update cached running time entry state
							this.RefreshCache();
						}
						catch (Exception exception)
						{
							this._context.API.LogException("TogglTrack", "Failed to start time entry at previous stop time", exception);
							this._context.API.ShowMsgError("Failed to start time entry.", exception.Message);
						}
						finally
						{
							this._selectedProjectId = -1;
						}
					});

					return true;
				},
			});

			return results;
		}

		internal async ValueTask<List<Result>> RequestEditEntry(CancellationToken token, Query query)
		{
			if (token.IsCancellationRequested)
			{
				this._selectedProjectId = -1;
				this._editProjectState = TogglTrack.EditProjectState.NoProjectChange;
				return new List<Result>();
			}

			var me = (await this._GetMe())?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var runningTimeEntry = (await this._GetRunningTimeEntry())?.ToTimeEntry(me);
			if (runningTimeEntry is null)
			{
				return new List<Result>
				{
					new Result
					{
						Title = $"No running time entry",
						SubTitle = "There is no current time entry to edit.",
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

			// Reset project selection if query emptied to 'tgl edit '
			if (query.SearchTerms.Length == (ArgumentIndices.Command + 1) && this._editProjectState == TogglTrack.EditProjectState.ProjectSelected)
			{
				this._selectedProjectId = -1;
				this._editProjectState = TogglTrack.EditProjectState.NoProjectChange;
			}

			if (this._editProjectState == TogglTrack.EditProjectState.NoProjectChange)
			{
				// Firstly set to current project
				this._selectedProjectId = runningTimeEntry.ProjectId;

				// If the -p flag exists, set up next request for project selection
				if (Array.IndexOf(query.SearchTerms, Settings.EditProjectFlag) != -1)
				{
					this._selectedProjectId = -1;
					this._editProjectState = TogglTrack.EditProjectState.NoProjectSelected;
					this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} ");
				}
			}

			if (this._selectedProjectId == -1)
			{
				var projects = new List<Result>
				{
					new Result
					{
						Title = "No Project",
						IcoPath = "edit.png",
						AutoCompleteText = $"{query.ActionKeyword} {Settings.EditCommand} ",
						// Ensure is 1 greater than the top-priority project
						Score = (me.Projects?.Count ?? 0) + 1,
						Action = c =>
						{
							this._selectedProjectId = null;
							this._editProjectState = TogglTrack.EditProjectState.ProjectSelected;
							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} no-project ", true);
							return false;
						},
					},
				};

				if (me.ActiveProjects is not null)
				{
					me.ActiveProjects.Sort((projectOne, projectTwo) => (projectTwo.ActualHours ?? 0) - (projectOne.ActualHours ?? 0));

					projects.AddRange(
						me.ActiveProjects.ConvertAll(project => new Result
						{
							Title = project.Name,
							SubTitle = $"{((project.ClientId is not null) ? $"{project.Client!.Name} | " : string.Empty)}{project.ElapsedString}",
							IcoPath = this._colourIconProvider.GetColourIcon(project.Colour, "edit.png"),
							AutoCompleteText = $"{query.ActionKeyword} {Settings.EditCommand} ",
							Score = me.ActiveProjects.Count - me.ActiveProjects.IndexOf(project),
							Action = c =>
							{
								this._selectedProjectId = project.Id;
								this._editProjectState = TogglTrack.EditProjectState.ProjectSelected;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} {project.KebabName} ", true);
								return false;
							},
						})
					);
				}

				string projectQuery = Main.ExtractFromQuery(query, ArgumentIndices.Project);
				return (string.IsNullOrWhiteSpace(projectQuery))
					? projects
					: projects.FindAll(result =>
					{
						return this._context.API.FuzzySearch(projectQuery, $"{result.Title} {Regex.Replace(result.SubTitle, @"(?: \| )?\d+ hours?$", string.Empty)}").Score > 0;
					});
			}

			var project = me.GetProject(this._selectedProjectId);

			string projectName = project?.WithClientName ?? "No Project";
			string description = Main.ExtractFromQuery(
				query,
				(this._editProjectState == TogglTrack.EditProjectState.ProjectSelected)
					? ArgumentIndices.DescriptionWithProject
					: ArgumentIndices.DescriptionWithoutProject
			);

			var results = new List<Result>
			{
				new Result
				{
					Title = (string.IsNullOrEmpty(description)) ? runningTimeEntry.Description : description,
					SubTitle = $"{projectName} | {runningTimeEntry.HumanisedElapsed} ({runningTimeEntry.DetailedElapsed})",
					IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "edit.png") ,
					AutoCompleteText = $"{query.ActionKeyword} {(string.IsNullOrEmpty(description) ? ($"{query.Search} {runningTimeEntry.Description}") : query.Search)}",
					Score = 10000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.Id}, {runningTimeEntry.Duration}, {runningTimeEntry.Start}, {this._selectedProjectId}, {runningTimeEntry.WorkspaceId}, {description}");
								
								var editedTimeEntry = (await this._client.EditTimeEntry(
									workspaceId: runningTimeEntry.WorkspaceId,
									projectId: this._selectedProjectId,
									id: runningTimeEntry.Id,
									description: description,
									duration: runningTimeEntry.Duration,
									tags: runningTimeEntry.Tags,
									billable: runningTimeEntry.Billable
								))?.ToTimeEntry(me);

								if (editedTimeEntry?.Id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this._context.API.ShowMsg($"Edited {editedTimeEntry.RawDescription}", $"{projectName} | {runningTimeEntry.DetailedElapsed}", "edit.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to edit time entry", exception);
								this._context.API.ShowMsgError("Failed to edit time entry.", exception.Message);
							}
							finally
							{
								this._selectedProjectId = -1;
								this._editProjectState = TogglTrack.EditProjectState.NoProjectChange;
							}
						});

						return true;
					},
				},
			};

			if (!query.SearchTerms.Contains(Settings.TimeSpanFlag))
			{
				results.Add(new Result
				{
					Title = "Usage Tip",
					SubTitle = $"Use {Settings.TimeSpanFlag} after the description to edit the start time",
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
			else
			{
				try
				{
					var startTimeSpan = TimeSpanParser.Parse(
						Main.ExtractFromQuery(query, Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag) + 1),
						new TimeSpanParserOptions
						{
							UncolonedDefault = Units.Minutes,
							ColonedDefault = Units.Minutes,
						}
					);
					// An exception will be thrown if a time span was not able to be parsed
					// If we get here, there will have been a valid time span
					var startTime = runningTimeEntry.StartDate + startTimeSpan;
					var newElapsed = runningTimeEntry.Elapsed.Subtract(startTimeSpan);

					// Remove -t flag from description
					string sanitisedDescription = string.Join(
						" ",
						query.SearchTerms
							.Take(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag))
							.Skip(
								(this._editProjectState == TogglTrack.EditProjectState.ProjectSelected)
									? ArgumentIndices.DescriptionWithProject
									: ArgumentIndices.DescriptionWithoutProject
							)
					);

					results.Add(new Result
					{
						Title = (string.IsNullOrEmpty(sanitisedDescription))
							? runningTimeEntry.Description
							: sanitisedDescription,
						SubTitle = $"{projectName} | {newElapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")})",
						IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "edit.png") ,
						AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
						Score = 100000,
						Action = c =>
						{
							Task.Run(async delegate
							{
								try
								{
									this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.Id}, {runningTimeEntry.Duration}, {runningTimeEntry.Start}, {this._selectedProjectId}, {runningTimeEntry.WorkspaceId}, {sanitisedDescription}, {startTime.ToString("yyyy-MM-ddTHH:mm:ssZ")}, {startTimeSpan.ToString()}, edit start time");
									
									var editedTimeEntry = (await this._client.EditTimeEntry(
										workspaceId: runningTimeEntry.WorkspaceId,
										projectId: this._selectedProjectId,
										id: runningTimeEntry.Id,
										description: sanitisedDescription,
										start: startTime,
										duration: runningTimeEntry.Duration,
										tags: runningTimeEntry.Tags,
										billable: runningTimeEntry.Billable
									))?.ToTimeEntry(me);

									if (editedTimeEntry?.Id is null)
									{
										throw new Exception("An API error was encountered.");
									}

									this._context.API.ShowMsg($"Edited {editedTimeEntry.RawDescription}", $"{projectName} | {(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")}", "edit.png");

									// Update cached running time entry state
									this.RefreshCache();
								}
								catch (Exception exception)
								{
									this._context.API.LogException("TogglTrack", "Failed to edit time entry", exception);
									this._context.API.ShowMsgError("Failed to edit time entry.", exception.Message);
								}
								finally
								{
									this._selectedProjectId = -1;
									this._editProjectState = TogglTrack.EditProjectState.NoProjectChange;
								}
							});

							return true;
						},
					});
				}
				catch
				{
					var queryToFlag = string.Join(" ", query.SearchTerms.Take(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag)));

					results.Add(new Result
					{
						Title = "Usage Example",
						SubTitle = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} 5 mins",
						IcoPath = "tip.png",
						AutoCompleteText = $"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} 5 mins",
						Score = 100000,
						Action = c =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToFlag} {Settings.TimeSpanFlag} 5 mins");
							return false;
						}
					});
				}
			}

			if (this._editProjectState != TogglTrack.EditProjectState.NoProjectChange)
			{
				return results;
			}

			results.Add(new Result
			{
				Title = "Usage Tip",
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

		internal async ValueTask<List<Result>> RequestStopEntry(CancellationToken token, Query query)
		{
			if (token.IsCancellationRequested)
			{
				return new List<Result>();
			}

			var me = (await this._GetMe())?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var runningTimeEntry = (await this._GetRunningTimeEntry())?.ToTimeEntry(me);
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

			string projectName = runningTimeEntry.Project?.WithClientName ?? "No Project";

			var results = new List<Result>
			{
				new Result
				{
					Title = $"Stop {runningTimeEntry.Description} now",
					SubTitle = $"{projectName} | {runningTimeEntry.HumanisedElapsed} ({runningTimeEntry.DetailedElapsed})",
					IcoPath = this._colourIconProvider.GetColourIcon(runningTimeEntry.Project?.Colour, "stop.png") ,
					AutoCompleteText = $"{query.ActionKeyword} {Settings.StopCommand} {runningTimeEntry.Description}",
					Score = 10000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.Id}, {runningTimeEntry.WorkspaceId}, {runningTimeEntry.StartDate}, {runningTimeEntry.DetailedElapsed}");
								
								var stoppedTimeEntry = (await this._client.StopTimeEntry(
									workspaceId: runningTimeEntry.WorkspaceId,
									id: runningTimeEntry.Id
								))?.ToTimeEntry(me);

								if (stoppedTimeEntry?.Id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this._context.API.ShowMsg($"Stopped {stoppedTimeEntry.RawDescription}", $"{runningTimeEntry.DetailedElapsed} elapsed", "stop.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to stop time entry", exception);
								this._context.API.ShowMsgError("Failed to stop time entry.", exception.Message);
							}
						});

						return true;
					},
				},
			};

			if (!query.SearchTerms.Contains(Settings.TimeSpanFlag))
			{
				results.Add(new Result
				{
					Title = "Usage Tip",
					SubTitle = $"Use {Settings.TimeSpanFlag} to specify the stop time",
					IcoPath = "tip.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ",
					Score = 1,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{query.ActionKeyword} {query.Search} {Settings.TimeSpanFlag} ");
						return false;
					}
				});

				return results;
			}

			try
			{
				var stopTimeSpan = TimeSpanParser.Parse(
					Main.ExtractFromQuery(query, Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag) + 1),
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
					Title = $"Stop {runningTimeEntry.Description} {stopTime.Humanize()}",
					SubTitle = $"{projectName} | {newElapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")})",
					IcoPath = this._colourIconProvider.GetColourIcon(runningTimeEntry.Project?.Colour, "stop.png") ,
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Score = 100000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.Id}, {runningTimeEntry.WorkspaceId}, {runningTimeEntry.StartDate}, {(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")}, {stopTime}, time span flag");

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

								this._context.API.ShowMsg($"Stopped {stoppedTimeEntry.RawDescription}", $"{(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")} elapsed", "stop.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to stop time entry", exception);
								this._context.API.ShowMsgError("Failed to stop time entry.", exception.Message);
							}
						});

						return true;
					},
				});
			}
			catch
			{
				results.Add(new Result
				{
					Title = "Usage Example",
					SubTitle = $"{query.ActionKeyword} {Settings.StopCommand} {Settings.TimeSpanFlag} -5 mins",
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

			return results;
		}

		internal async ValueTask<List<Result>> RequestDeleteEntry(CancellationToken token)
		{
			if (token.IsCancellationRequested)
			{
				return new List<Result>();
			}

			var me = (await this._GetMe())?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var runningTimeEntry = (await this._GetRunningTimeEntry())?.ToTimeEntry(me);
			if (runningTimeEntry is null)
			{
				return new List<Result>
				{
					new Result
					{
						Title = $"No running time entry",
						SubTitle = "There is no current time entry to delete.",
						IcoPath = this._context.CurrentPluginMetadata.IcoPath,
						Action = c =>
						{
							return true;
						},
					},
				};
			}

			return new List<Result>
			{
				new Result
				{
					Title = $"Delete {runningTimeEntry.Description}",
					SubTitle = $"{runningTimeEntry.Project?.WithClientName ?? "No Project"} | {runningTimeEntry.HumanisedElapsed} ({runningTimeEntry.DetailedElapsed})",
					IcoPath = this._colourIconProvider.GetColourIcon(runningTimeEntry.Project?.Colour, "delete.png") ,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} {runningTimeEntry.Description}",
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.Id}, {runningTimeEntry.WorkspaceId}, {runningTimeEntry.StartDate}, {runningTimeEntry.DetailedElapsed}");
								
								var statusCode = await this._client.DeleteTimeEntry(
									workspaceId: runningTimeEntry.WorkspaceId,
									id: runningTimeEntry.Id
								);

								if (statusCode is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this._context.API.ShowMsg($"Deleted {runningTimeEntry.RawDescription}", $"{runningTimeEntry.DetailedElapsed} elapsed", "delete.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to delete time entry", exception, "RequestDeleteEntry");
								this._context.API.ShowMsgError("Failed to delete time entry.", exception.Message);
							}
						});

						return true;
					},
				},
			};
		}

		internal async ValueTask<List<Result>> RequestContinueEntry(CancellationToken token, Query query)
		{
			if (token.IsCancellationRequested)
			{
				return new List<Result>();
			}

			var me = (await this._GetMe())?.ToMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var timeEntries = (await this._GetTimeEntries())?.ConvertAll(timeEntry => timeEntry.ToTimeEntry(me));
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

			var entries = timeEntries.ConvertAll(timeEntry => new Result
			{
				Title = timeEntry.Description,
				SubTitle = $"{timeEntry.Project?.WithClientName ?? "No Project"} | {timeEntry.HumanisedElapsed} ({timeEntry.HumanisedStart})",
				IcoPath = this._colourIconProvider.GetColourIcon(timeEntry.Project?.Colour, "continue.png") ,
				AutoCompleteText = $"{query.ActionKeyword} {Settings.ContinueCommand} {timeEntry.Description}",
				Score = timeEntries.Count - timeEntries.IndexOf(timeEntry),
				Action = c =>
				{
					this._selectedProjectId = timeEntry.ProjectId;
					this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {timeEntry.Project?.KebabName ?? "no-project"} {timeEntry.RawDescription}");
					return false;
				},
			});

			string entriesQuery = Main.ExtractFromQuery(query, ArgumentIndices.Description);
			return (string.IsNullOrWhiteSpace(entriesQuery))
				? entries
				: entries.FindAll(result =>
				{
					return this._context.API.FuzzySearch(entriesQuery, result.Title).Score > 0;
				});
		}

		internal async ValueTask<List<Result>> RequestViewReports(CancellationToken token, Query query)
		{
			if (token.IsCancellationRequested)
			{
				this._selectedProjectId = -1;
				this._selectedClientId = -1;
				return new List<Result>();
			}

			var me = (await this._GetMe())?.ToMe();
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
					_ = this._GetRunningTimeEntry(true);
				});
			}

			else if (query.SearchTerms.Length == ArgumentIndices.GroupingName)
			{
				this._selectedProjectId = -1;
				this._selectedClientId = -1;
			}

			/* 
			 * Report span selection --- tgl view [day | week | month | year]
			 */

			if ((query.SearchTerms.Length == ArgumentIndices.Span) || !Settings.ReportsSpanArguments.Exists(span => Regex.IsMatch(query.SearchTerms[ArgumentIndices.Span], $"{span.Argument}({Settings.ReportsSpanOffsetRegex})?")))
			{
				string spanQuery = Main.ExtractFromQuery(query, ArgumentIndices.Span);
				string queryToSpan = string.Join(" ", query.SearchTerms.Take(ArgumentIndices.Span));

				// Implementation of eg '-5' to set span to be 5 [days | weeks | months | years] ago
				Match spanOffsetMatch = Settings.ReportsSpanOffsetRegex.Match(spanQuery);
				int spanOffset = (spanOffsetMatch.Success)
					? int.Parse(spanOffsetMatch.Groups[1].Value)
					: 0;

				var spans = Settings.ReportsSpanArguments.ConvertAll(span =>
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

				if (!spanOffsetMatch.Success)
				{
					bool queryContainsDash = spanQuery.Contains("-");

					spans.Add((queryContainsDash)
						? new Result
						{
							Title = "Usage Example",
							SubTitle = $"{query.ActionKeyword} {queryToSpan} -1",
							IcoPath = "tip.png",
							AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} -1 ",
							Score = 100000,
							Action = c =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} -1 ");
								return false;
							}
						}
						: new Result
						{
							Title = "Usage Tip",
							SubTitle = $"Use -<number> to view past reports",
							IcoPath = "tip.png",
							AutoCompleteText = $"{query.ActionKeyword} {queryToSpan} -",
							Score = 1,
							Action = c =>
							{
								this._context.API.ChangeQuery($"{query.ActionKeyword} {queryToSpan} -");
								return false;
							}
						}
					);
				}

				string sanitisedSpanQuery = Settings.ReportsSpanOffsetRegex.Replace(spanQuery, string.Empty).Replace("-", string.Empty);

				return (string.IsNullOrWhiteSpace(sanitisedSpanQuery))
					? spans
					: spans.FindAll(result =>
					{
						return this._context.API.FuzzySearch(sanitisedSpanQuery, result.Title).Score > 0;
					});
			}

			/* 
			 * Report groupinging selection --- tgl view [duration] [projects | clients | entries]
			 */
			if ((query.SearchTerms.Length == ArgumentIndices.Grouping) || !Settings.ReportsGroupingArguments.Exists(grouping => grouping.Argument == query.SearchTerms[ArgumentIndices.Grouping]))
			{
				string queryToGrouping = string.Join(" ", query.SearchTerms.Take(ArgumentIndices.Grouping));

				var groupings = Settings.ReportsGroupingArguments.ConvertAll(grouping => new Result
				{
					Title = grouping.Argument,
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

				string groupingsQuery = Main.ExtractFromQuery(query, ArgumentIndices.Grouping);
				return (string.IsNullOrWhiteSpace(groupingsQuery))
					? groupings
					: groupings.FindAll(result =>
					{
						return this._context.API.FuzzySearch(groupingsQuery, result.Title).Score > 0;
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

			var summary = (await this._GetSummaryTimeEntries(
				workspaceId: me.DefaultWorkspaceId,
				userId: me.Id,
				reportGrouping: groupingConfiguration.Grouping,
				start: start,
				end: end
			))?.ToSummaryTimeEntry(me);

			// Use cached time entry here to improve responsiveness
			var runningTimeEntry = (await this._GetRunningTimeEntry())?.ToTimeEntry(me);
			if ((runningTimeEntry is not null) && ((runningTimeEntry.StartDate.Date >= start.Date) && ((runningTimeEntry.StartDate).Date <= end.Date)))
			{
				summary = summary?.InsertRunningTimeEntry(runningTimeEntry, groupingConfiguration.Grouping);
			}

			var total = summary?.Elapsed ?? TimeSpan.Zero;

			var results = new List<Result>
			{
				new Result
				{
					Title = $"{total.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {spanConfiguration.Interpolation(spanArgumentOffset)} ({(int)total.TotalHours}:{total.ToString(@"mm\:ss")})",
					IcoPath = "reports.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
					Score = (int)total.TotalSeconds + 1000,
				},
			};

			if (summary is null)
			{
				return results;
			}

			switch (groupingConfiguration.Grouping)
			{
				case (Settings.ReportsGroupingKeys.Projects):
				{
					if (this._selectedProjectId == -1)
					{
						results.AddRange(
							summary.Groups.Values.Select(group => new Result
							{
								Title = group.Project?.Name ?? "No Project",
								SubTitle = $"{((group.Project?.ClientId is not null) ? $"{group.Project.Client!.Name} | " : string.Empty)}{group.HumanisedElapsed} ({group.DetailedElapsed})",
								IcoPath = this._colourIconProvider.GetColourIcon(group.Project?.Colour, "reports.png") ,
								AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} ",
								Score = (int)group.Seconds,
								Action = c =>
								{
									this._selectedProjectId = group.Project?.Id;
									this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {group.Project?.KebabName ?? "no-project"} ", true);
									return false;
								}
							})
						);
						break;
					}

					var selectedProjectGroup = summary.GetGroup(this._selectedProjectId);

					if (selectedProjectGroup?.SubGroups is null)
					{
						break;
					}
					
					var project = me.GetProject(selectedProjectGroup.Id);

					var subResults = selectedProjectGroup.SubGroups.Values.Select(subGroup => new Result
					{
						Title = subGroup.Title,
						SubTitle = $"{subGroup.HumanisedElapsed} ({subGroup.DetailedElapsed})",
						IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "reports.png") ,
						AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {project?.KebabName ?? "no-project"} {subGroup.Title}",
						Score = (int)subGroup.Elapsed.TotalSeconds,
						Action = c =>
						{
							this._selectedProjectId = project?.Id;
							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project?.KebabName ?? "no-project"} {subGroup.RawTitle}");
							return false;
						},
					});

					subResults = subResults.Append(new Result
					{
						Title = $"{selectedProjectGroup.HumanisedElapsed} tracked {spanConfiguration.Interpolation(spanArgumentOffset)} ({selectedProjectGroup.DetailedElapsed})",
						SubTitle = project?.WithClientName ?? "No Project",
						IcoPath = "reports.png",
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
						Score = (int)selectedProjectGroup.Elapsed.TotalSeconds + 1000,
					});

					string subNameQuery = Main.ExtractFromQuery(query, ArgumentIndices.SubGroupingName);
					return ((string.IsNullOrWhiteSpace(subNameQuery))
						? subResults
						: subResults.Where(result => this._context.API.FuzzySearch(subNameQuery, result.Title).Score > 0)
					).ToList();
				}
				case (Settings.ReportsGroupingKeys.Clients):
				{
					if (this._selectedClientId == -1)
					{	
						results.AddRange(
							summary.Groups.Values.Select(group =>
							{
								var longestProject = me.GetProject(group.LongestSubGroup?.Id);

								return new Result
								{
									Title = group.Client?.Name ?? "No Client",
									SubTitle = $"{group.HumanisedElapsed} ({group.DetailedElapsed})",
									IcoPath = this._colourIconProvider.GetColourIcon(longestProject?.Colour, "reports.png") ,
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} ",
									Score = (int)group.Seconds,
									Action = c =>
									{
										this._selectedClientId = group.Client?.Id;
										this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {group.Client?.KebabName ?? "no-client"} ", true);
										return false;
									}
								};
							})
						);
						break;
					}

					var selectedClientGroup = summary.GetGroup(this._selectedClientId);

					if (selectedClientGroup?.SubGroups is null)
					{
						break;
					}
					
					var client = me.GetClient(selectedClientGroup.Id);

					var subResults = selectedClientGroup.SubGroups.Values.Select(subGroup =>
					{
						var project = me.GetProject(subGroup.Id);

						return new Result
						{
							Title = project?.Name ?? "No Project",
							SubTitle = $"{((client?.Id is not null) ? $"{client.Name} | " : string.Empty)}{subGroup.HumanisedElapsed} ({subGroup.DetailedElapsed})",
							IcoPath = this._colourIconProvider.GetColourIcon(project?.Colour, "reports.png") ,
							AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {client?.KebabName ?? "no-client"} ",
							Score = (int)subGroup.Seconds,
							Action = c =>
							{
								this._selectedClientId = -1;
								this._selectedProjectId = project?.Id;

								if (string.IsNullOrEmpty(groupingConfiguration.SubArgument))
								{
									throw new Exception("Invalid ViewGroupingCommandArgument configuration: Missing 'SubArgument' field.");
								}

								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingConfiguration.SubArgument} {project?.KebabName ?? "no-project"} ", true);
								return false;
							}
						};
					});

					subResults = subResults.Append(new Result
					{
						Title = $"{selectedClientGroup.HumanisedElapsed} tracked {spanConfiguration.Interpolation(spanArgumentOffset)} ({selectedClientGroup.DetailedElapsed})",
						SubTitle = client?.Name ?? "No Client",
						IcoPath = "reports.png",
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
						Score = (int)selectedClientGroup.Elapsed.TotalSeconds + 1000,
					});

					string subNameQuery = Main.ExtractFromQuery(query, ArgumentIndices.SubGroupingName);
					return ((string.IsNullOrWhiteSpace(subNameQuery))
						? subResults
						: subResults.Where(result => this._context.API.FuzzySearch(subNameQuery, result.Title).Score > 0)
					).ToList();
				}
				case (Settings.ReportsGroupingKeys.Entries):
				{
					results.AddRange(
						summary.Groups.Values.SelectMany(group =>
						{
							if (group.SubGroups is null)
							{
								return Enumerable.Empty<Result>();
							}

							return group.SubGroups.Values.Select(subGroup => new Result
							{
								Title = subGroup.Title,
								SubTitle = $"{group.Project?.WithClientName ?? "No Project"} | {subGroup.HumanisedElapsed} ({subGroup.DetailedElapsed})",
								IcoPath = this._colourIconProvider.GetColourIcon(group.Project?.Colour, "reports.png") ,
								AutoCompleteText = $"{query.ActionKeyword} {Settings.ReportsCommand} {spanArgument} {groupingArgument} {subGroup.Title}",
								Score = (int)subGroup.Elapsed.TotalSeconds,
								Action = c =>
								{
									this._selectedProjectId = group.Project?.Id;
									this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {group.Project?.KebabName ?? "no-project"} {subGroup.RawTitle}");
									return false;
								},
							});
						})
					);
					
					break;
				}
			}

			string nameQuery = Main.ExtractFromQuery(query, ArgumentIndices.GroupingName);
			return (string.IsNullOrWhiteSpace(nameQuery))
				? results
				: results.FindAll(result =>
				{
					return this._context.API.FuzzySearch(nameQuery, result.Title).Score > 0;
				});
		}
	}
}