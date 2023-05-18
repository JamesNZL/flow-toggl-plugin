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
		internal const string ReportsCommand = "reports";
		internal const string BrowserCommand = "browser";
		internal const string RefreshCommand = "refresh";

		internal const string EditProjectFlag = "-p";
		internal const string TimeSpanFlag = "-t";

		internal enum ReportsSpanKeys
		{
			Day,
			Week,
			Month,
			Year,
		}
		internal static readonly List<ReportsSpanCommandArgument> ReportsSpanArguments = new List<ReportsSpanCommandArgument>
		{
			new ReportsSpanCommandArgument
			{
				Argument = "day",
				Interpolation = "today",
				Score = 400,
				// Today
				Start = now => now,
				End = now => now,
			},
			new ReportsSpanCommandArgument
			{
				Argument = "week",
				Interpolation = "this week",
				Score = 300,
				// Monday of the current week
				Start = now => now.AddDays(-(int)now.DayOfWeek + 1),
				// Sunday of the current week
				End = now => now.AddDays(-(int)now.DayOfWeek + 7),
			},
			new ReportsSpanCommandArgument
			{
				Argument = "month",
				Interpolation = "this month",
				Score = 200,
				// First day of the current month
				Start = now => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset),
				// Last day of the current month
				End = now => new DateTimeOffset(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 0, 0, 0, now.Offset),
			},
			new ReportsSpanCommandArgument
			{
				Argument = "year",
				Interpolation = "this year",
				Score = 100,
				// First day of the current year
				Start = now => new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, now.Offset),
				// Last day of the current year
				End = now => new DateTimeOffset(now.Year, 12, 31, 0, 0, 0, now.Offset),
			},
		};

		public enum ReportsGroupingKeys
		{
			Projects,
			Clients,
			Entries,
		}
		private const string ReportsGroupingProjectsArgument = "projects";
		private const string ReportsGroupingClientsArgument = "clients";
		private const string ReportsGroupingEntriesArgument = "entries";
		internal static readonly List<ReportsGroupingCommandArgument> ReportsGroupingArguments = new List<ReportsGroupingCommandArgument>
		{
			new ReportsGroupingCommandArgument
			{
				Argument = Settings.ReportsGroupingProjectsArgument,
				Interpolation = "View tracked time grouped by project",
				Score = 300,
				Grouping = Settings.ReportsGroupingKeys.Projects,
				SubArgument = null,
			},
			new ReportsGroupingCommandArgument
			{
				Argument = Settings.ReportsGroupingClientsArgument,
				Interpolation = "View tracked time grouped by client",
				Score = 200,
				Grouping = Settings.ReportsGroupingKeys.Clients,
				SubArgument = Settings.ReportsGroupingProjectsArgument,
			},
			new ReportsGroupingCommandArgument
			{
				Argument = Settings.ReportsGroupingEntriesArgument,
				Interpolation = "View tracked time entries",
				Score = 100,
				Grouping = Settings.ReportsGroupingKeys.Entries,
				SubArgument = null,
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

	public class ReportsSpanCommandArgument : CommandArgument
	{
		#nullable disable
		public Func<DateTimeOffset, DateTimeOffset> Start { get; init; }
		public Func<DateTimeOffset, DateTimeOffset> End { get; init; }
		#nullable enable
	}

	public class ReportsGroupingCommandArgument : CommandArgument
	{
		#nullable disable
		public Settings.ReportsGroupingKeys Grouping { get; init; }
		public string SubArgument { get; init; }
		#nullable enable
	}
}