using System;
using System.Collections.Generic;
using System.Linq;
using Humanizer;
using Flow.Launcher.Plugin.TogglTrack.TogglApi;

namespace Flow.Launcher.Plugin.TogglTrack
{
	public class Me
	{
		public readonly string? ApiToken;
		public readonly long Id;

		public readonly long DefaultWorkspaceId;

		public readonly DayOfWeek BeginningOfWeek;
		public readonly string ReportsTimeZoneId;

		public readonly Dictionary<long, Client>? Clients;
		public readonly Dictionary<long, Project>? Projects;

		public readonly List<Project>? ActiveProjects;

		public Me(MeResponse response)
		{
			this.ApiToken = response.api_token;
			this.Id = response.id;

			this.DefaultWorkspaceId = response.default_workspace_id;

			try
			{
				this.BeginningOfWeek = Enum.Parse<DayOfWeek>(response.beginning_of_week.ToString());
			}
			catch
			{
				this.BeginningOfWeek = DayOfWeek.Monday;
			}

			this.ReportsTimeZoneId = response.timezone ?? TimeZoneInfo.Local.Id;

			this.Clients = response.clients?.ToDictionary(keySelector: clientResponse => clientResponse.id, elementSelector: clientResponse => clientResponse.ToClient(this));
			this.Projects = response.projects?.ToDictionary(keySelector: projectResponse => projectResponse.id, elementSelector: projectResponse => projectResponse.ToProject(this));

			this.ActiveProjects = this.Projects?.Where(pair => pair.Value.Active ?? false).Select(pair => pair.Value).ToList();
		}

		public Project? GetProject(long? id)
		{
			if ((id is null) || (this.Projects is null))
			{
				return null;
			}

			return this.Projects.GetValueOrDefault((long)id);
		}

		public Client? GetClient(long? id)
		{
			if ((id is null) || (this.Clients is null))
			{
				return null;
			}

			return this.Clients.GetValueOrDefault((long)id);
		}
	}

	public class Client
	{
		public readonly long Id;
		public readonly string Name;

		public Client(ClientResponse response, Me me)
		{
			this.Id = response.id;
			this.Name = response.name ?? "(no name)";
		}

		public string KebabName
		{
			get => this.Name.Kebaberize();
		}
	}

	public class Project
	{
		private readonly Me _me;

		public readonly long Id;
		public readonly string Name;

		public readonly long WorkspaceId;
		public readonly long? ClientId;

		public readonly bool? Active;
		public readonly int? ActualHours;
		public readonly string? Colour;

		public readonly Client? Client;

		public Project(ProjectResponse response, Me me)
		{
			this._me = me;

			this.Id = response.id;
			this.Name = response.name ?? "(no name)";

			this.ClientId = response.client_id;
			this.WorkspaceId = response.workspace_id;

			this.Active = response.active;
			this.ActualHours = response.actual_hours;
			this.Colour = response.color;

			this.Client = this._me.GetClient(this.ClientId);
		}

		public string WithClientName
		{
			get => (this.Client is not null)
				? $"{this.Name} • {this.Client.Name}"
				: this.Name;
		}

		public string KebabName
		{
			get => this.Name.Kebaberize();
		}

		public string ElapsedString
		{
			get => ((this.ActualHours ?? 0) == 1)
				? $"{this.ActualHours ?? 0} hour"
				: $"{this.ActualHours ?? 0} hours";
		}
	}

	public class TimeEntry
	{
		private readonly Me _me;
		private readonly string? _rawDescription;

		public readonly long Id;

		public readonly long WorkspaceId;
		public readonly long? ProjectId;

		public readonly bool? Billable;
		public readonly long Duration;
		public readonly string Start;
		public readonly string? Stop;
		public readonly List<string>? Tags;

		public readonly Project? Project;

		public TimeEntry(TimeEntryResponse response, Me me)
		{
			this._me = me;
			this._rawDescription = response.description;

			this.Id = response.id;

			this.WorkspaceId = response.workspace_id;
			this.ProjectId = response.project_id;

			this.Billable = response.billable;
			this.Duration = response.duration;
			this.Start = response.start ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz");
			this.Stop = response.stop;
			this.Tags = response.tags;

			this.Project = this._me.GetProject(this.ProjectId);
		}

		public TimeEntry(DetailedReportTimeEntryResponse timeEntryResponse, DetailedReportTimeEntryGroupResponse timeEntryGroupResponse, Me me)
		{
			this._me = me;
			this._rawDescription = timeEntryGroupResponse.description;

			this.Id = timeEntryResponse.id;

			this.WorkspaceId = me.DefaultWorkspaceId;
			this.ProjectId = timeEntryGroupResponse.project_id;

			this.Billable = timeEntryGroupResponse.billable;
			this.Duration = timeEntryResponse.seconds;
			this.Start = timeEntryResponse.start ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz");
			this.Stop = timeEntryResponse.stop;

			this.Project = this._me.GetProject(this.ProjectId);
		}

		public string? GetRawDescription(bool withTrailingSpace = false)
		{
			if (string.IsNullOrEmpty(this._rawDescription))
			{
				return string.Empty;
			}

			if (!withTrailingSpace)
			{
				return this._rawDescription;
			}

			return $"{this._rawDescription} ";
		}

		public string Description
		{
			get => (string.IsNullOrEmpty(this._rawDescription)) ? "(no description)" : this._rawDescription;
		}

		public DateTimeOffset StartDate
		{
			get => DateTimeOffset.Parse(this.Start);
		}

		public string HumanisedStart
		{
			get => DateTime.Parse(this.Start).Humanize(false);
		}

		public bool IsRunning
		{
			get => (this.Stop is null);
		}

		public DateTimeOffset? StopDate
		{
			get => (this.Stop is not null)
				? DateTimeOffset.Parse(this.Stop)
				: null;
		}

		public string? HumanisedStop
		{
			get => (this.Stop is not null)
				? DateTime.Parse(this.Stop).Humanize(false)
				: null;
		}

		public TimeSpan Elapsed
		{
			get => (this.Duration < 0)
				? DateTimeOffset.UtcNow.Subtract(this.StartDate)
				: TimeSpan.FromSeconds(this.Duration);
		}

		public string HumanisedElapsed
		{
			get => this.Elapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour);
		}

		public string DetailedElapsed
		{
			get => $"{(int)this.Elapsed.TotalHours}:{this.Elapsed.ToString(@"mm\:ss")}";
		}
	}

	public class SummaryReport : ICloneable
	{
		private readonly Me _me;

		// Key as -1 for 'No Project' or 'No Client' cases
		public Dictionary<long, SummaryReportGroup> Groups;

		public SummaryReport(SummaryReportResponse response, Me me)
		{
			this._me = me;

			this.Groups = response.groups?.ToDictionary(keySelector: groupResponse => SummaryReport.GetGroupKey(groupResponse.id), elementSelector: groupResponse => groupResponse.ToSummaryReportGroup(me)) ?? new Dictionary<long, SummaryReportGroup>();
		}
		public SummaryReport(SummaryReport summary)
		{
			this._me = summary._me;

			this.Groups = summary.Groups.ToDictionary(keySelector: pair => pair.Key, elementSelector: pair => pair.Value.Clone());
		}

		internal static long GetGroupKey(long? id)
		{
			return id ?? -1;
		}

		public SummaryReport Clone()
		{
			return new SummaryReport(this);
		}
		object ICloneable.Clone()
		{
			return this.Clone();
		}

		public SummaryReport? InsertRunningTimeEntry(TimeEntry timeEntry, Settings.ReportsGroupingKey reportGrouping)
		{
			const string PROJECTS_KEY = "projects";
			const string CLIENTS_KEY = "clients";
			const string ENTRIES_KEY = "time_entries";

			(string grouping, string subGrouping) = (reportGrouping) switch
			{
				Settings.ReportsGroupingKey.Projects => (PROJECTS_KEY, ENTRIES_KEY),
				Settings.ReportsGroupingKey.Clients => (CLIENTS_KEY, PROJECTS_KEY),
				Settings.ReportsGroupingKey.Entries => (PROJECTS_KEY, ENTRIES_KEY),
				_ => (PROJECTS_KEY, ENTRIES_KEY),
			};

			// Perform deep copy of summary so the cache is not mutated
			var clonedSummary = this.Clone();
			if (clonedSummary is null)
			{
				return null;
			}

			long? groupId = (grouping) switch
			{
				PROJECTS_KEY => timeEntry.ProjectId,
				CLIENTS_KEY => timeEntry.Project?.ClientId,
				// Default grouping of projects
				_ => timeEntry.ProjectId,
			};

			var newSubGroup = (subGrouping) switch
			{
				ENTRIES_KEY => new SummaryReportSubGroupResponse
				{
					title = timeEntry.GetRawDescription(),
					seconds = (int)timeEntry.Elapsed.TotalSeconds,
				},
				PROJECTS_KEY => new SummaryReportSubGroupResponse
				{
					id = timeEntry.ProjectId,
					seconds = (int)timeEntry.Elapsed.TotalSeconds,
				},
				// Default sub-grouping of entries
				_ => new SummaryReportSubGroupResponse
				{
					title = timeEntry.GetRawDescription(),
					seconds = (int)timeEntry.Elapsed.TotalSeconds,
				},
			};

			var group = clonedSummary.GetGroup(groupId);
			var subGroup = group?.GetSubGroup(newSubGroup.id, newSubGroup.title);

			if (subGroup is not null)
			{
				subGroup.Seconds += (int)timeEntry.Elapsed.TotalSeconds;
				return clonedSummary;
			}

			if (group?.SubGroups is not null)
			{
				group.SubGroups.Add(group.GetSubGroupKey(newSubGroup.id, newSubGroup.title), new SummaryReportSubGroup(newSubGroup));
				return clonedSummary;
			}

			if (group is not null)
			{
				group.SubGroups = new Dictionary<string, SummaryReportSubGroup>
				{
					{
						group.GetSubGroupKey(newSubGroup.id, newSubGroup.title), new SummaryReportSubGroup(newSubGroup)
					},
				};
				return clonedSummary;
			}

			clonedSummary.Groups.Add(
				SummaryReport.GetGroupKey(groupId),
				new SummaryReportGroup(
					new SummaryReportGroupResponse
					{
						id = groupId,
						sub_groups = new List<SummaryReportSubGroupResponse>
						{
							newSubGroup,
						},
					},
					this._me
				)
			);

			return clonedSummary;
		}

		public long Seconds
		{
			get => this.Groups.Values.Sum(group => group.Seconds);
		}

		public TimeSpan Elapsed
		{
			get => TimeSpan.FromSeconds(this.Seconds);
		}

		public string HumanisedElapsed
		{
			get => this.Elapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour);
		}

		public string DetailedElapsed
		{
			get => $"{(int)this.Elapsed.TotalHours}:{this.Elapsed.ToString(@"mm\:ss")}";
		}

		public SummaryReportGroup? GetGroup(long? id)
		{
			return this.Groups.GetValueOrDefault(SummaryReport.GetGroupKey(id));
		}
	}

	public class SummaryReportGroup : ICloneable
	{
		private readonly Me _me;

		public long? Id;
		public Dictionary<string, SummaryReportSubGroup>? SubGroups;

		public Project? Project;
		public Client? Client;

		public SummaryReportGroup(SummaryReportGroupResponse response, Me me)
		{
			this._me = me;

			this.Id = response.id;
			this.SubGroups = response.sub_groups?.ToDictionary(keySelector: subGroupResponse => this.GetSubGroupKey(subGroupResponse.id, subGroupResponse.title), elementSelector: subGroupResponse => subGroupResponse.ToSummaryReportSubGroup());

			this.Project = me.GetProject(this.Id);
			this.Client = me.GetClient(this.Id);
		}
		public SummaryReportGroup(SummaryReportGroup group)
		{
			this._me = group._me;

			this.Id = group.Id;
			this.SubGroups = group.SubGroups?.ToDictionary(keySelector: pair => pair.Key, elementSelector: pair => pair.Value.Clone());

			this.Project = group.Project;
			this.Client = group.Client;
		}

		internal string GetSubGroupKey(long? id, string? title)
		{
			return $"{this.Id?.ToString("X") ?? "-1"}-{id?.ToString("X") ?? title}";
		}

		public SummaryReportGroup Clone()
		{
			return new SummaryReportGroup(this);
		}
		object ICloneable.Clone()
		{
			return this.Clone();
		}

		public long Seconds
		{
			get => this?.SubGroups?.Values.Sum(subGroup => subGroup.Seconds) ?? 0;
		}

		public TimeSpan Elapsed
		{
			get => TimeSpan.FromSeconds(this.Seconds);
		}

		public string HumanisedElapsed
		{
			get => this.Elapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour);
		}

		public string DetailedElapsed
		{
			get => $"{(int)this.Elapsed.TotalHours}:{this.Elapsed.ToString(@"mm\:ss")}";
		}

		public SummaryReportSubGroup? LongestSubGroup
		{
			get => this.SubGroups?.Values.MaxBy(subGroup => subGroup.Seconds);
		}

		public SummaryReportSubGroup? GetSubGroup(long? id, string? title)
		{
			if (this.SubGroups is null)
			{
				return null;
			}

			return this.SubGroups.GetValueOrDefault(this.GetSubGroupKey(id, title));
		}
	}

	public class SummaryReportSubGroup : ICloneable
	{
		private string? _rawTitle;

		public long? Id;
		public long Seconds;
		public List<long>? Ids;

		public SummaryReportSubGroup(SummaryReportSubGroupResponse response)
		{
			this._rawTitle = response.title;

			this.Id = response.id;
			this.Seconds = response.seconds;
			this.Ids = response.ids;
		}
		public SummaryReportSubGroup(SummaryReportSubGroup subGroup)
		{
			this.Id = subGroup.Id;
			this._rawTitle = subGroup._rawTitle;
			this.Seconds = subGroup.Seconds;
			this.Ids = (subGroup.Ids is not null)
				? new List<long>(subGroup.Ids)
				: null;
		}

		public SummaryReportSubGroup Clone()
		{
			return new SummaryReportSubGroup(this);
		}
		object ICloneable.Clone()
		{
			return this.Clone();
		}

		public string? GetRawTitle(bool withTrailingSpace = false)
		{
			if (string.IsNullOrEmpty(this._rawTitle))
			{
				return string.Empty;
			}

			if (!withTrailingSpace)
			{
				return this._rawTitle;
			}

			return $"{this._rawTitle} ";
		}

		public string Title
		{
			get => (string.IsNullOrEmpty(this._rawTitle)) ? "(no description)" : this._rawTitle;
		}

		public TimeSpan Elapsed
		{
			get => TimeSpan.FromSeconds(this.Seconds);
		}

		public string HumanisedElapsed
		{
			get => this.Elapsed.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second, maxUnit: Humanizer.Localisation.TimeUnit.Hour);
		}

		public string DetailedElapsed
		{
			get => $"{(int)this.Elapsed.TotalHours}:{this.Elapsed.ToString(@"mm\:ss")}";
		}

		public long? LatestId
		{
			get => (this.Ids?.Any() ?? false)
				? this.Ids.Max()
				: null;
		}
	}

	public class DetailedReportTimeEntryGroup
	{
		private readonly Me _me;

		public List<TimeEntry> TimeEntries;

		public DetailedReportTimeEntryGroup(DetailedReportTimeEntryGroupResponse response, Me me)
		{
			this._me = me;

			this.TimeEntries = response.time_entries?.ConvertAll(timeEntryResponse => timeEntryResponse.ToTimeEntry(me, response)) ?? new List<TimeEntry>();
		}
		public DetailedReportTimeEntryGroup(TimeEntry timeEntry, Me me)
		{
			this._me = me;

			this.TimeEntries = new List<TimeEntry>
			{
				timeEntry,
			};
		}
	}
}