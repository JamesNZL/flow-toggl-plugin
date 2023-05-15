using System;
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
		internal static readonly Dictionary<Settings.ViewSpanKeys, ViewSpanCommandArgument> ViewSpanArguments = new Dictionary<Settings.ViewSpanKeys, ViewSpanCommandArgument>
		{
			{
				Settings.ViewSpanKeys.Day,
				new ViewSpanCommandArgument
				{
					Argument = "day",
					Interpolation = "today",
					Score = 400,
					// Today
					Start = now => now,
					End = now => now
				}
			},
			{
				Settings.ViewSpanKeys.Week,
				new ViewSpanCommandArgument
				{
					Argument = "week",
					Interpolation = "this week",
					Score = 300,
					// Monday of the current week
					Start = now => now.AddDays(-(int)now.DayOfWeek + 1),
					// Sunday of the current week
					End = now => now.AddDays(-(int)now.DayOfWeek + 7)
				}
			},
			{
				Settings.ViewSpanKeys.Month,
				new ViewSpanCommandArgument
				{
					Argument = "month",
					Interpolation = "this month",
					Score = 200,
					// First day of the current month
					Start = now => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset),
					// Last day of the current month
					End = now => new DateTimeOffset(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 0, 0, 0, now.Offset)
				}
			},
			{
				Settings.ViewSpanKeys.Year,
				new ViewSpanCommandArgument
				{
					Argument = "year",
					Interpolation = "this year",
					Score = 100,
					// First day of the current year
					Start = now => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset),
					// Last day of the current year
					End = now => new DateTimeOffset(now.Year, 12, 31, 0, 0, 0, now.Offset)
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
		public string Argument { get; init; }
		public string Interpolation { get; init; }
		public int Score { get; init; }
		#nullable enable
	}

	public class ViewSpanCommandArgument : CommandArgument
	{
		#nullable disable
		public Func<DateTimeOffset, DateTimeOffset> Start { get; init; }
		public Func<DateTimeOffset, DateTimeOffset> End { get; init; }
		#nullable enable
	}
}