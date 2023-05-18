using System;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Caching;
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

		private MemoryCache _cache = MemoryCache.Default;
		private List<string> _summaryTimeEntriesCacheKeys = new List<string>();

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
		}

		private async ValueTask<Me?> _GetMe(bool force = false)
		{
			const string cacheKey = "Me";

			if (!force && this._cache.Contains(cacheKey))
			{
				return (Me?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching me", "_GetMe");
				
				var me = await this._client.GetMe();

				#pragma warning disable CS8604 // Possible null reference argument
				this._cache.Set(cacheKey, me, DateTimeOffset.Now.AddDays(3));
				#pragma warning restore CS8604 // Possible null reference argument

				return me;
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch me", exception, "_GetMe");
				return null;
			}
		}

		private async ValueTask<TimeEntry?> _GetRunningTimeEntry(bool force = false)
		{
			const string cacheKey = "RunningTimeEntry";

			if (!force && this._cache.Contains(cacheKey))
			{
				return (TimeEntry?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching running time entry", "_GetRunningTimeEntry");
				
				var runningTimeEntry = await this._client.GetRunningTimeEntry();

				#pragma warning disable CS8604 // Possible null reference argument
				this._cache.Set(cacheKey, runningTimeEntry, DateTimeOffset.Now.AddSeconds(30));
				#pragma warning restore CS8604 // Possible null reference argument

				return runningTimeEntry;
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch running time entry", exception, "_GetRunningTimeEntry");
				return null;
			}
		}

		private async ValueTask<List<TimeEntry>?> _GetTimeEntries(bool force = false)
		{
			const string cacheKey = "TimeEntries";

			if (!force && this._cache.Contains(cacheKey))
			{
				return (List<TimeEntry>?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching time entries", "_GetTimeEntries");
				
				var timeEntries = await this._client.GetTimeEntries();

				#pragma warning disable CS8604 // Possible null reference argument
				this._cache.Set(cacheKey, timeEntries, DateTimeOffset.Now.AddSeconds(30));
				#pragma warning restore CS8604 // Possible null reference argument

				return timeEntries;
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch time entries", exception, "_GetTimeEntries");
				return null;
			}
		}

		private async ValueTask<SummaryTimeEntry?> _GetSummaryTimeEntries(long workspaceId, long userId, Settings.ViewGroupingKeys reportGrouping, DateTimeOffset start, DateTimeOffset? end, bool force = false)
		{
			string cacheKey = $"SummaryTimeEntries{workspaceId}{userId}{(int)reportGrouping}{start.ToString("yyyy-MM-dd")}{end?.ToString("yyyy-MM-dd")}";

			if (!force && this._cache.Contains(cacheKey))
			{
				return (SummaryTimeEntry?)this._cache.Get(cacheKey);
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching summary time entries for reports", "_GetSummaryTimeEntries");
				
				var summary = await this._client.GetSummaryTimeEntries(workspaceId, userId, reportGrouping, start, end);

				#pragma warning disable CS8604 // Possible null reference argument
				this._cache.Set(cacheKey, summary, DateTimeOffset.Now.AddSeconds(30));
				#pragma warning restore CS8604 // Possible null reference argument

				this._summaryTimeEntriesCacheKeys.Add(cacheKey);

				return summary;
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch summary time entries for reports", exception, "_GetSummaryTimeEntries");
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

			// TODO: this equal does not work

			if (this._settings.ApiToken.Equals(this._lastToken.Token))
			{
				return this._lastToken.IsValid;
			}

			this._lastToken.Token = this._settings.ApiToken;

			if (string.IsNullOrWhiteSpace(this._settings.ApiToken))
			{
				return this._lastToken.IsValid = false;
			}

			this._client.UpdateToken(this._settings.ApiToken);

			this._lastToken.IsValid = (await this._GetMe(true))?.api_token?.Equals(this._settings.ApiToken) ?? false;
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
				}
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
				}
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
					Title = Settings.ViewCommand,
					SubTitle = "View tracked time reports",
					IcoPath = "view.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ViewCommand} ",
					Score = 5,
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ViewCommand} ");
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

			var me = await this._GetMe();
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
						Score = (me.projects?.Count ?? 0) + 1,
						Action = c =>
						{
							this._selectedProjectId = null;
							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} no-project ", true);
							return false;
						},
					},
				};

				if (me.projects is not null)
				{
					var filteredProjects = me.projects.FindAll(project => project.active ?? false);
					filteredProjects.Sort((projectOne, projectTwo) => (projectTwo.actual_hours ?? 0) - (projectOne.actual_hours ?? 0));

					projects.AddRange(
						filteredProjects.ConvertAll(project => new Result
						{
							Title = project.name,
							SubTitle = $"{((project.client_id is not null) ? $"{me.clients?.Find(client => client.id == project.client_id)?.name} | " : string.Empty)}{project.actual_hours ?? 0} {(((project.actual_hours ?? 0) != 1) ? "hours" : "hour")}",
							IcoPath = (project.color is not null)
								? new ColourIcon(this._context, project.color, "start.png").GetColourIcon()
								: "start.png",
							AutoCompleteText = $"{query.ActionKeyword} {Settings.StartCommand} ",
							Score = filteredProjects.Count - filteredProjects.IndexOf(project),
							Action = c =>
							{
								this._selectedProjectId = project.id;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project.name?.Kebaberize()} ", true);
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

			var project = me.projects?.Find(project => project.id == this._selectedProjectId);
			var client = me.clients?.Find(client => client.id == project?.client_id);
			long workspaceId = project?.workspace_id ?? me.default_workspace_id;

			string clientName = (client is not null)
				? $" • {client.name}"
				: string.Empty;
			string projectName = (project is not null)
				? $"{project.name}{clientName}"
				: "No Project";

			string description = Main.ExtractFromQuery(query, ArgumentIndices.Description);

			var results = new List<Result>
			{
				new Result
				{
					Title = $"Start {description}{((string.IsNullOrEmpty(description) ? string.Empty : " "))}now",
					SubTitle = projectName,
					IcoPath = (project?.color is not null)
						? new ColourIcon(this._context, project.color, "start.png").GetColourIcon()
						: "start.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Score = 10000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {workspaceId}, {description}", "RequestStartEntry");
								
								// TODO: billable
								var createdTimeEntry = await this._client.CreateTimeEntry(this._selectedProjectId, workspaceId, description, null, null, null);
								if (createdTimeEntry?.id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this._context.API.ShowMsg($"Started {createdTimeEntry.description}", projectName, "start.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to start time entry", exception, "RequestStartEntry");
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
					// An exception will be thrown if a time span was not able to be parsed
					// If we get here, there will have been a valid time span
					var startTime = DateTimeOffset.UtcNow + startTimeSpan;

					// Remove -t flag from description
					string sanitisedDescription = string.Join(" ", query.SearchTerms.Take(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag)).Skip(ArgumentIndices.Description));

					results.Add(new Result
					{
						Title = $"Start {sanitisedDescription}{((string.IsNullOrEmpty(sanitisedDescription) ? string.Empty : " "))}{startTime.Humanize()}",
						SubTitle = projectName,
						IcoPath = (project?.color is not null)
							? new ColourIcon(this._context, project.color, "start.png").GetColourIcon()
							: "start.png",
						AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
						Score = 100000,
						Action = c =>
						{
							Task.Run(async delegate
							{
								try
								{
									this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {workspaceId}, {sanitisedDescription}, {startTimeSpan.ToString()}, time span flag", "RequestStartEntry");
									
									// TODO: billable
									var createdTimeEntry = await this._client.CreateTimeEntry(this._selectedProjectId, workspaceId, sanitisedDescription, startTime, null, null);
									if (createdTimeEntry?.id is null)
									{
										throw new Exception("An API error was encountered.");
									}

									this._context.API.ShowMsg($"Started {createdTimeEntry.description}{((string.IsNullOrEmpty(sanitisedDescription) ? string.Empty : " "))}{startTime.Humanize()}", projectName, "start.png");

									// Update cached running time entry state
									this.RefreshCache();
								}
								catch (Exception exception)
								{
									this._context.API.LogException("TogglTrack", "Failed to start time entry", exception, "RequestStartEntry");
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
			var likelyPastTimeEntry = (await this._GetTimeEntries())?.FirstOrDefault();
			if ((likelyPastTimeEntry is null) || (likelyPastTimeEntry.stop is null))
			{
				return results;
			}

			results.Add(new Result
			{
				Title = $"Start {description}{((string.IsNullOrEmpty(description) ? string.Empty : " "))}at previous stop time",
				SubTitle = projectName,
				IcoPath = (project?.color is not null)
						? new ColourIcon(this._context, project.color, "start.png").GetColourIcon()
						: "start.png",
				AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
				Score = 10000,
				Action = c =>
				{
					Task.Run(async delegate
					{
						try
						{
							this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {workspaceId}, {description}, at previous stop time", "RequestStartEntry");

							// Force a new fetch to ensure correctness
							// User input has ended at this point so no responsiveness concerns
							var lastTimeEntry = (await this._GetTimeEntries(true))?.FirstOrDefault();
							if (lastTimeEntry is null)
							{
								throw new Exception("There is no previous time entry.");
							}
							else if (lastTimeEntry.stop is null)
							{
								throw new Exception("A time entry is currently running.");
							}

							// TODO: billable
							var createdTimeEntry = await this._client.CreateTimeEntry(this._selectedProjectId, workspaceId, description, DateTimeOffset.Parse(lastTimeEntry.stop), null, null);
							if (createdTimeEntry?.id is null)
							{
								throw new Exception("An API error was encountered.");
							}

							this._context.API.ShowMsg($"Started {createdTimeEntry.description}{((string.IsNullOrEmpty(description) ? string.Empty : " "))}at previous stop time", projectName, "start.png");

							// Update cached running time entry state
							this.RefreshCache();
						}
						catch (Exception exception)
						{
							this._context.API.LogException("TogglTrack", "Failed to start time entry at previous stop time", exception, "RequestStartEntry");
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

			var me = await this._GetMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var runningTimeEntry = await this._GetRunningTimeEntry();
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
				this._selectedProjectId = runningTimeEntry.project_id;

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
						Score = (me.projects?.Count ?? 0) + 1,
						Action = c =>
						{
							this._selectedProjectId = null;
							this._editProjectState = TogglTrack.EditProjectState.ProjectSelected;
							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} no-project ", true);
							return false;
						},
					},
				};

				if (me.projects is not null)
				{
					var filteredProjects = me.projects.FindAll(project => project.active ?? false);
					filteredProjects.Sort((projectOne, projectTwo) => (projectTwo.actual_hours ?? 0) - (projectOne.actual_hours ?? 0));

					projects.AddRange(
						filteredProjects.ConvertAll(project => new Result
						{
							Title = project.name,
							SubTitle = $"{((project.client_id is not null) ? $"{me.clients?.Find(client => client.id == project.client_id)?.name} | " : string.Empty)}{project.actual_hours ?? 0} {(((project.actual_hours ?? 0) != 1) ? "hours" : "hour")}",
							IcoPath = (project.color is not null)
								? new ColourIcon(this._context, project.color, "edit.png").GetColourIcon()
								: "edit.png",
							AutoCompleteText = $"{query.ActionKeyword} {Settings.EditCommand} ",
							Score = filteredProjects.Count - filteredProjects.IndexOf(project),
							Action = c =>
							{
								this._selectedProjectId = project.id;
								this._editProjectState = TogglTrack.EditProjectState.ProjectSelected;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.EditCommand} {project.name?.Kebaberize()} ", true);
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

			var startDate = DateTimeOffset.Parse(runningTimeEntry.start!);
			var elapsed = DateTimeOffset.UtcNow.Subtract(startDate);

			var project = me.projects?.Find(project => project.id == this._selectedProjectId);
			var client = me.clients?.Find(client => client.id == project?.client_id);

			string clientName = (client is not null)
				? $" • {client.name}"
				: string.Empty;
			string projectName = (project is not null)
				? $"{project.name}{clientName}"
				: "No Project";

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
					Title = (string.IsNullOrEmpty(description)) ? ((string.IsNullOrEmpty(runningTimeEntry.description)) ? "(no description)" : runningTimeEntry.description) : description,
					SubTitle = $"{projectName} | {elapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")})",
					IcoPath = (project?.color is not null)
						? new ColourIcon(this._context, project.color, "edit.png").GetColourIcon()
						: "edit.png",
					AutoCompleteText = $"{query.ActionKeyword} {(string.IsNullOrEmpty(description) ? ($"{query.Search} {((string.IsNullOrEmpty(runningTimeEntry.description)) ? "(no description)" : runningTimeEntry.description)}") : query.Search)}",
					Score = 10000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.id}, {runningTimeEntry.duration}, {runningTimeEntry.start}, {this._selectedProjectId}, {runningTimeEntry.workspace_id}, {description}", "RequestEditEntry");
								
								var editedTimeEntry = await this._client.EditTimeEntry(runningTimeEntry, this._selectedProjectId, description, null, null);
								if (editedTimeEntry?.id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this._context.API.ShowMsg($"Edited {editedTimeEntry.description}", $"{projectName} | {(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")}", "edit.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to edit time entry", exception, "RequestEditEntry");
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
					var startTime = startDate + startTimeSpan;
					var newElapsed = elapsed.Subtract(startTimeSpan);

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
						Title = (string.IsNullOrEmpty(sanitisedDescription)) ? ((string.IsNullOrEmpty(runningTimeEntry.description)) ? "(no description)" : runningTimeEntry.description) : sanitisedDescription,
						SubTitle = $"{projectName} | {newElapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")})",
						IcoPath = (project?.color is not null)
							? new ColourIcon(this._context, project.color, "edit.png").GetColourIcon()
							: "edit.png",
						AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
						Score = 100000,
						Action = c =>
						{
							Task.Run(async delegate
							{
								try
								{
									this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.id}, {runningTimeEntry.duration}, {runningTimeEntry.start}, {this._selectedProjectId}, {runningTimeEntry.workspace_id}, {sanitisedDescription}, {startTime.ToString("yyyy-MM-ddTHH:mm:ssZ")}, {startTimeSpan.ToString()}, edit start time", "RequestEditEntry");
									
									var editedTimeEntry = await this._client.EditTimeEntry(runningTimeEntry, this._selectedProjectId, sanitisedDescription, startTime, null);
									if (editedTimeEntry?.id is null)
									{
										throw new Exception("An API error was encountered.");
									}

									this._context.API.ShowMsg($"Edited {editedTimeEntry.description}", $"{projectName} | {(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")}", "edit.png");

									// Update cached running time entry state
									this.RefreshCache();
								}
								catch (Exception exception)
								{
									this._context.API.LogException("TogglTrack", "Failed to edit time entry", exception, "RequestEditEntry");
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

			var me = await this._GetMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var runningTimeEntry = await this._GetRunningTimeEntry();
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

			var startDate = DateTimeOffset.Parse(runningTimeEntry.start!);
			var elapsed = DateTimeOffset.UtcNow.Subtract(startDate);

			var project = me.projects?.Find(project => project.id == runningTimeEntry.project_id);
			var client = me.clients?.Find(client => client.id == project?.client_id);

			string clientName = (client is not null)
				? $" • {client.name}"
				: string.Empty;
			string projectName = (project is not null)
				? $"{project.name}{clientName}"
				: "No Project";

			var results = new List<Result>
			{
				new Result
				{
					Title = $"Stop {((string.IsNullOrEmpty(runningTimeEntry.description)) ? "(no description)" : runningTimeEntry.description)} now",
					SubTitle = $"{projectName} | {elapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")})",
					IcoPath = (project?.color is not null)
						? new ColourIcon(this._context, project.color, "stop.png").GetColourIcon()
						: "stop.png",
					AutoCompleteText = $"{query.ActionKeyword} {Settings.StopCommand} {((string.IsNullOrEmpty(runningTimeEntry.description)) ? "(no description)" : runningTimeEntry.description)}",
					Score = 10000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.id}, {runningTimeEntry.workspace_id}, {startDate}, {(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")}", "RequestStopEntry");
								
								var stoppedTimeEntry = await this._client.StopTimeEntry(runningTimeEntry.id, runningTimeEntry.workspace_id);
								if (stoppedTimeEntry?.id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this._context.API.ShowMsg($"Stopped {stoppedTimeEntry.description}", $"{(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")} elapsed", "stop.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to stop time entry", exception, "RequestStopEntry");
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
				if (stopTime.CompareTo(startDate) < 0)
				{
					// Ensure stop is not before start
					stopTime = startDate;
				}

				var newElapsed = stopTime.Subtract(startDate);

				results.Add(new Result
				{
					Title = $"Stop {((string.IsNullOrEmpty(runningTimeEntry.description)) ? "(no description)" : runningTimeEntry.description)} {stopTime.Humanize()}",
					SubTitle = $"{projectName} | {newElapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")})",
					IcoPath = (project?.color is not null)
							? new ColourIcon(this._context, project.color, "stop.png").GetColourIcon()
							: "stop.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Score = 100000,
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.id}, {runningTimeEntry.workspace_id}, {startDate}, {(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")}, {stopTime}, time span flag", "RequestStopEntry");

								var stoppedTimeEntry = await this._client.EditTimeEntry(runningTimeEntry, null, null, null, stopTime);
								if (stoppedTimeEntry?.id is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this._context.API.ShowMsg($"Stopped {stoppedTimeEntry.description}", $"{(int)newElapsed.TotalHours}:{newElapsed.ToString(@"mm\:ss")} elapsed", "stop.png");

								// Update cached running time entry state
								this.RefreshCache();
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to stop time entry", exception, "RequestStopEntry");
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

			var me = await this._GetMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var runningTimeEntry = await this._GetRunningTimeEntry();
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

			var startDate = DateTimeOffset.Parse(runningTimeEntry.start!);
			var elapsed = DateTimeOffset.UtcNow.Subtract(startDate);

			var project = me.projects?.Find(project => project.id == runningTimeEntry.project_id);
			var client = me.clients?.Find(client => client.id == project?.client_id);

			string clientName = (client is not null)
				? $" • {client.name}"
				: string.Empty;
			string projectName = (project is not null)
				? $"{project.name}{clientName}"
				: "No Project";

			return new List<Result>
			{
				new Result
				{
					Title = $"Delete {((string.IsNullOrEmpty(runningTimeEntry.description)) ? "(no description)" : runningTimeEntry.description)}",
					SubTitle = $"{projectName} | {elapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")})",
					IcoPath = (project?.color is not null)
						? new ColourIcon(this._context, project.color, "delete.png").GetColourIcon()
						: "delete.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.DeleteCommand} {((string.IsNullOrEmpty(runningTimeEntry.description)) ? "(no description)" : runningTimeEntry.description)}",
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.id}, {runningTimeEntry.workspace_id}, {startDate}, {(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")}", "RequestDeleteEntry");
								
								var statusCode = await this._client.DeleteTimeEntry(runningTimeEntry.id, runningTimeEntry.workspace_id);
								if (statusCode is null)
								{
									throw new Exception("An API error was encountered.");
								}

								this._context.API.ShowMsg($"Deleted {runningTimeEntry.description}", $"{(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")} elapsed", "delete.png");

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

			var me = await this._GetMe();
			if (me is null)
			{
				return this.NotifyUnknownError();
			}

			var timeEntries = await this._GetTimeEntries();
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

			var entries = timeEntries.ConvertAll(timeEntry => 
			{
				var elapsed = (timeEntry.duration < 0)
					? DateTimeOffset.UtcNow.Subtract(DateTimeOffset.Parse(timeEntry.start!))
					: TimeSpan.FromSeconds(timeEntry.duration);

				var project = me.projects?.Find(project => project.id == timeEntry.project_id);
				var client = me.clients?.Find(client => client.id == project?.client_id);
				long workspaceId = project?.workspace_id ?? me.default_workspace_id;

				string clientName = (client is not null)
					? $" • {client.name}"
					: string.Empty;
				string projectName = (project is not null)
					? $"{project.name}{clientName}"
					: "No Project";

				return new Result
				{
					Title = (string.IsNullOrEmpty(timeEntry.description)) ? "(no description)" : timeEntry.description,
					SubTitle = $"{projectName} | {elapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({DateTime.Parse(timeEntry.start!).Humanize(false)})",
					IcoPath = (project?.color is not null)
							? new ColourIcon(this._context, project.color, "continue.png").GetColourIcon()
							: "continue.png",
					AutoCompleteText = $"{query.ActionKeyword} {Settings.ContinueCommand} {((string.IsNullOrEmpty(timeEntry.description)) ? "(no description)" : timeEntry.description)}",
					Score = timeEntries.Count - timeEntries.IndexOf(timeEntry),
					Action = c =>
					{
						this._selectedProjectId = project?.id;
						this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project?.name?.Kebaberize() ?? "no-project"} {timeEntry.description}");
						return false;
					},
				};
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

			var me = await this._GetMe();
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

			if (query.SearchTerms.Length == ArgumentIndices.Span || !Settings.ViewSpanArguments.Exists(span => span.Argument == query.SearchTerms[ArgumentIndices.Span]))
			{
				var spans = Settings.ViewSpanArguments.ConvertAll(span =>
				{
					return new Result
					{
						Title = span.Argument,
						SubTitle = $"View tracked time report for {span.Interpolation}",
						IcoPath = "view.png",
						AutoCompleteText = $"{query.ActionKeyword} {Settings.ViewCommand} {span.Argument} ",
						Score = span.Score,
						Action = c =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ViewCommand} {span.Argument} ", true);
							return false;
						},
					};
				});

				string spanQuery = Main.ExtractFromQuery(query, ArgumentIndices.Span);
				return (string.IsNullOrWhiteSpace(spanQuery))
					? spans
					: spans.FindAll(result =>
					{
						return this._context.API.FuzzySearch(spanQuery, result.Title).Score > 0;
					});
			}

			/* 
			 * Report groupinging selection --- tgl view [duration] [projects | clients | entries]
			 */
			if (query.SearchTerms.Length == ArgumentIndices.Grouping || !Settings.ViewGroupingArguments.Exists(grouping => grouping.Argument == query.SearchTerms[ArgumentIndices.Grouping]))
			{
				var groupings = Settings.ViewGroupingArguments.ConvertAll(grouping =>
				{
					return new Result
					{
						Title = grouping.Argument,
						SubTitle = grouping.Interpolation,
						IcoPath = "view.png",
						AutoCompleteText = $"{query.ActionKeyword} {Settings.ViewCommand} {query.SearchTerms[1]} {grouping.Argument} ",
						Score = grouping.Score,
						Action = c =>
						{
							this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ViewCommand} {query.SearchTerms[1]} {grouping.Argument} ", true);
							return false;
						},
					};
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

			var spanConfiguration = Settings.ViewSpanArguments.Find(span => span.Argument == spanArgument);
			var groupingConfiguration = Settings.ViewGroupingArguments.Find(grouping => grouping.Argument == groupingArgument);

			if ((spanConfiguration is null) || (groupingConfiguration is null))
			{
				return this.NotifyUnknownError();
			}

			var start = spanConfiguration.Start(DateTimeOffset.Now);
			var end = spanConfiguration.End(DateTimeOffset.Now);
			
			this._context.API.LogInfo("TogglTrack", $"{spanArgument}, {groupingArgument}, {start}, {end}", "RequestViewReports");

			var summary = await this._GetSummaryTimeEntries(me.default_workspace_id, me.id, groupingConfiguration.Grouping, start, end);

			// Use cached time entry here to improve responsiveness
			var runningTimeEntry = await this._GetRunningTimeEntry();
			var runningElapsed = (runningTimeEntry is null)
				? TimeSpan.Zero
				: DateTimeOffset.UtcNow.Subtract(DateTimeOffset.Parse(runningTimeEntry.start!));

			var total = TimeSpan.FromSeconds(summary?.groups?.Sum(group => group.seconds) ?? 0) + runningElapsed;

			var results = new List<Result>
			{
				new Result
				{
					Title = $"{total.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {spanConfiguration.Interpolation} ({(int)total.TotalHours}:{total.ToString(@"mm\:ss")})",
					IcoPath = "view.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
					Score = (int)total.TotalSeconds + 1000,
				},
			};

			if ((summary is null) || (summary.groups is null))
			{
				return results;
			}

			switch (groupingConfiguration.Grouping)
			{
				case (Settings.ViewGroupingKeys.Projects):
				{
					if (runningTimeEntry is not null)
					{
						// Perform deep copy of summary so the cache is not mutated
						var serialisedSummary = JsonSerializer.Serialize<SummaryTimeEntry>(summary);
						summary = JsonSerializer.Deserialize<SummaryTimeEntry>(serialisedSummary);
						if ((summary is null) || (summary.groups is null))
						{
							return results;
						}

						var projectGroup = summary.groups.Find(group => group.id == runningTimeEntry.project_id);
						var entrySubGroup = projectGroup?.sub_groups?.Find(subGroup => subGroup.title == runningTimeEntry.description);

						if (entrySubGroup is not null)
						{
							entrySubGroup.seconds += (int)runningElapsed.TotalSeconds;
						}
						else if (projectGroup?.sub_groups is not null)
						{
							projectGroup.sub_groups.Add(new SummaryTimeEntrySubGroup
							{
								title = runningTimeEntry.description,
								seconds = (int)runningElapsed.TotalSeconds,
							});
						}
						else if (projectGroup is not null)
						{
							projectGroup.sub_groups = new List<SummaryTimeEntrySubGroup>
							{
								new SummaryTimeEntrySubGroup
								{
									title = runningTimeEntry.description,
									seconds = (int)runningElapsed.TotalSeconds,
								},
							};
						}
						else
						{
							summary.groups.Add(new SummaryTimeEntryGroup
							{
								id = runningTimeEntry.project_id,
								sub_groups = new List<SummaryTimeEntrySubGroup>
								{
									new SummaryTimeEntrySubGroup
									{
										title = runningTimeEntry.description,
										seconds = (int)runningElapsed.TotalSeconds,
									},
								},
							});
						}
					}

					if (this._selectedProjectId == -1)
					{
						results.AddRange(
							summary.groups.ConvertAll(group =>
							{
								var project = me.projects?.Find(project => project.id == group.id);
								var elapsed = TimeSpan.FromSeconds(group.seconds);

								return new Result
								{
									Title = project?.name ?? "No Project",
									SubTitle = $"{((project?.client_id is not null) ? $"{me.clients?.Find(client => client.id == project.client_id)?.name} | " : string.Empty)}{elapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")})",
									IcoPath = (project?.color is not null)
										? new ColourIcon(this._context, project.color, "view.png").GetColourIcon()
										: "view.png",
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ViewCommand} {spanConfiguration.Argument} {groupingConfiguration.Argument} ",
									Score = (int)group.seconds,
									Action = c =>
									{
										this._selectedProjectId = project?.id;
										this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ViewCommand} {spanConfiguration.Argument} {groupingConfiguration.Argument} {project?.name?.Kebaberize() ?? "No Project"} ", true);
										return false;
									}
								};
							})
						);
						break;
					}

					var selectedProjectGroup = summary.groups.Find(group => group.id == this._selectedProjectId);

					if (selectedProjectGroup?.sub_groups is null)
					{
						break;
					}
					
					var project = me.projects?.Find(project => project.id == selectedProjectGroup.id);
					var client = me.clients?.Find(client => client.id == project?.client_id);

					string clientName = (client is not null)
						? $" • {client.name}"
						: string.Empty;
					string projectName = (project is not null)
						? $"{project.name}{clientName}"
						: "No Project";

					var subResults = selectedProjectGroup.sub_groups.ConvertAll(subGroup =>
					{
						var elapsed = TimeSpan.FromSeconds(subGroup.seconds);

						return new Result
						{
							Title = (string.IsNullOrEmpty(subGroup.title)) ? "(no description)" : subGroup.title,
							SubTitle = $"{elapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")})",
							IcoPath = (project?.color is not null)
									? new ColourIcon(this._context, project.color, "view.png").GetColourIcon()
									: "view.png",
							//	TODO: project name
							AutoCompleteText = $"{query.ActionKeyword} {Settings.ViewCommand} {spanConfiguration.Argument} {groupingConfiguration.Argument} {((string.IsNullOrEmpty(subGroup.title)) ? "(no description)" : subGroup.title)}",
							Score = (int)elapsed.TotalSeconds,
							Action = c =>
							{
								this._selectedProjectId = project?.id;
								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project?.name?.Kebaberize() ?? "no-project"} {subGroup.title}");
								return false;
							},
						};
					});

					var subTotal = TimeSpan.FromSeconds(selectedProjectGroup.seconds);
					subResults.Add(new Result
					{
						Title = $"{subTotal.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {spanConfiguration.Interpolation} ({(int)subTotal.TotalHours}:{subTotal.ToString(@"mm\:ss")})",
						SubTitle = projectName,
						IcoPath = "view.png",
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
						Score = (int)subTotal.TotalSeconds + 1000,
					});

					string subNameQuery = Main.ExtractFromQuery(query, ArgumentIndices.SubGroupingName);
					return (string.IsNullOrWhiteSpace(subNameQuery))
						? subResults
						: subResults.FindAll(result =>
						{
							return this._context.API.FuzzySearch(subNameQuery, result.Title).Score > 0;
						});
				}
				case (Settings.ViewGroupingKeys.Clients):
				{
					if (runningTimeEntry is not null)
					{
						// Perform deep copy of summary so the cache is not mutated
						var serialisedSummary = JsonSerializer.Serialize<SummaryTimeEntry>(summary);
						summary = JsonSerializer.Deserialize<SummaryTimeEntry>(serialisedSummary);
						if ((summary is null) || (summary.groups is null))
						{
							return results;
						}

						Project? runningProject = me.projects?.Find(project => project.id == runningTimeEntry.project_id);

						if (runningProject?.client_id is not null)
						{
							var clientGroup = summary.groups.Find(group => group.id == runningProject.client_id);
							var projectSubGroup = clientGroup?.sub_groups?.Find(subGroup => subGroup.id == runningProject.id);

							if (projectSubGroup is not null)
							{
								projectSubGroup.seconds += (int)runningElapsed.TotalSeconds;
							}
							else if (clientGroup?.sub_groups is not null)
							{
								clientGroup.sub_groups.Add(new SummaryTimeEntrySubGroup
								{
									title = runningTimeEntry.description,
									seconds = (int)runningElapsed.TotalSeconds,
								});
							}
							else if (clientGroup is not null)
							{
								clientGroup.sub_groups = new List<SummaryTimeEntrySubGroup>
								{
									new SummaryTimeEntrySubGroup
									{
										title = runningTimeEntry.description,
										seconds = (int)runningElapsed.TotalSeconds,
									},
								};
							}
							else
							{
								summary.groups.Add(new SummaryTimeEntryGroup
								{
									id = runningTimeEntry.project_id,
									sub_groups = new List<SummaryTimeEntrySubGroup>
									{
										new SummaryTimeEntrySubGroup
										{
											title = runningTimeEntry.description,
											seconds = (int)runningElapsed.TotalSeconds,
										},
									},
								});
							}
						}
					}

					if (this._selectedClientId == -1)
					{	
						results.AddRange(
							summary.groups.ConvertAll(group =>
							{
								var client = me.clients?.Find(client => client.id == group.id);
								var elapsed = TimeSpan.FromSeconds(group.seconds);

								var highestProjectId = group.sub_groups?.MaxBy(subGroup => subGroup.seconds)?.id;
								var highestProject = me.projects?.Find(project => project.id == highestProjectId);

								return new Result
								{
									Title = client?.name ?? "No Client",
									SubTitle = $"{elapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")})",
									IcoPath = (highestProject?.color is not null)
										? new ColourIcon(this._context, highestProject.color, "view.png").GetColourIcon()
										: "view.png",
									// TODO: client name
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ViewCommand} {spanConfiguration.Argument} {groupingConfiguration.Argument} ",
									Score = (int)group.seconds,
									Action = c =>
									{
										this._selectedClientId = client?.id;
										this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ViewCommand} {spanConfiguration.Argument} {groupingConfiguration.Argument} {client?.name?.Kebaberize() ?? "No Client"} ", true);
										return false;
									}
								};
							})
						);
						break;
					}

					var selectedClientGroup = summary.groups.Find(group => group.id == this._selectedClientId);

					if (selectedClientGroup?.sub_groups is null)
					{
						break;
					}
					
					var client = me.clients?.Find(client => client.id == selectedClientGroup.id);

					var subResults = selectedClientGroup.sub_groups.ConvertAll(subGroup =>
					{
						var project = me.projects?.Find(project => project.id == subGroup.id);
						var elapsed = TimeSpan.FromSeconds(subGroup.seconds);

						return new Result
						{
							Title = project?.name ?? "No Project",
							SubTitle = $"{((client?.id is not null) ? $"{client?.name} | " : string.Empty)}{elapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")})",
							IcoPath = (project?.color is not null)
								? new ColourIcon(this._context, project.color, "view.png").GetColourIcon()
								: "view.png",
							AutoCompleteText = $"{query.ActionKeyword} {Settings.ViewCommand} {spanConfiguration.Argument} {groupingConfiguration.Argument} ",
							Score = (int)subGroup.seconds,
							Action = c =>
							{
								this._selectedClientId = -1;
								this._selectedProjectId = project?.id;

								if (string.IsNullOrEmpty(groupingConfiguration.SubArgument))
								{
									throw new Exception("Invalid ViewGroupingCommandArgument configuration: Missing 'SubArgument' field.");
								}

								this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.ViewCommand} {spanConfiguration.Argument} {groupingConfiguration.SubArgument} {project?.name?.Kebaberize() ?? "No Project"} ", true);
								return false;
							}
						};
					});

					var subTotal = TimeSpan.FromSeconds(selectedClientGroup.seconds);
					subResults.Add(new Result
					{
						Title = $"{subTotal.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} tracked {spanConfiguration.Interpolation} ({(int)subTotal.TotalHours}:{subTotal.ToString(@"mm\:ss")})",
						SubTitle = client?.name ?? "No Client",
						IcoPath = "view.png",
						AutoCompleteText = $"{query.ActionKeyword} {query.Search} ",
						Score = (int)subTotal.TotalSeconds + 1000,
					});

					string subNameQuery = Main.ExtractFromQuery(query, ArgumentIndices.SubGroupingName);
					return (string.IsNullOrWhiteSpace(subNameQuery))
						? subResults
						: subResults.FindAll(result =>
						{
							return this._context.API.FuzzySearch(subNameQuery, result.Title).Score > 0;
						});
				}
				case (Settings.ViewGroupingKeys.Entries):
				{
					if (runningTimeEntry is not null)
					{
						// Perform deep copy of summary so the cache is not mutated
						var serialisedSummary = JsonSerializer.Serialize<SummaryTimeEntry>(summary);
						summary = JsonSerializer.Deserialize<SummaryTimeEntry>(serialisedSummary);
						if ((summary is null) || (summary.groups is null))
						{
							return results;
						}

						var projectGroup = summary.groups.Find(group => group.id == runningTimeEntry.project_id);
						var entrySubGroup = projectGroup?.sub_groups?.Find(subGroup => subGroup.title == runningTimeEntry.description);

						if (entrySubGroup is not null)
						{
							entrySubGroup.seconds += (int)runningElapsed.TotalSeconds;
						}
						else if (projectGroup?.sub_groups is not null)
						{
							projectGroup.sub_groups.Add(new SummaryTimeEntrySubGroup
							{
								title = runningTimeEntry.description,
								seconds = (int)runningElapsed.TotalSeconds,
							});
						}
						else if (projectGroup is not null)
						{
							projectGroup.sub_groups = new List<SummaryTimeEntrySubGroup>
							{
								new SummaryTimeEntrySubGroup
								{
									title = runningTimeEntry.description,
									seconds = (int)runningElapsed.TotalSeconds,
								},
							};
						}
						else
						{
							summary.groups.Add(new SummaryTimeEntryGroup
							{
								id = runningTimeEntry.project_id,
								sub_groups = new List<SummaryTimeEntrySubGroup>
								{
									new SummaryTimeEntrySubGroup
									{
										title = runningTimeEntry.description,
										seconds = (int)runningElapsed.TotalSeconds,
									},
								},
							});
						}
					}

					summary.groups.ForEach(group =>
					{
						if (group.sub_groups is null)
						{
							return;
						}
						
						var project = me.projects?.Find(project => project.id == group.id);
						var client = me.clients?.Find(client => client.id == project?.client_id);

						string clientName = (client is not null)
							? $" • {client.name}"
							: string.Empty;
						string projectName = (project is not null)
							? $"{project.name}{clientName}"
							: "No Project";

						results.AddRange(
							group.sub_groups.ConvertAll(subGroup =>
							{
								var elapsed = TimeSpan.FromSeconds(subGroup.seconds);

								return new Result
								{
									Title = (string.IsNullOrEmpty(subGroup.title)) ? "(no description)" : subGroup.title,
									SubTitle = $"{projectName} | {elapsed.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Hour)} ({(int)elapsed.TotalHours}:{elapsed.ToString(@"mm\:ss")})",
									IcoPath = (project?.color is not null)
											? new ColourIcon(this._context, project.color, "view.png").GetColourIcon()
											: "view.png",
									AutoCompleteText = $"{query.ActionKeyword} {Settings.ViewCommand} {spanConfiguration.Argument} {groupingConfiguration.Argument} {((string.IsNullOrEmpty(subGroup.title)) ? "(no description)" : subGroup.title)}",
									Score = (int)elapsed.TotalSeconds,
									Action = c =>
									{
										this._selectedProjectId = project?.id;
										this._context.API.ChangeQuery($"{query.ActionKeyword} {Settings.StartCommand} {project?.name?.Kebaberize() ?? "no-project"} {subGroup.title}");
										return false;
									},
								};
							})
						);
					});

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