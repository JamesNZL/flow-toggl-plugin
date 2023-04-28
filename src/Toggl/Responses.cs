using System.Collections.Generic;

namespace Flow.Launcher.Plugin.TogglTrack.TogglApi
{
	public interface IMe
	{
		public int default_workspace_id { get; set; }
	}

	public interface IWorkspace
	{
		public int id { get; set; }
		public string name { get; set; }
	}

	public interface IProject
	{
		public bool billable { get; set; }
		public int client_id { get; set; }
		public string color { get; set; }
		public int id { get; set; }
		public string name { get; set; }
		public int workspace_id { get; set; }
	}

	public interface ITimeEntry
	{
		public string at { get; set; }
		public bool billable { get; set; }
		public string description { get; set; }
		public int id { get; set; }
		public int project_id { get; set; }
		public string start { get; set; }
		public int duration { get; set; }
		public List<string> tags { get; set; }
		public int workspace_id { get; set; }
	}

	public interface IClient
	{
		public int id { get; set; }
		public string name { get; set; }
	}

	public interface ITag
	{
		public int id { get; set; }
		public string name { get; set; }
		public int workspace_id { get; set; }
	}
}