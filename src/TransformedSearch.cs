using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.TogglTrack
{
	internal class TransformedQuery
	{
		internal enum Escaping
		{
			Raw,
			Unescaped,
			Escaped,
		};

		public IEnumerable<string> SearchTerms;

		public static string EscapeDescription(string description)
		{
			string escaped = Regex.Replace(description, @"(\\(?!\\))", @"\\");
			return Regex.Replace(escaped, @" -", @" \-");
		}

		internal TransformedQuery(Query query)
		{
			this.SearchTerms = query.SearchTerms;
		}

		public int IndexOf(string value)
		{
			// TODO: #83
			return Array.IndexOf(this.SearchTerms.ToArray(), value);
		}

		public TransformedQuery To(int index)
		{
			this.SearchTerms = this.SearchTerms.Take(index);
			return this;
		}

		public TransformedQuery To(string value)
		{
			return this.To(this.IndexOf(value));
		}

		public TransformedQuery After(int index)
		{
			this.SearchTerms = this.SearchTerms.Skip(index);
			return this;
		}

		public TransformedQuery After(string value)
		{
			return this.After(this.IndexOf(value) + 1);
		}

		public TransformedQuery Between(int after, int to)
		{
			this.SearchTerms = this.SearchTerms.Take(to).Skip(after);
			return this;
		}

		public TransformedQuery Between(int after, string to)
		{
			return this.Between(after, this.IndexOf(to));
		}

		private string UnescapeSearch()
		{
			return Regex.Replace(this.ToString(), @"(\\(?!\\))", string.Empty);
		}

		public override string ToString()
		{
			return string.Join(' ', this.SearchTerms);
		}

		public string ToString(TransformedQuery.Escaping escaping)
		{
			return (escaping) switch
			{
				TransformedQuery.Escaping.Raw => this.ToString(),
				TransformedQuery.Escaping.Unescaped => this.UnescapeSearch(),
				TransformedQuery.Escaping.Escaped => TransformedQuery.EscapeDescription(this.ToString()),
				_ => this.ToString(),
			};
		}
	}
}