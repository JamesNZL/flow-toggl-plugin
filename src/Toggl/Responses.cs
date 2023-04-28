using System.Collections.Generic;

namespace Flow.Launcher.Plugin.TogglTrack.TogglApi
{
	public class Me
	{
		public long default_workspace_id { get; set; }
	}

	public class Workspace
	{
		public long id { get; set; }
		public string name { get; set; }
	}

	public class Project
	{
		public bool billable { get; set; }
		public long client_id { get; set; }
		public string color { get; set; }
		public long id { get; set; }
		public string name { get; set; }
		public long workspace_id { get; set; }
	}

	public class TimeEntry
	{
		public string at { get; set; }
		public bool billable { get; set; }
		public string description { get; set; }
		public long id { get; set; }
		public long project_id { get; set; }
		public string start { get; set; }
		public long duration { get; set; }
		public List<string> tags { get; set; }
		public long workspace_id { get; set; }
	}

	public class Client
	{
		public long id { get; set; }
		public string name { get; set; }
	}

	public class Tag
	{
		public long id { get; set; }
		public string name { get; set; }
		public long workspace_id { get; set; }
	}
}