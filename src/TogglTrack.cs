using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using Flow.Launcher.Plugin.TogglTrack.TogglApi;

namespace Flow.Launcher.Plugin.TogglTrack
{
	internal class TogglTrack
	{
		private PluginInitContext _context { get; set; }
		private Settings _settings { get; set; }

		private TogglClient _togglClient;

		private (bool IsValid, string Token) _lastToken = (false, string.Empty);
		private (Me? me, DateTime LastFetched) _lastMe = (null, DateTime.MinValue);
		private (TimeEntry? timeEntry, DateTime LastFetched) _lastCurrentlyRunning = (null, DateTime.MinValue);
		private (List<TimeEntry>? timeEntries, DateTime LastFetched) _lastTimeEntries = (null, DateTime.MinValue);
		private long? _selectedProjectId = -1;

		internal TogglTrack(PluginInitContext context, Settings settings)
		{
			this._context = context;
			this._settings = settings;

			this._togglClient = new TogglClient(this._settings.ApiToken);
		}

		private async ValueTask<Me?> _GetMe(bool force = false)
		{
			if (!force && ((DateTime.Now - this._lastMe.LastFetched).TotalDays < 3))
			{
				return this._lastMe.me;
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching me", "_GetMe");
				
				this._lastMe.LastFetched = DateTime.Now;
				return this._lastMe.me = await this._togglClient.GetMe();
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch me", exception, "_GetMe");
				return null;
			}
		}

		private async ValueTask<TimeEntry?> _GetRunningTimeEntry(bool force = false)
		{
			if (!force && ((DateTime.Now - this._lastCurrentlyRunning.LastFetched).TotalSeconds < 30))
			{
				return this._lastCurrentlyRunning.timeEntry;
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching running time entry", "_GetRunningTimeEntry");
				
				this._lastCurrentlyRunning.LastFetched = DateTime.Now;
				return this._lastCurrentlyRunning.timeEntry = await this._togglClient.GetRunningTimeEntry();
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch running time entry", exception, "_GetRunningTimeEntry");
				return null;
			}
		}

		private async ValueTask<List<TimeEntry>?> _GetTimeEntries(bool force = false)
		{
			if (!force && ((DateTime.Now - this._lastTimeEntries.LastFetched).TotalSeconds < 30))
			{
				return this._lastTimeEntries.timeEntries;
			}

			try
			{
				this._context.API.LogInfo("TogglTrack", "Fetching time entries", "_GetTimeEntries");
				
				this._lastTimeEntries.LastFetched = DateTime.Now;
				return this._lastTimeEntries.timeEntries = await this._togglClient.GetTimeEntries();
			}
			catch (Exception exception)
			{
				this._context.API.LogException("TogglTrack", "Failed to fetch time entries", exception, "_GetTimeEntries");
				return null;
			}
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

			this._togglClient.UpdateToken(this._settings.ApiToken);

			return this._lastToken.IsValid = (await this._GetMe(true))?.api_token?.Equals(this._settings.ApiToken) ?? false;
		}

		internal List<Result> NotifyMissingToken()
		{
			return new List<Result>
			{
				new Result
				{
					Title = "ERROR: Missing API Token",
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
					Title = "ERROR: Invalid API Token",
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

		internal async ValueTask<List<Result>> GetDefaultHotKeys()
		{
			this._selectedProjectId = -1;

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
					Title = Settings.RefreshCommand,
					SubTitle = "Refresh plugin cache",
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.RefreshCommand} ",
					Score = -100,
					Action = c =>
					{
						_ = Task.Run(() =>
						{
							_ = this._GetMe(true);
							_ = this._GetRunningTimeEntry(true);
							_ = this._GetTimeEntries(true);
						});

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

			if (this._selectedProjectId == -1 || query.SearchTerms.Length == 1)
			{
				this._selectedProjectId = -1;

				var projects = new List<Result>
				{
					new Result
					{
						Title = "No project",
						IcoPath = "start.png",
						AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} ",
						// Ensure is 1 greater than the top-priority project
						Score = (me?.projects?.Count ?? 0) + 1,
						Action = c =>
						{
							this._selectedProjectId = null;
							this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} no-project ", true);
							return false;
						},
					},
				};

				if (me?.projects is not null)
				{
					var filteredProjects = me.projects.FindAll(project => project.active);
					filteredProjects.Sort((projectOne, projectTwo) => (projectTwo.actual_hours ?? 0) - (projectOne.actual_hours ?? 0));

					projects.AddRange(
						filteredProjects.ConvertAll(project => new Result
						{
							Title = project.name,
							SubTitle = $"{((project?.client_id is not null) ? $"{me?.clients?.Find(client => client.id == project.client_id)?.name} | " : string.Empty)}{project?.actual_hours ?? 0} hours",
							IcoPath = (project?.color is not null)
								? new ColourIcon(this._context, project.color).GetColourIcon()
								: "start.png",
							AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} ",
							Score = filteredProjects.Count - filteredProjects.IndexOf(project),
							Action = c =>
							{
								this._selectedProjectId = project.id;
								this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} {project.name.ToLower().Replace(" ", "-")} ", true);
								return false;
							},
						})
					);
				}

				return (string.IsNullOrWhiteSpace(query.SecondToEndSearch))
					? projects
					: projects.FindAll(result =>
					{
						return this._context.API.FuzzySearch(query.SecondToEndSearch, $"{result.Title} {result.SubTitle}").Score > 0;
					});
			}

			Project? project = me?.projects?.Find(project => project.id == this._selectedProjectId);
			Client? client = me?.clients?.Find(client => client.id == project?.client_id);
			long workspaceId = project?.workspace_id ?? me.default_workspace_id;

			string clientName = (client is not null)
				? $" • {client.name}"
				: string.Empty;
			string projectName = (project is not null)
				? $"{project.name}{clientName}"
				: "No project";

			string description = string.Join(" ", query.SearchTerms.Skip(2));

			return new List<Result>
			{
				new Result
				{
					Title = $"Start {description}",
					SubTitle = projectName,
					IcoPath = (project?.color is not null)
						? new ColourIcon(this._context, project.color).GetColourIcon()
						: "start.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {workspaceId}, {description}", "RequestStartEntry");
								
								// TODO: billable
								var createdTimeEntry = await this._togglClient.CreateTimeEntry(this._selectedProjectId, workspaceId, description, null, null);
								if (createdTimeEntry?.id is null)
								{
									throw new Exception();
								}

								this._context.API.ShowMsg($"Started {createdTimeEntry?.description}", projectName, "start.png");

								// Update cached running time entry state
								_ = Task.Run(() =>
								{
									_ = this._GetRunningTimeEntry(true);
									_ = this._GetTimeEntries(true);
								});
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to continue time entry", exception, "RequestStartEntry");
								this._context.API.ShowMsgError("Failed to continue time entry.", exception.Message);
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
		}

		internal async ValueTask<List<Result>> RequestEditEntry(CancellationToken token, Query query)
		{
			if (token.IsCancellationRequested)
			{
				return new List<Result>();
			}

			var me = await this._GetMe();
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

			DateTimeOffset startDate = DateTimeOffset.Parse(runningTimeEntry.start);
			TimeSpan elapsed = DateTimeOffset.UtcNow.Subtract(startDate);

			Project? project = me?.projects?.Find(project => project.id == runningTimeEntry.project_id);
			Client? client = me?.clients?.Find(client => client.id == project?.client_id);

			string clientName = (client is not null)
				? $" • {client.name}"
				: string.Empty;
			string projectName = (project is not null)
				? $"{project.name}{clientName}"
				: "No project";

			return new List<Result>
			{
				new Result
				{
					Title = (string.IsNullOrEmpty(query.SecondToEndSearch)) ? ((string.IsNullOrEmpty(runningTimeEntry?.description)) ? "(no description)" : runningTimeEntry.description) : query.SecondToEndSearch,
					SubTitle = $"{projectName} | {elapsed.Humanize()} ({elapsed.ToString(@"h\:mm\:ss")})",
					IcoPath = (project?.color is not null)
						? new ColourIcon(this._context, project.color).GetColourIcon()
						: "edit.png",
					AutoCompleteText = $"{query.ActionKeyword} {query.Search}",
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.id}, {runningTimeEntry.duration}, {runningTimeEntry.start}, {runningTimeEntry.project_id}, {runningTimeEntry.workspace_id}, {query.SecondToEndSearch}", "RequestEditEntry");
								
								var editedTimeEntry = await this._togglClient.EditTimeEntry(runningTimeEntry, query.SecondToEndSearch);
								if (editedTimeEntry?.id is null)
								{
									throw new Exception();
								}

								this._context.API.ShowMsg($"Edited {editedTimeEntry?.description}", $"{projectName} | {elapsed.ToString(@"h\:mm\:ss")}", "edit.png");

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
						});

						return true;
					},
				},
			};
		}

		internal async ValueTask<List<Result>> RequestStopEntry(CancellationToken token)
		{
			if (token.IsCancellationRequested)
			{
				return new List<Result>();
			}

			var me = await this._GetMe();
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

			DateTimeOffset startDate = DateTimeOffset.Parse(runningTimeEntry.start);
			TimeSpan elapsed = DateTimeOffset.UtcNow.Subtract(startDate);

			Project? project = me?.projects?.Find(project => project.id == runningTimeEntry.project_id);
			Client? client = me?.clients?.Find(client => client.id == project?.client_id);

			string clientName = (client is not null)
				? $" • {client.name}"
				: string.Empty;
			string projectName = (project is not null)
				? $"{project.name}{clientName}"
				: "No project";

			return new List<Result>
			{
				new Result
				{
					Title = $"Stop {((string.IsNullOrEmpty(runningTimeEntry?.description)) ? "(no description)" : runningTimeEntry.description)}",
					SubTitle = $"{projectName} | {elapsed.Humanize()} ({elapsed.ToString(@"h\:mm\:ss")})",
					IcoPath = (project?.color is not null)
						? new ColourIcon(this._context, project.color).GetColourIcon()
						: "stop.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} {((string.IsNullOrEmpty(runningTimeEntry?.description)) ? "(no description)" : runningTimeEntry.description)}",
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.id}, {runningTimeEntry.workspace_id}, {startDate}, {elapsed.ToString(@"h\:mm\:ss")}", "RequestStopEntry");
								
								// TODO: billable
								var stoppedTimeEntry = await this._togglClient.StopTimeEntry(runningTimeEntry.id, runningTimeEntry.workspace_id);
								if (stoppedTimeEntry?.id is null)
								{
									throw new Exception();
								}

								this._context.API.ShowMsg($"Stopped {stoppedTimeEntry?.description}", $"{elapsed.ToString(@"h\:mm\:ss")} elapsed", "stop.png");

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
		}

		internal async ValueTask<List<Result>> RequestContinueEntry(CancellationToken token, Query query)
		{
			if (token.IsCancellationRequested)
			{
				return new List<Result>();
			}

			var me = await this._GetMe();
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
					? DateTimeOffset.UtcNow.Subtract(DateTimeOffset.Parse(timeEntry.start))
					: TimeSpan.FromSeconds(timeEntry.duration);

				Project? project = me?.projects?.Find(project => project.id == timeEntry?.project_id);
				Client? client = me?.clients?.Find(client => client.id == project?.client_id);
				long workspaceId = project?.workspace_id ?? me.default_workspace_id;

				string clientName = (client is not null)
					? $" • {client.name}"
					: string.Empty;
				string projectName = (project is not null)
					? $"{project.name}{clientName} | "
					: string.Empty;

				return new Result
				{
					Title = (string.IsNullOrEmpty(timeEntry?.description)) ? "(no description)" : timeEntry.description,
					SubTitle = $"{projectName}{elapsed.Humanize()} ({DateTime.Parse(timeEntry.start).Humanize(false)})",
					IcoPath = (project?.color is not null)
							? new ColourIcon(this._context, project.color).GetColourIcon()
							: "continue.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ContinueCommand} {((string.IsNullOrEmpty(timeEntry?.description)) ? "(no description)" : timeEntry.description)}",
					Score = timeEntries.Count - timeEntries.IndexOf(timeEntry),
					Action = c =>
					{
						Task.Run(async delegate
						{
							try
							{
								this._context.API.LogInfo("TogglTrack", $"{project?.id}, {workspaceId}, {timeEntry?.description}", "RequestContinueEntry");
								
								// TODO: billable
								var createdTimeEntry = await this._togglClient.CreateTimeEntry(project?.id, workspaceId, timeEntry?.description, null, null);
								if (createdTimeEntry?.id is null)
								{
									throw new Exception();
								}

								this._context.API.ShowMsg($"Continued {createdTimeEntry?.description}", projectName, "continue.png");

								// Update cached running time entry state
								_ = Task.Run(() =>
								{
									_ = this._GetRunningTimeEntry(true);
									_ = this._GetTimeEntries(true);
								});
							}
							catch (Exception exception)
							{
								this._context.API.LogException("TogglTrack", "Failed to continue time entry", exception, "RequestContinueEntry");
								this._context.API.ShowMsgError("Failed to continue time entry.", exception.Message);
							}
						});

						return true;
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