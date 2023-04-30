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

		public void UpdateToken(string token)
		{
			this._api.UpdateToken(token);
		}

		public async Task<Me> GetMe()
		{
			return await this._api.Get<Me>("me?with_related_data=true");
		}

		public async Task<List<Workspace>> GetWorkspaces()
		{
			return await this._api.Get<List<Workspace>>("workspaces");
		}

		public async Task<List<Project>> GetWorkspaceProjects(long workspaceId)
		{
			return await this._api.Get<List<Project>>($"workspaces/{workspaceId}/projects?per_page=500") ?? new List<Project>();
		}

		public async Task<TimeEntry> CreateTimeEntry(long? projectId, long workspaceId, string description, List<string> tags, bool? billable)
		{
			return await this._api.Post<TimeEntry>($"workspaces/{workspaceId}/time_entries", new
			{
				billable,
				created_with = "flow-toggl-plugin",
				description,
				duration = -1 * DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				project_id = projectId ?? default(long?),
				start = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				tags,
				workspace_id = workspaceId,
			});
		}
		
		public async Task<TimeEntry> EditTimeEntry(TimeEntry timeEntry, string description)
		{
			return await this._api.Put<TimeEntry>($"workspaces/{timeEntry.workspace_id}/time_entries/{timeEntry.id}", new
			{
				timeEntry.billable,
				created_with = "flow-toggl-plugin",
				description,
				timeEntry.duration,
				timeEntry?.project_id,
				timeEntry.tags,
				timeEntry.workspace_id,
			});
		}

		public async Task<TimeEntry> GetRunningTimeEntry()
		{
			return await this._api.Get<TimeEntry>("me/time_entries/current");
		}

		public async Task<List<Client>> GetWorkspaceClients(long workspaceId)
		{
			return await this._api.Get<List<Client>>($"workspaces/{workspaceId}/clients");
		}

		public async Task<List<Tag>> GetWorkspaceTags(long workspaceId)
		{
			return await this._api.Get<List<Tag>>($"workspaces/{workspaceId}/tags");
		}

		public async Task<TimeEntry> StopTimeEntry(long id, long workspaceId)
		{
			return await this._api.Patch<TimeEntry>($"workspaces/{workspaceId}/time_entries/{id}/stop", new { });
		}

		public async Task<List<TimeEntry>> GetTimeEntries()
		{
			return await this._api.Get<List<TimeEntry>>($"me/time_entries");
		}
	}
}
