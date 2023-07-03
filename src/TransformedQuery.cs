using System;
using System.Text.RegularExpressions;
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

		public string[] SearchTerms;

		public static string EscapeDescription(string description)
		{
			string escaped = Regex.Replace(description, @"(\\(?!\\))", @"\\");
			return Regex.Replace(escaped, @" -", @" \-");
		}

		internal TransformedQuery(Query query)
		{
			this.SearchTerms = query.SearchTerms;
		}
		private TransformedQuery(string[] searchTerms)
		{
			this.SearchTerms = searchTerms;
		}

		public int IndexOf(string term)
		{
			return Array.IndexOf(this.SearchTerms, term);
		}

		private TransformedQuery Slice(int? start = null, int? end = null)
		{
			this.SearchTerms = this.SearchTerms[(start ?? 0)..(end ?? ^0)];
			return this;
		}

		public TransformedQuery To(int index)
		{
			return this.Slice(end: index);
		}

		public TransformedQuery To(string term)
		{
			return this.Slice(end: this.IndexOf(term));
		}

		public TransformedQuery After(int index)
		{
			return this.Slice(start: index);
		}

		public TransformedQuery After(string term)
		{
			return this.Slice(start: this.IndexOf(term) + 1);
		}

		public TransformedQuery Between(int after, int to)
		{
			return this.Slice(start: after, end: to);
		}

		public TransformedQuery Between(int start, string to)
		{
			return this.Slice(start: start, end: this.IndexOf(to));
		}

		public (TransformedQuery, TransformedQuery) Split(int index)
		{
			return (
				new TransformedQuery(this.SearchTerms[..index]),
				new TransformedQuery(this.SearchTerms[index..])
			);
		}

		public TransformedQuery RemoveAll(string term)
		{
			this.SearchTerms = this.SearchTerms.Where(searchTerm => searchTerm != term).ToArray();
			return this;
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

	internal static class TupleExtensions
	{
		public static (string, string) ToStrings(this (TransformedQuery queryOne, TransformedQuery queryTwo) tuple)
		{
			return (
				tuple.queryOne.ToString(),
				tuple.queryTwo.ToString()
			);
		}
	}
}