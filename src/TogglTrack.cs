using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.TogglTrack.TogglApi;

namespace Flow.Launcher.Plugin.TogglTrack
{
	internal class TogglTrack
	{
		private PluginInitContext _context { get; set; }
		private Settings _settings { get; set; }

		private (bool IsValid, string Token) _lastToken = (false, "");
		private TogglClient _togglClient;

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
			return this._lastToken.IsValid = (await this._togglClient.GetMe())?.api_token?.Equals(this._settings.ApiToken) ?? false;
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
			return new List<Result>
			{
				new Result
				{
					Title = Settings.StartCommand,
					SubTitle = "Start a new time entry",
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
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
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
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
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ContinueCommand} ",
					Action = c =>
					{
						this._context.API.ChangeQuery($"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.ContinueCommand} ");
						return false;
					}
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
					}
				},
			};
			}

			DateTimeOffset startDate = DateTimeOffset.Parse(runningTimeEntry.start);
			string elapsed = DateTimeOffset.UtcNow.Subtract(startDate).ToString(@"h\:mm\:ss");

			return new List<Result>
			{
				new Result
				{
					Title = $"Stop {runningTimeEntry.description}",
					SubTitle = elapsed,
					IcoPath = this._context.CurrentPluginMetadata.IcoPath,
					AutoCompleteText = $"{this._context.CurrentPluginMetadata.ActionKeyword} {Settings.StopCommand} {runningTimeEntry.description}",
					Action = c =>
					{
						this._context.API.ShowMsg($"Stopped {runningTimeEntry.description}", $"{elapsed} elapsed", this._context.CurrentPluginMetadata.IcoPath);
						return true;
					}
				},
			};
		}
	}
}