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
		private readonly static string _reportsPaginationHeader = "X-Next-Row-Number";
		private readonly AuthenticatedFetch _api;
		private readonly AuthenticatedFetch _reportsApi;

		public TogglClient(string token)
		{
			this._api = new AuthenticatedFetch(token, TogglClient._baseUrl);
			this._reportsApi = new AuthenticatedFetch(token, TogglClient._reportsUrl, TogglClient._reportsPaginationHeader);
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

		public async Task<TimeEntryResponse?> CreateTimeEntry(
			long workspaceId,
			long? projectId,
			string? description,
			DateTimeOffset start,
			List<string>? tags = null,
			bool? billable = null
		)
		{
			return await this._api.Post<TimeEntryResponse>($"workspaces/{workspaceId}/time_entries", new
			{
				billable,
				created_with = "flow-toggl-plugin",
				description,
				duration = -1 * start.ToUnixTimeSeconds(),
				project_id = projectId ?? default(long?),
				start = start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				tags,
				workspace_id = workspaceId,
			});
		}

		public async Task<TimeEntryResponse?> EditTimeEntry(
			long workspaceId,
			long? projectId,
			long id,
			string? description = null,
			DateTimeOffset? start = null,
			DateTimeOffset? stop = null,
			long? duration = null,
			List<string>? tags = null,
			bool? billable = null
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

		public async Task<TimeEntryResponse?> StopTimeEntry(long workspaceId, long id)
		{
			return await this._api.Patch<TimeEntryResponse>($"workspaces/{workspaceId}/time_entries/{id}/stop", new { });
		}

		public async Task<HttpStatusCode?> DeleteTimeEntry(long workspaceId, long id)
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

		public async Task<SummaryReportResponse?> GetSummaryReport(
			long workspaceId,
			long userId,
			Settings.ReportsGroupingKey reportGrouping,
			DateTimeOffset start,
			DateTimeOffset? end
		)
		{
			(string grouping, string sub_grouping, bool includeTimeEntryIds) = (reportGrouping) switch
			{
				Settings.ReportsGroupingKey.Projects => ("projects", "time_entries", false),
				Settings.ReportsGroupingKey.Clients => ("clients", "projects", false),
				Settings.ReportsGroupingKey.Entries => ("projects", "time_entries", true),
				_ => ("projects", "time_entries", false),
			};

			return await this._reportsApi.Post<SummaryReportResponse>($"workspace/{workspaceId}/summary/time_entries", new
			{
				user_ids = new long[] { userId },
				start_date = start.ToString("yyyy-MM-dd"),
				end_date = end?.ToString("yyyy-MM-dd"),
				grouping,
				sub_grouping,
				include_time_entry_ids = includeTimeEntryIds,
			});
		}

		public async Task<List<DetailedReportTimeEntryGroupResponse>> GetDetailedReport(
			long workspaceId,
			long userId,
			List<long?>? projectIds,
			DateTimeOffset start,
			DateTimeOffset? end
		)
		{
			List<DetailedReportTimeEntryGroupResponse> results = new List<DetailedReportTimeEntryGroupResponse>();
			int? nextPaginationCursor = 1;

			while (nextPaginationCursor is not null)
			{
				var response = await this._reportsApi.Post<List<DetailedReportTimeEntryGroupResponse>>($"workspace/{workspaceId}/search/time_entries", new
				{
					user_ids = new long[] { userId },
					project_ids = projectIds,
					start_date = start.ToString("yyyy-MM-dd"),
					end_date = end?.ToString("yyyy-MM-dd"),
					grouped = true,
					order_by = "date",
					order_dir = "DESC",
					first_row_number = nextPaginationCursor,
				});

				if (response is null)
				{
					return results;
				}

				results.AddRange(response);
				nextPaginationCursor = this._reportsApi.nextPaginationCursor;
			}

			return results;
		}
	}
}
