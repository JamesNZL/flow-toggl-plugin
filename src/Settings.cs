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
				Interpolation = "today's",
			},
			new CommandArgument
			{
				Argument = "week",
				Interpolation = "this week's",
			},
			new CommandArgument
			{
				Argument = "month",
				Interpolation = "this month's",
			},
			new CommandArgument
			{
				Argument = "year",
				Interpolation = "this year's",
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