using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.TogglTrack.TogglApi
{
	public class TogglClient
	{
		private readonly static string _baseUrl = "https://api.track.toggl.com/api/v9/";
		private readonly AuthenticatedFetch _api;

		public TogglClient(string token)
		{
			this._api = new AuthenticatedFetch(token, TogglClient._baseUrl);
		}

		public async Task<IMe> GetMe()
		{
			return await this._api.Get<IMe>("me");
		}

		public async Task<List<IWorkspace>> GetWorkspaces()
		{
			return await this._api.Get<List<IWorkspace>>("workspaces");
		}

		public async Task<List<IProject>> GetWorkspaceProjects(int workspaceId)
		{
			return await this._api.Get<List<IProject>>($"workspaces/{workspaceId}/projects?per_page=500") ?? new List<IProject>();
		}

		public async Task<ITimeEntry> CreateTimeEntry(int? projectId, int workspaceId, string description, List<string> tags, bool billable)
		{
			var now = DateTime.Now;

			return await this._api.Post<ITimeEntry>($"workspaces/{workspaceId}/time_entries", new
			{
				billable,
				created_with = "flow-toggl-plugin",
				description,
				duration = (int)Math.Floor((-1 * now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds),
				project_id = projectId ?? default(int?),
				start = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
				tags,
				workspace_id = workspaceId,
			});
		}

		public async Task<ITimeEntry> GetRunningTimeEntry()
		{
			return await this._api.Get<ITimeEntry>("me/time_entries/current");
		}

		public async Task<List<IClient>> GetWorkspaceClients(int workspaceId)
		{
			return await this._api.Get<List<IClient>>($"workspaces/{workspaceId}/clients");
		}

		public async Task<List<ITag>> GetWorkspaceTags(int workspaceId)
		{
			return await this._api.Get<List<ITag>>($"workspaces/{workspaceId}/tags");
		}

		public async Task<ITimeEntry> StopTimeEntry(int id, int workspaceId)
		{
			return await this._api.Patch<ITimeEntry>($"workspaces/{workspaceId}/time_entries/{id}/stop", new { });
		}

		public async Task<List<ITimeEntry>> GetTimeEntries(DateTime startDate, DateTime endDate)
		{
			return await this._api.Get<List<ITimeEntry>>($"me/time_entries?start_date={startDate:yyyy-MM-ddTHH:mm:ss.fffZ}&end_date={endDate:yyyy-MM-ddTHH:mm:ss.fffZ}");
		}
	}
}
