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

		public async Task<MeResponse?> GetMe()
		{
			return await this._api.Get<MeResponse>("me?with_related_data=true");
		}

		public async Task<List<ProjectResponse>?> GetWorkspaceProjects(long workspaceId)
		{
			return await this._api.Get<List<ProjectResponse>>($"workspaces/{workspaceId}/projects?per_page=500");
		}

		public async Task<TimeEntryResponse?> CreateTimeEntry(
			long? projectId,
			long workspaceId,
			string? description,
			DateTimeOffset? start,
			List<string>? tags,
			bool? billable
		)
		{
			var dateTimeOffset = (start is not null)
				? (DateTimeOffset)start
				: DateTimeOffset.UtcNow;

			return await this._api.Post<TimeEntryResponse>($"workspaces/{workspaceId}/time_entries", new
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
		
		public async Task<TimeEntryResponse?> EditTimeEntry(
			long id,
			long workspaceId,
			long? projectId,
			string? description,
			DateTimeOffset? start,
			DateTimeOffset? stop,
			long? duration,
			List<string>? tags,
			bool? billable
		)
		{
			if (start is not null)
			{
				duration = -1 * ((DateTimeOffset)start).ToUnixTimeSeconds();
			}
			if (stop is not null)
			{
				duration = default(long?);
			}

			return await this._api.Put<TimeEntryResponse>($"workspaces/{workspaceId}/time_entries/{id}", new
			{
				billable,
				created_with = "flow-toggl-plugin",
				description,
				duration,
				project_id = projectId,
				start = start?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				stop = stop?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				tags,
				workspace_id = workspaceId,
			});
		}

		public async Task<TimeEntryResponse?> GetRunningTimeEntry()
		{
			return await this._api.Get<TimeEntryResponse>("me/time_entries/current");
		}

		public async Task<TimeEntryResponse?> StopTimeEntry(long id, long workspaceId)
		{
			return await this._api.Patch<TimeEntryResponse>($"workspaces/{workspaceId}/time_entries/{id}/stop", new { });
		}

		public async Task<HttpStatusCode?> DeleteTimeEntry(long id, long workspaceId)
		{
			return await this._api.Delete<HttpStatusCode>($"workspaces/{workspaceId}/time_entries/{id}");
		}

		public async Task<List<TimeEntryResponse>?> GetTimeEntries()
		{
			return await this._api.Get<List<TimeEntryResponse>>($"me/time_entries");
		}

		/* 
		 * Reports API
		 */

		public async Task<SummaryTimeEntryResponse?> GetSummaryTimeEntries(
			long workspaceId,
			long userId,
			Settings.ReportsGroupingKeys reportGrouping,
			DateTimeOffset start,
			DateTimeOffset? end
		)
		{
			(string grouping, string sub_grouping) = (reportGrouping) switch
			{
				Settings.ReportsGroupingKeys.Projects => ("projects", "time_entries"),
				Settings.ReportsGroupingKeys.Clients => ("clients", "projects"),
				Settings.ReportsGroupingKeys.Entries => ("projects", "time_entries"),
				_ => ("projects", "time_entries"),
			};

			return await this._reportsApi.Post<SummaryTimeEntryResponse>($"workspace/{workspaceId}/summary/time_entries", new
			{
				user_ids = new long[] { userId },
				start_date = start.ToLocalTime().ToString("yyyy-MM-dd"),
				end_date = end?.ToLocalTime().ToString("yyyy-MM-dd"),
				grouping,
				sub_grouping,
			});
		}
	}
}
