using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.TogglTrack
{
	/// <Summary>
	/// Flow Launcher Toggl Track plugin settings.
	/// </Summary>
	public class Settings
	{
		internal const string StartCommand = "start";
		internal const string StopCommand = "stop";
		internal const string ContinueCommand = "continue";
		internal const string EditCommand = "edit";
		internal const string DeleteCommand = "delete";
		internal const string ReportsCommand = "reports";
		internal const string BrowserCommand = "browser";
		internal const string RefreshCommand = "refresh";
		internal const string HelpCommand = "help";
		internal static readonly string[] Commands = new string[] {
			StartCommand,
			StopCommand,
			ContinueCommand,
			EditCommand,
			DeleteCommand,
			ReportsCommand,
			BrowserCommand,
			RefreshCommand,
			HelpCommand,
		};

		internal const string ProjectPrefix = "@";
		internal const string EscapeCharacter = @"\";

		internal const string EditProjectFlag = "-p";
		internal const string ClearDescriptionFlag = "-C";
		internal const string TimeSpanFlag = "-t";
		internal const string TimeSpanEndFlag = "-T";
		internal const string ShowStopFlag = "-S";

		internal const string NoProjectName = "No Project";
		internal const string NoClientName = "No Client";
		internal const string EmptyDescription = "(no description)";
		internal const string EmptyTimeEntry = "an empty time entry";

		internal const string UsageTipTitle = "Usage Tip";
		internal const string UsageExampleTitle = "Usage Example";
		internal const string UsageWarningTitle = "Usage Warning";

		internal static readonly Regex QueryEscapingRegex = new Regex(@$"(\{Settings.EscapeCharacter}(?!\{Settings.EscapeCharacter}))");
		internal static readonly Regex UnescapedProjectPrefixRegex = new Regex(@$"(?<!\{Settings.EscapeCharacter}){Settings.ProjectPrefix}");
		internal static readonly Regex UnescapedFlagRegex = new Regex(@" -");
		internal static readonly Regex ReportsSpanOffsetRegex = new Regex(@"-(\d+)");

		internal enum ReportsSpanKey
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
				Interpolation = offset =>
				{
					switch (offset)
					{
						case (0):
						{
							return "today";
						}
						case (1):
						{
							return "yesterday";
						}
						default:
						{
							return $"{offset} days ago";
						}
					}
				},
				Score = 400,
				// Offsetted day
				Start = (referenceDate, _, offset) => referenceDate.AddDays(-offset),
				End = (referenceDate, _, offset) => referenceDate.AddDays(-offset),
			},
			new ReportsSpanCommandArgument
			{
				Argument = "week",
				Interpolation = offset =>
				{
					switch (offset)
					{
						case (0):
						{
							return "this week";
						}
						case (1):
						{
							return "last week";
						}
						default:
						{
							return $"{offset} weeks ago";
						}
					}
				},
				Score = 300,
				// Start of the offsetted week
				Start = (referenceDate, beginningOfWeek, offset) => referenceDate.AddDays(-((7 + ((int)referenceDate.DayOfWeek - (int)beginningOfWeek)) % 7) - (offset * 7)),
				// End of the offsetted week
				End = (referenceDate, beginningOfWeek, offset) => referenceDate.AddDays(((6 + ((int)beginningOfWeek - (int)referenceDate.DayOfWeek)) % 7) - (offset * 7))
			},
			new ReportsSpanCommandArgument
			{
				Argument = "month",
				Interpolation = offset =>
				{
					switch (offset)
					{
						case (0):
						{
							return "this month";
						}
						case (1):
						{
							return "last month";
						}
						default:
						{
							return $"{offset} months ago";
						}
					}
				},
				Score = 200,
				// First day of the offsetted month
				Start = (referenceDate, _, offset) => new DateTimeOffset(referenceDate.Year, referenceDate.Month, 1, 0, 0, 0, referenceDate.Offset).AddMonths(-offset),
				// Last day of the offsetted month
				End = (referenceDate, _, offset) => new DateTimeOffset(referenceDate.Year, referenceDate.Month, DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month), 0, 0, 0, referenceDate.Offset).AddMonths(-offset),
			},
			new ReportsSpanCommandArgument
			{
				Argument = "year",
				Interpolation = offset =>
				{
					switch (offset)
					{
						case (0):
						{
							return "this year";
						}
						case (1):
						{
							return "last year";
						}
						default:
						{
							return $"{offset} years ago";
						}
					}
				},
				Score = 100,
				// First day of the offsetted year
				Start = (referenceDate, _, offset) => new DateTimeOffset(referenceDate.Year - offset, 1, 1, 0, 0, 0, referenceDate.Offset),
				// Last day of the offsetted year
				End = (referenceDate, _, offset) => new DateTimeOffset(referenceDate.Year - offset, 12, 31, 0, 0, 0, referenceDate.Offset),
			},
		};

		public enum ReportsGroupingKey
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
				Grouping = Settings.ReportsGroupingKey.Projects,
				SubArgument = null,
			},
			new ReportsGroupingCommandArgument
			{
				Argument = Settings.ReportsGroupingClientsArgument,
				Interpolation = "View tracked time grouped by client",
				Score = 200,
				Grouping = Settings.ReportsGroupingKey.Clients,
				SubArgument = Settings.ReportsGroupingProjectsArgument,
			},
			new ReportsGroupingCommandArgument
			{
				Argument = Settings.ReportsGroupingEntriesArgument,
				Interpolation = "View tracked time entries",
				Score = 100,
				Grouping = Settings.ReportsGroupingKey.Entries,
				SubArgument = null,
			},
		};

		/// <Summary>
		/// Toggl Track API Token.
		/// </Summary>
		public string ApiToken { get; set; } = string.Empty;

		public bool ShowUsageTips { get; set; } = true;
		public bool ShowUsageExamples { get; set; } = true;
		public bool ShowUsageWarnings { get; set; } = true;

		public bool AllowSuccessNotifications { get; set; } = true;
		public bool AllowErrorNotifications { get; set; } = true;
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
		public new Func<int, string> Interpolation { get; init; }
		public Func<DateTimeOffset, DayOfWeek, int, DateTimeOffset> Start { get; init; }
		public Func<DateTimeOffset, DayOfWeek, int, DateTimeOffset> End { get; init; }
#nullable enable
	}

	public class ReportsGroupingCommandArgument : CommandArgument
	{
#nullable disable
		public Settings.ReportsGroupingKey Grouping { get; init; }
		public string SubArgument { get; init; }
#nullable enable
	}
}