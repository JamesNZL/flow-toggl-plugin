using System.Collections.Generic;

namespace Flow.Launcher.Plugin.TogglTrack.TogglApi
{
	public class MeResponse
	{
		public string? api_token { get; set; }
		// public string? at { get; set; }
		public int beginning_of_week { get; set; }
		public List<ClientResponse>? clients { get; set; }
		// public long? country_id { get; set; }
		// public string? created_at { get; set; }
		public long default_workspace_id { get; set; }
		// public string? email { get; set; }
		// public string? fullname { get; set; }
		// public bool? has_password { get; set; }
		public long id { get; set; }
		// public string? image_url { get; set; }
		// public string? intercom_hash { get; set; }
		// public List<string>? oath_providers { get; set; }
		// public string? openid_email { get; set; }
		// public bool? openid_enabled { get; set; }
		public List<ProjectResponse>? projects { get; set; }
		// public List<Tasks>? tasks { get; set; }
		// public List<Tag>? tags { get; set; }
		// public List<TimeEntry>? time_entries { get; set; }
		public string? timezone { get; set; }
		// public string? updated_at { get; set; }
		// public List<Workspace>? workspaces { get; set; }

		public Me ToMe()
		{
			return new Me(this);
		}
	}

	// public class WorkspaceResponse
	// {
	// 	// public bool? admin { get; set; }
	// 	// public string? api_token { get; set; }
	// 	// public string? at { get; set; }
	// 	// public bool? business_ws { get; set; }
	// 	// public string? default_currency { get; set; }
	// 	// public float? default_hourly_rate { get; set; }
	// 	// public bool? ical_enabled { get; set; }
	// 	// public string? ical_url { get; set; }
	// 	// public long? id { get; set; }
	// 	// public string? logo_url { get; set; }
	// 	// public string? name { get; set; }
	// 	// public bool? only_admins_may_create_projects { get; set; }
	// 	// public bool? only_admins_may_create_tags { get; set; }
	// 	// public bool? only_admins_see_billable_rates { get; set; }
	// 	// public bool? only_admins_see_team_dashboard { get; set; }
	// 	// public long? organization_id { get; set; }
	// 	// public bool? premium { get; set; }
	// 	// public long? profile { get; set; }
	// 	// public bool? projects_billable_by_default { get; set; }
	// 	// public string? rate_last_updated { get; set; }
	// 	// public bool? reports_collapse { get; set; }
	// 	// public long? rounding { get; set; }
	// 	// public long? rounding_minutes { get; set; }
	// 	// public string? server_deleted_at { get; set; }
	// 	// public string? suspended_at { get; set; }
	// }

	public class ClientResponse
	{
		// public bool? archived { get; set; }
		// public string? at { get; set; }
		public long id { get; set; }
		public string? name { get; set; }
		// public string? server_deleted_at { get; set; }
		// public long? wid { get; set; }

		public Client ToClient(Me me)
		{
			return new Client(this, me);
		}
	}

	public class ProjectResponse
	{
		public bool? active { get; set; }
		public int? actual_hours { get; set; }
		// public string? at { get; set; }
		// public bool? auto_estimates { get; set; }
		// public bool? billable { get; set; }
		// public long? cid { get; set; }
		public long? client_id { get; set; }
		public string? color { get; set; }
		// public string? created_at { get; set; }
		// public string? currency { get; set; }
		// public string? end_date { get; set; }
		// public long? estimated_hours { get; set; }
		// public string? first_time_entry { get; set; }
		// public float? fixed_fee { get; set; }
		public long id { get; set; }
		// public bool? is_private { get; set; }
		public string? name { get; set; }
		// public float? rate { get; set; }
		// public string? rate_last_updated { get; set; }
		// public bool? recurring { get; set; }
		// public string? server_deleted_at { get; set; }
		// public string? start_date { get; set; }
		// public bool? template { get; set; }
		// public long? wid { get; set; }
		public long workspace_id { get; set; }

		public Project ToProject(Me me)
		{
			return new Project(this, me);
		}
	}

	// public class TasksResponse
	// {
	// 	// public bool? active { get; set; }
	// 	// public string? at { get; set; }
	// 	// public long? estimated_seconds { get; set; }
	// 	// public long? id { get; set; }
	// 	// public string? name { get; set; }
	// 	// public long? project_id { get; set; }
	// 	// public bool? recurring { get; set; }
	// 	// public string? server_deleted_at { get; set; }
	// 	// public long? tracked_seconds { get; set; }
	// 	// public long? user_id { get; set; }
	// 	// public long? workspace_id { get; set; }
	// }

	// public class TagResponse
	// {
	// 	// public string? at { get; set; }
	// 	// public string? deleted_at { get; set; }
	// 	// public long? id { get; set; }
	// 	// public string? name { get; set; }
	// 	// public long? workspace_id { get; set; }
	// }

	public class TimeEntryResponse
	{
		// public string? at { get; set; }
		public bool? billable { get; set; }
		public string? description { get; set; }
		public long duration { get; set; }
		// public bool? duronly { get; set; }
		public long id { get; set; }
		// public long? pid { get; set; }
		public long? project_id { get; set; }
		// public string? server_deleted_at { get; set; }
		public string? start { get; set; }
		public string? stop { get; set; }
		// public List<long>? tag_ids { get; set; }
		public List<string>? tags { get; set; }
		// public long? task_id { get; set; }
		// public long? tid { get; set; }
		// public long? uid { get; set; }
		// public long? user_id { get; set; }
		// public long? wid { get; set; }
		public long workspace_id { get; set; }

		public TimeEntry ToTimeEntry(Me me)
		{
			return new TimeEntry(this, me);
		}
	}

	public class SummaryReportResponse
	{
		public List<SummaryReportGroupResponse>? groups { get; set; }

		public SummaryReport ToSummaryReport(Me me)
		{
			return new SummaryReport(this, me);
		}
	}

	public class SummaryReportGroupResponse
	{
		public long? id { get; set; }
		public List<SummaryReportSubGroupResponse>? sub_groups { get; set; }

		public SummaryReportGroup ToSummaryReportGroup(Me me)
		{
			return new SummaryReportGroup(this, me);
		}
	}

	public class SummaryReportSubGroupResponse
	{
		public long? id { get; set; }
		public string? title { get; set; }
		public long seconds { get; set; }
		public List<long>? ids { get; set; }

		public SummaryReportSubGroup ToSummaryReportSubGroup(SummaryReportGroup group)
		{
			return new SummaryReportSubGroup(this, group);
		}
	}

	public class DetailedReportTimeEntryGroupResponse
	{
		public bool? billable { get; set; }
		// public long? billable_amount_in_cents { get; set; }
		// public string? currency { get; set; }
		public string? description { get; set; }
		// public long? hourly_rate_in_cents { get; set; }
		public long id { get; set; }
		public long? project_id { get; set; }
		// public List<long>? tag_ids { get; set; }
		// public long? task_id { get; set; }
		public List<DetailedReportTimeEntryResponse>? time_entries { get; set; }
		// public long? user_id { get; set; }
		// public string? username { get; set; }

		public DetailedReportTimeEntryGroup ToDetailedReportTimeEntryGroup(Me me)
		{
			return new DetailedReportTimeEntryGroup(this, me);
		}
	}

	public class DetailedReportTimeEntryResponse
	{
		// public string? at { get; set; }
		public long id { get; set; }
		public long seconds { get; set; }
		public string? start { get; set; }
		public string? stop { get; set; }

		public TimeEntry ToTimeEntry(Me me, DetailedReportTimeEntryGroupResponse timeEntryGroupResponse)
		{
			return new TimeEntry(this, timeEntryGroupResponse, me);
		}
	}
}