using System;
using System.Runtime.Caching;

namespace Flow.Launcher.Plugin.TogglTrack
{
	internal class NullableCache
	{
		private readonly static object NullObject = new object();
		private MemoryCache _cache = new MemoryCache("TogglTrack");

		internal bool Contains(string key)
		{
			return this._cache.Contains(key);
		}

		internal object? Get(string key)
		{
			var retrieved = this._cache.Get(key);

			return (retrieved == NullableCache.NullObject)
				? null
				: retrieved;
		}

		internal void Set(string key, object? value, DateTimeOffset absoluteExpiration)
		{
			var set = (value is null)
				? NullableCache.NullObject
				: value;

			this._cache.Set(key, set, absoluteExpiration);
		}

		internal object Remove(string key)
		{
			return this._cache.Remove(key);
		}

		internal void Clear()
		{
			this._cache.Dispose();
			this._cache = new MemoryCache("TogglTrack");
		}
	}
}