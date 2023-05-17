using System;
using System.Text.RegularExpressions;
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

		private long? _selectedProjectId = -1;
		
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

		internal void RefreshCache()
		{
			_ = Task.Run(() =>
			{
				// This is the main one that needs to be run
				_ = this._GetMe(true);
				_ = this._GetRunningTimeEntry(true);
				_ = this._GetTimeEntries(true);
			});
		}

		internal async ValueTask<bool> VerifyApiToken()
		{
			if (!InternetAvailability.IsInternetAvailable())
			{
				return false;
			}

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

			return this._lastToken.IsValid = (await this._GetMe(true))?.api_token?.Equals(this._settings.ApiToken) ?? false;
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
						this.RefreshCache();
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

			if (this._selectedProjectId == -1 || query.SearchTerms.Length == 1)
			{
				this._selectedProjectId = -1;

				// Start fetch for time entries asynchronously in the backgroundd
				_ = Task.Run(() =>
				{
					_ = this._GetTimeEntries(true);
				});

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

				return (string.IsNullOrWhiteSpace(query.SecondToEndSearch))
					? projects
					: projects.FindAll(result =>
					{
						return this._context.API.FuzzySearch(query.SecondToEndSearch, $"{result.Title} {Regex.Replace(result.SubTitle, @"(?: \| )?\d+ hours?$", string.Empty)}").Score > 0;
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

			string description = string.Join(" ", query.SearchTerms.Skip(2));

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
								_ = Task.Run(() =>
								{
									_ = this._GetRunningTimeEntry(true);
									_ = this._GetTimeEntries(true);
								});
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
						string.Join(" ", query.SearchTerms.Skip(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag) + 1)),
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
					string sanitisedDescription = string.Join(" ", query.SearchTerms.Take(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag)).Skip(2));

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
									_ = Task.Run(() =>
									{
										_ = this._GetRunningTimeEntry(true);
										_ = this._GetTimeEntries(true);
									});
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
							_ = Task.Run(() =>
							{
								_ = this._GetRunningTimeEntry(true);
								_ = this._GetTimeEntries(true);
							});
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

			// Reset project selection if query emptied to 'tgl edit '
			if (query.SearchTerms.Length == 1 && this._editProjectState == TogglTrack.EditProjectState.ProjectSelected)
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

				return (string.IsNullOrWhiteSpace(query.SecondToEndSearch))
					? projects
					: projects.FindAll(result =>
					{
						return this._context.API.FuzzySearch(query.SecondToEndSearch, $"{result.Title} {Regex.Replace(result.SubTitle, @"(?: \| )?\d+ hours?$", string.Empty)}").Score > 0;
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

			string description = (this._editProjectState == TogglTrack.EditProjectState.ProjectSelected)
				? string.Join(" ", query.SearchTerms.Skip(2))
				: query.SecondToEndSearch;

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
								_ = Task.Run(() =>
								{
									_ = this._GetRunningTimeEntry(true);
									_ = this._GetTimeEntries(true);
								});
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
						string.Join(" ", query.SearchTerms.Skip(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag) + 1)),
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
					string sanitisedDescription = string.Join(" ", query.SearchTerms.Take(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag)).Skip(
						(this._editProjectState == TogglTrack.EditProjectState.ProjectSelected)
							? 2
							: 1
					));

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
									_ = Task.Run(() =>
									{
										_ = this._GetRunningTimeEntry(true);
										_ = this._GetTimeEntries(true);
									});
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
								_ = Task.Run(() =>
								{
									_ = this._GetRunningTimeEntry(true);
									_ = this._GetTimeEntries(true);
								});
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
					string.Join(" ", query.SearchTerms.Skip(Array.IndexOf(query.SearchTerms, Settings.TimeSpanFlag) + 1)),
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
								_ = Task.Run(() =>
								{
									_ = this._GetRunningTimeEntry(true);
									_ = this._GetTimeEntries(true);
								});
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
								_ = Task.Run(() =>
								{
									_ = this._GetRunningTimeEntry(true);
									_ = this._GetTimeEntries(true);
								});
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

			return (string.IsNullOrWhiteSpace(query.SecondToEndSearch))
				? entries
				: entries.FindAll(result =>
				{
					return this._context.API.FuzzySearch(query.SecondToEndSearch, result.Title).Score > 0;
				});
		}
	}
}