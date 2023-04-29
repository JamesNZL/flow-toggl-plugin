using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.TogglTrack.TogglApi;

namespace Flow.Launcher.Plugin.TogglTrack
{
	internal class TogglTrack
	{
		private PluginInitContext _context { get; set; }
		private Settings _settings { get; set; }

		private TogglClient _togglClient;
		private (bool IsValid, string Token) _lastToken = (false, string.Empty);
		private Me _me;

		private long? _selectedProjectId = -1;

		internal TogglTrack(PluginInitContext context, Settings settings)
		{
			this._context = context;
			this._settings = settings;

			this._togglClient = new TogglClient(this._settings.ApiToken);
		}

		internal async ValueTask<bool> VerifyApiToken()
		{
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
			// TODO: add refresh functionality
			this._me = await this._togglClient.GetMe();

			return this._lastToken.IsValid = this._me?.api_token?.Equals(this._settings.ApiToken) ?? false;
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
				}
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

		internal List<Result> GetDefaultHotKeys()
		{
			this._selectedProjectId = -1;

			return new List<Result>
			{
				new Result
				{
					Title = Settings.StartCommand,
					SubTitle = "Start a new time entry",
					IcoPath = "start.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} ",
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} ");
						return false;
					}
				},
				new Result
				{
					Title = Settings.StopCommand,
					SubTitle = "Stop current time entry",
					IcoPath = "stop.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} ",
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} ");
						return false;
					}
				},
				new Result
				{
					Title = Settings.ContinueCommand,
					SubTitle = "Continue previous time entry",
					IcoPath = "continue.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ContinueCommand} ",
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ContinueCommand} ");
						return false;
					}
				},
			};
		}

		internal List<Result> RequestStartEntry(CancellationToken token, Query query)
		{
			if (token.IsCancellationRequested)
			{
				this._selectedProjectId = -1;
				return new List<Result>();
			}

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
						Action = c =>
						{
							this._selectedProjectId = null;
							this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} no-project ", true);
							return false;
						},
					},
				};

				if (this._me?.projects is not null)
				{
					projects.AddRange(
						this._me.projects.ConvertAll(project => new Result
						{
							Title = project.name,
							SubTitle = (project?.client_id is not null) ? this._me?.clients?.Find(client => client.id == project.client_id)?.name : null,
							IcoPath = (project?.color is not null)
								? new ColourIcon(this._context, project.color).GetColourIcon()
								: "start.png",
							AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StartCommand} ",
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
					: projects.Where(hotkey =>
					{
						return this._context.API.FuzzySearch(query.SecondToEndSearch, hotkey.Title).Score > 0;
					}
					).ToList();
			}

			Project? project = this._me?.projects?.Find(project => project.id == this._selectedProjectId);
			Client? client = this._me?.clients?.Find(client => client.id == project?.client_id);
			long workspaceId = project?.workspace_id ?? this._me.default_workspace_id;

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
							this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {workspaceId}, {description}", "RequestStartEntry");

							// TODO: billable
							await this._togglClient.CreateTimeEntry(this._selectedProjectId, workspaceId, description, null, null);
							this._context.API.ShowMsg($"Started {description}", projectName, "start.png");
							
							this._selectedProjectId = -1;
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

			var runningTimeEntry = await this._togglClient.GetRunningTimeEntry();

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
			string elapsed = DateTimeOffset.UtcNow.Subtract(startDate).ToString(@"h\:mm\:ss");

			Project? project = this._me?.projects?.Find(project => project.id == runningTimeEntry.project_id);
			Client? client = this._me?.clients?.Find(client => client.id == project?.client_id);

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
					Title = $"Stop {runningTimeEntry.description}",
					SubTitle = $"{elapsed} | {projectName}",
					IcoPath = (project?.color is not null)
						? new ColourIcon(this._context, project.color).GetColourIcon()
						: "stop.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} {runningTimeEntry.description}",
					Action = c =>
					{
						Task.Run(async delegate
						{
							this._context.API.LogInfo("TogglTrack", $"{this._selectedProjectId}, {runningTimeEntry.id}, {runningTimeEntry.workspace_id}, {startDate}, {elapsed}", "RequestStopEntry");

							await this._togglClient.StopTimeEntry(runningTimeEntry.id, runningTimeEntry.workspace_id);
							this._context.API.ShowMsg($"Stopped {runningTimeEntry.description}", $"{elapsed} elapsed", "stop.png");
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

			var timeEntries = await this._togglClient.GetTimeEntries();

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
				string elapsed = TimeSpan.FromSeconds(timeEntry.duration).ToString(@"h\:mm\:ss");

				Project? project = this._me?.projects?.Find(project => project.id == timeEntry?.project_id);
				Client? client = this._me?.clients?.Find(client => client.id == project?.client_id);
				long workspaceId = project?.workspace_id ?? this._me.default_workspace_id;

				string clientName = (client is not null)
					? $" • {client.name}"
					: string.Empty;
				string projectName = (project is not null)
					? $"{project.name}{clientName}"
					: "No project";

				return new Result
				{
					Title = (string.IsNullOrEmpty(timeEntry?.description)) ? "No description" : timeEntry.description,
					SubTitle = $"{elapsed} | {projectName}",
					IcoPath = (project?.color is not null)
							? new ColourIcon(this._context, project.color).GetColourIcon()
							: "continue.png",
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ContinueCommand} {((string.IsNullOrEmpty(timeEntry?.description)) ? "No description" : timeEntry.description)}",
					Action = c =>
					{
						Task.Run(async delegate
						{
							this._context.API.LogInfo("TogglTrack", $"{project?.id}, {workspaceId}, {timeEntry?.description}", "RequestContinueEntry");

							// TODO: billable
							await this._togglClient.CreateTimeEntry(project?.id, workspaceId, timeEntry?.description, null, null);
							this._context.API.ShowMsg($"Continued {timeEntry?.description}", projectName, "continue.png");
						});

						return true;
					},
				};
			});

			return (string.IsNullOrWhiteSpace(query.SecondToEndSearch))
				? entries
				: entries.Where(hotkey =>
				{
					return this._context.API.FuzzySearch(query.SecondToEndSearch, hotkey.Title).Score > 0;
				}
				).ToList();
		}
	}
}