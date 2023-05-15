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

		internal static readonly List<CommandArgument> ViewDurationArguments = new List<CommandArgument>
		{
			new CommandArgument
			{
				Argument = "day",
				Interpolation = "today",
			},
			new CommandArgument
			{
				Argument = "week",
				Interpolation = "this week",
			},
			new CommandArgument
			{
				Argument = "month",
				Interpolation = "this month",
			},
			new CommandArgument
			{
				Argument = "year",
				Interpolation = "this year",
			},
		};
		internal static readonly List<CommandArgument> ViewGroupingArguments = new List<CommandArgument>
		{
			new CommandArgument
			{
				Argument = "entries",
				Interpolation = "View tracked time entries",
			},
			new CommandArgument
			{
				Argument = "projects",
				Interpolation = "View tracked time grouped by project",
			},
			new CommandArgument
			{
				Argument = "clients",
				Interpolation = "View tracked time grouped by client",
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
		#nullable enable
	}
}