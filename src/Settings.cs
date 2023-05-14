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

		internal static readonly List<ViewDuration> ViewDurationArguments = new List<ViewDuration>
		{
			new ViewDuration
			(
				"day",
				"today's"
			),
			new ViewDuration
			(
				"week",
				"this week's"
			),
			new ViewDuration
			(
				"month",
				"this month's"
			),
			new ViewDuration
			(
				"year",
				"this year's"
			),
		};

		/// <Summary>
		/// Toggl Track API Token.
		/// </Summary>
		public string ApiToken { get; set; } = string.Empty;
	}

	public class ViewDuration
	{
		public string argument;
		public string spanString;

		public ViewDuration(string argument, string spanString)
		{
			this.argument = argument;
			this.spanString = spanString;
		}
	}
}