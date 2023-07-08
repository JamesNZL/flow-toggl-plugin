using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;

namespace Flow.Launcher.Plugin.TogglTrack
{
	public class IconProvider
	{
		private static readonly int IMAGE_SIZE = 32;
		private static readonly int CIRCLE_SIZE = 20;

		private PluginInitContext _context { get; set; }
		private DirectoryInfo? _coloursDirectory { get; set; }

		internal const string UsageTipIcon = "tip.png";
		internal const string UsageExampleIcon = IconProvider.UsageTipIcon;
		internal const string UsageWarningIcon = "tip-warning.png";
		internal const string UsageErrorIcon = "tip-error.png";

		internal const string StartIcon = "start.png";
		internal const string ContinueIcon = "continue.png";
		internal const string StopIcon = "stop.png";
		internal const string EditIcon = "edit.png";
		internal const string DeleteIcon = "delete.png";
		internal const string ReportsIcon = "reports.png";
		internal const string BrowserIcon = "browser.png";
		internal const string HelpIcon = IconProvider.UsageTipIcon;
		internal const string RefreshIcon = "refresh.png";

		public IconProvider(PluginInitContext context)
		{
			this._context = context;

			var iconsDirectoryPath = Path.Combine(this._context.CurrentPluginMetadata.PluginDirectory, "ColourIcons");
			if (!Directory.Exists(iconsDirectoryPath))
			{
				this._coloursDirectory = Directory.CreateDirectory(iconsDirectoryPath);
			}
			else
			{
				this._coloursDirectory = new DirectoryInfo(iconsDirectoryPath);
			}
		}

		private string CreateCacheImage(string path, string colourCode)
		{
			using (var bitmap = new Bitmap(IconProvider.IMAGE_SIZE, IconProvider.IMAGE_SIZE))
			using (var graphics = Graphics.FromImage(bitmap))
			{
				var colour = ColorTranslator.FromHtml(colourCode);
				graphics.Clear(Color.Transparent);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;

				int centre = (IconProvider.IMAGE_SIZE - IconProvider.CIRCLE_SIZE) / 2;
				graphics.FillEllipse(new SolidBrush(colour), centre, centre, IconProvider.CIRCLE_SIZE, IconProvider.CIRCLE_SIZE);

				bitmap.Save(path, ImageFormat.Png);
				return path;
			}
		}

		public string GetColourIcon(string? colourCode, string fallbackIcon)
		{
			if (string.IsNullOrWhiteSpace(colourCode))
			{
				return fallbackIcon;
			}

			var path = Path.Combine(this._coloursDirectory?.FullName ?? string.Empty, $"{colourCode}.png");

			try
			{
				return (File.Exists(path))
					? path
					: CreateCacheImage(path, colourCode);
			}
			catch
			{
				// Check whether file was actually created or not
				return (File.Exists(path))
					? path
					: fallbackIcon;
			}
		}
	}
}