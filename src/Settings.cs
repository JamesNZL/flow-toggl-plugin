using System.Collections.Generic;

namespace Flow.Launcher.Plugin.TogglTrack
{
	/// <Summary>
	/// Flow Launcher Toggl Track plugin settings.
	/// </Summary>
	public class Settings
	{
		internal const string StartCommand = "start";
		internal const string EditCommand = "edit";
		internal const string StopCommand = "stop";
		internal const string DeleteCommand = "delete";
		internal const string ContinueCommand = "continue";
		internal const string ViewCommand = "view";
		internal const string BrowserCommand = "browser";
		internal const string RefreshCommand = "refresh";

		internal const string EditProjectFlag = "-p";
		internal const string TimeSpanFlag = "-t";

		internal enum ViewSpanKeys
		{
			Day,
			Week,
			Month,
			Year,
		}
		internal static readonly Dictionary<Settings.ViewSpanKeys, CommandArgument> ViewSpanArguments = new Dictionary<Settings.ViewSpanKeys, CommandArgument>
		{
			{
				Settings.ViewSpanKeys.Day,
				new CommandArgument
				{
					Argument = "day",
					Interpolation = "today",
					Score = 400
				}
			},
			{
				Settings.ViewSpanKeys.Week,
				new CommandArgument
				{
					Argument = "week",
					Interpolation = "this week",
					Score = 300
				}
			},
			{
				Settings.ViewSpanKeys.Month,
				new CommandArgument
				{
					Argument = "month",
					Interpolation = "this month",
					Score = 200
				}
			},
			{
				Settings.ViewSpanKeys.Year,
				new CommandArgument
				{
					Argument = "year",
					Interpolation = "this year",
					Score = 100
				}
			},
		};

		internal enum ViewGroupingKeys
		{
			Entries,
			Projects,
			Clients,
		}
		internal static readonly Dictionary<Settings.ViewGroupingKeys, CommandArgument> ViewGroupingArguments = new Dictionary<Settings.ViewGroupingKeys, CommandArgument>
		{
			{
				Settings.ViewGroupingKeys.Entries,
				new CommandArgument
				{
					Argument = "entries",
					Interpolation = "View tracked time entries",
					Score = 300
				}
			},
			{
				Settings.ViewGroupingKeys.Projects,
				new CommandArgument
				{
					Argument = "projects",
					Interpolation = "View tracked time grouped by project",
					Score = 200
				}
			},
			{
				Settings.ViewGroupingKeys.Clients,
				new CommandArgument
				{
					Argument = "clients",
					Interpolation = "View tracked time grouped by client",
					Score = 100
				}
			},
		};

		/// <Summary>
		/// Toggl Track API Token.
		/// </Summary>
		public string ApiToken { get; set; } = string.Empty;
	}

	public class CommandArgument
	{
		#nullable disable
		public string Argument { get; set; }
		public string Interpolation { get; set; }
		public int Score { get; set; }
		#nullable enable
	}
}