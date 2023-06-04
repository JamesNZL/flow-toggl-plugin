// * From https://stackoverflow.com/a/25933213

using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.TogglTrack
{
	public class InternetAvailability
	{
		[DllImport("wininet.dll")]
		private extern static bool InternetGetConnectedState(out int description, int reservedValue);

		public static bool IsInternetAvailable()
		{
			int description;
			return InternetGetConnectedState(out description, 0);
		}
	}
}