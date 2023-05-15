using System;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.TogglTrack.TogglApi
{
	public class TogglClient
	{
		private readonly static string _baseUrl = "https://api.track.toggl.com/api/v9/";
		private readonly static string _reportsUrl = "https://api.track.toggl.com/reports/api/v3/";
		private readonly AuthenticatedFetch _api;
		private readonly AuthenticatedFetch _reportsApi;

		public TogglClient(string token)
		{
			this._api = new AuthenticatedFetch(token, TogglClient._baseUrl);
			this._reportsApi = new AuthenticatedFetch(token, TogglClient._reportsUrl);
		}

		public void UpdateToken(string token)
		{
			this._api.UpdateToken(token);
			this._reportsApi.UpdateToken(token);
		}

		/* 
	     * Standard API
		 */

		public async Task<Me?> GetMe()
		{
			return await this._api.Get<Me>("me?with_related_data=true");
		}

		public async Task<List<Workspace>?> GetWorkspaces()
		{
			return await this._api.Get<List<Workspace>>("workspaces");
		}

		public async Task<List<Project>?> GetWorkspaceProjects(long workspaceId)
		{
			return await this._api.Get<List<Project>>($"workspaces/{workspaceId}/projects?per_page=500");
		}

		public async Task<TimeEntry?> CreateTimeEntry(long? projectId, long workspaceId, string? description, DateTimeOffset? start, List<string>? tags, bool? billable)
		{
			var dateTimeOffset = (start is not null)
				? (DateTimeOffset)start
				: DateTimeOffset.UtcNow;

			return await this._api.Post<TimeEntry>($"workspaces/{workspaceId}/time_entries", new
			{
				billable,
				created_with = "flow-toggl-plugin",
				description,
				duration = -1 * dateTimeOffset.ToUnixTimeSeconds(),
				project_id = projectId ?? default(long?),
				start = dateTimeOffset.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				tags,
				workspace_id = workspaceId,
			});
		}
		
		public async Task<TimeEntry?> EditTimeEntry(TimeEntry timeEntry, long? projectId, string? description, DateTimeOffset? start, DateTimeOffset? stop)
		{
			long? duration = timeEntry.duration;
			if (start is not null)
			{
				duration = -1 * ((DateTimeOffset)start).ToUnixTimeSeconds();
			}
			if (stop is not null)
			{
				duration = default(long?);
			}

			return await this._api.Put<TimeEntry>($"workspaces/{timeEntry.workspace_id}/time_entries/{timeEntry.id}", new
			{
				timeEntry.billable,
				created_with = "flow-toggl-plugin",
				description,
				duration,
				project_id = projectId,
				start = start?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				stop = stop?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				timeEntry.tags,
				timeEntry.workspace_id,
			});
		}

		public async Task<TimeEntry?> GetRunningTimeEntry()
		{
			return await this._api.Get<TimeEntry>("me/time_entries/current");
		}

		public async Task<List<Client>?> GetWorkspaceClients(long workspaceId)
		{
			return await this._api.Get<List<Client>>($"workspaces/{workspaceId}/clients");
		}

		public async Task<List<Tag>?> GetWorkspaceTags(long workspaceId)
		{
			return await this._api.Get<List<Tag>>($"workspaces/{workspaceId}/tags");
		}

		public async Task<TimeEntry?> StopTimeEntry(long id, long workspaceId)
		{
			return await this._api.Patch<TimeEntry>($"workspaces/{workspaceId}/time_entries/{id}/stop", new { });
		}

		public async Task<HttpStatusCode?> DeleteTimeEntry(long id, long workspaceId)
		{
			return await this._api.Delete<HttpStatusCode>($"workspaces/{workspaceId}/time_entries/{id}");
		}

		public async Task<List<TimeEntry>?> GetTimeEntries()
		{
			return await this._api.Get<List<TimeEntry>>($"me/time_entries");
		}

		/* 
		 * Reports API
		 */

		public async Task<List<SummaryProjectReport>?> GetSummaryProjectReports(long workspaceId, DateTimeOffset start, DateTimeOffset? end)
		{
			return await this._reportsApi.Post<List<SummaryProjectReport>>($"workspace/{workspaceId}/projects/summary", new
			{
				start_date = start.ToString("yyyy-MM-dd"),
				end_date = end?.ToString("yyyy-MM-dd"),
			});
		}
	}
}
