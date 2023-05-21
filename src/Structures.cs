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

		public readonly Dictionary<long, Client>? Clients;
		public readonly Dictionary<long, Project>? Projects;

		public readonly List<Project>? ActiveProjects;

		public Me(MeResponse response)
		{
			this.ApiToken = response.api_token;

			this.DefaultWorkspaceId = response.default_workspace_id;
			this.Id = response.id;
			
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

		public string GetColourIcon(PluginInitContext context, string fallbackIcon)
		{
			return (this.Colour is not null)
				? new ColourIcon(context, this.Colour, "start.png").GetColourIcon()
				: fallbackIcon;
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

		public readonly long Id;
		public readonly string? RawDescription;

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

			this.Id = response.id;
			this.RawDescription = response.description;

			this.WorkspaceId = response.workspace_id;
			this.ProjectId = response.project_id;

			this.Billable = response.billable;
			this.Duration = response.duration;
			this.Start = response.start ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz");
			this.Stop = response.stop;
			this.Tags = response.tags;

			this.Project = this._me.GetProject(this.ProjectId);
		}

		public string Description
		{
			get => (string.IsNullOrEmpty(this.RawDescription)) ? "(no description)" : this.RawDescription;
		}

		public DateTimeOffset StartDate
		{
			get => DateTimeOffset.Parse(this.Start);
		}

		public string HumanisedStart
		{
			get => DateTime.Parse(this.Start).Humanize(false);
		}

		public DateTimeOffset? StopDate
		{
			get => (this.Stop is not null)
				? DateTimeOffset.Parse(this.Stop)
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

	public class SummaryTimeEntry : ICloneable
	{
		private readonly Me _me;

		// Key as -1 for 'No Project' or 'No Client' cases
		public Dictionary<long, SummaryTimeEntryGroup> Groups;

		public SummaryTimeEntry(SummaryTimeEntryResponse response, Me me)
		{
			this._me = me;

			this.Groups = response.groups?.ToDictionary(keySelector: groupResponse => SummaryTimeEntry.GetGroupKey(groupResponse.id), elementSelector: groupResponse => groupResponse.ToSummaryTimeEntryGroup(me)) ?? new Dictionary<long, SummaryTimeEntryGroup>();
		}
		public SummaryTimeEntry(SummaryTimeEntry summary)
		{
			this._me = summary._me;

			this.Groups = summary.Groups.ToDictionary(keySelector: pair => pair.Key, elementSelector: pair => pair.Value.Clone());
		}

		internal static long GetGroupKey(long? id)
		{
			return id ?? -1;
		}

		public SummaryTimeEntry Clone()
		{
			return new SummaryTimeEntry(this);
		}
		object ICloneable.Clone()
		{
			return this.Clone();
		}

		public SummaryTimeEntry? InsertRunningTimeEntry(TimeEntry timeEntry, Settings.ReportsGroupingKeys reportGrouping)
		{
			const string PROJECTS_KEY = "projects";
			const string CLIENTS_KEY = "clients";
			const string ENTRIES_KEY = "time_entries";

			(string grouping, string subGrouping) = (reportGrouping) switch
			{
				Settings.ReportsGroupingKeys.Projects => (PROJECTS_KEY, ENTRIES_KEY),
				Settings.ReportsGroupingKeys.Clients => (CLIENTS_KEY, PROJECTS_KEY),
				Settings.ReportsGroupingKeys.Entries => (PROJECTS_KEY, ENTRIES_KEY),
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

			var group = clonedSummary.GetGroup(groupId);
			var subGroup = group?.GetSubGroup(timeEntry.Id, timeEntry.RawDescription);

			if (subGroup is not null)
			{
				subGroup.Seconds += (int)timeEntry.Elapsed.TotalSeconds;
				return clonedSummary;
			}

			var newSubGroup = (subGrouping) switch
			{
				ENTRIES_KEY => new SummaryTimeEntrySubGroupResponse
				{
					title = timeEntry.RawDescription,
					seconds = (int)timeEntry.Elapsed.TotalSeconds,
				},
				PROJECTS_KEY => new SummaryTimeEntrySubGroupResponse
				{
					id = timeEntry.ProjectId,
					seconds = (int)timeEntry.Elapsed.TotalSeconds,
				},
				// Default sub-grouping of entries
				_ => new SummaryTimeEntrySubGroupResponse
				{
					title = timeEntry.RawDescription,
					seconds = (int)timeEntry.Elapsed.TotalSeconds,
				},
			};

			if (group?.SubGroups is not null)
			{
				group.SubGroups.Add(group.GetSubGroupKey(newSubGroup.id, newSubGroup.title), new SummaryTimeEntrySubGroup(newSubGroup));
				return clonedSummary;
			}

			if (group is not null)
			{
				group.SubGroups = new Dictionary<string, SummaryTimeEntrySubGroup>
				{
					{
						group.GetSubGroupKey(newSubGroup.id, newSubGroup.title), new SummaryTimeEntrySubGroup(newSubGroup)
					}
				};
				return clonedSummary;
			}

			clonedSummary.Groups.Add(
				SummaryTimeEntry.GetGroupKey(groupId),
				new SummaryTimeEntryGroup(
					new SummaryTimeEntryGroupResponse
					{
						id = groupId,
						sub_groups = new List<SummaryTimeEntrySubGroupResponse>
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

		public SummaryTimeEntryGroup? GetGroup(long? id)
		{
			return this.Groups.GetValueOrDefault(SummaryTimeEntry.GetGroupKey(id));
		}
	}

	public class SummaryTimeEntryGroup : ICloneable
	{
		private readonly Me _me;

		public long? Id;
		public Dictionary<string, SummaryTimeEntrySubGroup>? SubGroups;

		public Project? Project;
		public Client? Client;

		public SummaryTimeEntryGroup(SummaryTimeEntryGroupResponse response, Me me)
		{
			this._me = me;

			this.Id = response.id;
			this.SubGroups = response.sub_groups?.ToDictionary(keySelector: subGroupResponse => this.GetSubGroupKey(subGroupResponse.id, subGroupResponse.title), elementSelector: subGroupResponse => subGroupResponse.ToSummaryTimeEntrySubGroup());

			this.Project = me.GetProject(this.Id);
			this.Client = me.GetClient(this.Id);
		}
		public SummaryTimeEntryGroup(SummaryTimeEntryGroup group)
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
		
		public SummaryTimeEntryGroup Clone()
		{
			return new SummaryTimeEntryGroup(this);
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

		public SummaryTimeEntrySubGroup? LongestSubGroup
		{
			get => this.SubGroups?.Values.MaxBy(subGroup => subGroup.Seconds);
		}

		public SummaryTimeEntrySubGroup? GetSubGroup(long? id, string? title)
		{
			if (this.SubGroups is null)
			{
				return null;
			}

			return this.SubGroups.GetValueOrDefault(this.GetSubGroupKey(id, title));
		}
	}

	public class SummaryTimeEntrySubGroup : ICloneable
	{
		public long? Id;
		public string? RawTitle;
		public long Seconds;

		public SummaryTimeEntrySubGroup(SummaryTimeEntrySubGroupResponse response)
		{
			this.Id = response.id;
			this.RawTitle = response.title;
			this.Seconds = response.seconds;
		}
		public SummaryTimeEntrySubGroup(SummaryTimeEntrySubGroup subGroup)
		{
			this.Id = subGroup.Id;
			this.RawTitle = subGroup.RawTitle;
			this.Seconds = subGroup.Seconds;
		}

		public SummaryTimeEntrySubGroup Clone()
		{
			return new SummaryTimeEntrySubGroup(this);
		}
		object ICloneable.Clone()
		{
			return this.Clone();
		}

		public string Title
		{
			get => (string.IsNullOrEmpty(this.RawTitle)) ? "(no description)" : this.RawTitle;
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
	}
}