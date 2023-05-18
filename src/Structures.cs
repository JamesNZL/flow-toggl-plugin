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
		public readonly List<Client>? Clients;
		public readonly long DefaultWorkspaceId;
		public readonly long Id;
		public readonly List<Project>? Projects;

		public readonly List<Project>? ActiveProjects;

		public Me(MeResponse response)
		{
			this.ApiToken = response.api_token;
			this.Clients = response.clients?.ConvertAll(client => client.ToClient(this));
			this.DefaultWorkspaceId = response.default_workspace_id;
			this.Id = response.id;
			this.Projects = response.projects?.ConvertAll(project => project.ToProject(this));

			this.ActiveProjects = this.Projects?.FindAll(project => project.Active ?? false);
		}

		public Project? FindProject(long? id)
		{
			return this.Projects?.Find(project => project.Id == id);
		}

		public Client? FindClient(long? id)
		{
			return this.Clients?.Find(client => client.Id == id);
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

		public readonly bool? Active;
		public readonly int? ActualHours;
		public readonly long? ClientId;
		public readonly string? Colour;
		public readonly long Id;
		public readonly string Name;
		public readonly long WorkspaceId;

		public readonly Client? Client;

		public Project(ProjectResponse response, Me me)
		{
			this._me = me;

			this.Active = response.active;
			this.ActualHours = response.actual_hours;
			this.ClientId = response.client_id;
			this.Colour = response.color;
			this.Id = response.id;
			this.Name = response.name ?? "(no name)";
			this.WorkspaceId = response.workspace_id;

			this.Client = this._me.FindClient(this.ClientId);
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

		public readonly bool? Billable;
		public readonly string? RawDescription;
		public readonly long Duration;
		public readonly long Id;
		public readonly long? ProjectId;
		public readonly string Start;
		public readonly string? Stop;
		public readonly List<string>? Tags;
		public readonly long WorkspaceId;

		public readonly Project? Project;

		public TimeEntry(TimeEntryResponse response, Me me)
		{
			this._me = me;

			this.Billable = response.billable;
			this.RawDescription = response.description;
			this.Duration = response.duration;
			this.Id = response.id;
			this.ProjectId = response.project_id;
			this.Start = response.start ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz");
			this.Stop = response.stop;
			this.Tags = response.tags;
			this.WorkspaceId = response.workspace_id;

			this.Project = this._me.FindProject(this.ProjectId);
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

		public List<SummaryTimeEntryGroup> Groups;

		public SummaryTimeEntry(SummaryTimeEntryResponse response, Me me)
		{
			this._me = me;

			this.Groups = response.groups?.ConvertAll(group => group.ToSummaryTimeEntryGroup(me)) ?? new List<SummaryTimeEntryGroup>();
		}
		public SummaryTimeEntry(SummaryTimeEntry summary)
		{
			this._me = summary._me;

			this.Groups = summary.Groups.ConvertAll<SummaryTimeEntryGroup>(group => group.Clone());
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

			var group = clonedSummary.FindGroup(groupId);
			var subGroup = (subGrouping) switch
			{
				ENTRIES_KEY => group?.FindSubGroup(timeEntry.RawDescription),
				PROJECTS_KEY => group?.FindSubGroup(timeEntry.ProjectId),
				// Default sub-grouping of entries
				_ => group?.FindSubGroup(timeEntry.RawDescription),
			};

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
				group.SubGroups.Add(new SummaryTimeEntrySubGroup(newSubGroup));
				return clonedSummary;
			}

			if (group is not null)
			{
				group.SubGroups = new List<SummaryTimeEntrySubGroup>
				{
					new SummaryTimeEntrySubGroup(newSubGroup),
				};
				return clonedSummary;
			}

			clonedSummary.Groups.Add(new SummaryTimeEntryGroup(
				new SummaryTimeEntryGroupResponse
				{
					id = groupId,
					sub_groups = new List<SummaryTimeEntrySubGroupResponse>
					{
						newSubGroup,
					},
				},
				this._me
			));

			return clonedSummary;
		}

		public long Seconds
		{
			get => this.Groups.Sum(group => group.Seconds);
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

		public SummaryTimeEntryGroup? FindGroup(long? id)
		{
			return this.Groups.Find(group => group.Id == id);
		}
	}

	public class SummaryTimeEntryGroup : ICloneable
	{
		private readonly Me _me;

		public long? Id;
		public List<SummaryTimeEntrySubGroup>? SubGroups;

		public Project? Project;
		public Client? Client;

		public SummaryTimeEntryGroup(SummaryTimeEntryGroupResponse response, Me me)
		{
			this._me = me;

			this.Id = response.id;
			this.SubGroups = response.sub_groups?.ConvertAll(subGroup => subGroup.ToSummaryTimeEntrySubGroup());

			this.Project = me.FindProject(this.Id);
			this.Client = me.FindClient(this.Id);
		}
		public SummaryTimeEntryGroup(SummaryTimeEntryGroup group)
		{
			this._me = group._me;

			this.Id = group.Id;
			this.SubGroups = group.SubGroups?.ConvertAll<SummaryTimeEntrySubGroup>(subGroup => subGroup.Clone());

			this.Project = group.Project;
			this.Client = group.Client;
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
			get => this?.SubGroups?.Sum(subGroup => subGroup.Seconds) ?? 0;
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
			get => this.SubGroups?.MaxBy(subGroup => subGroup.Seconds);
		}

		public SummaryTimeEntrySubGroup? FindSubGroup(long? id)
		{
			return this.SubGroups?.Find(subGroup => subGroup.Id == id);
		}
		public SummaryTimeEntrySubGroup? FindSubGroup(string? title)
		{
			return this.SubGroups?.Find(subGroup => subGroup.RawTitle == title);
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