using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;

namespace Flow.Launcher.Plugin.TogglTrack
{
	public class ColourIconProvider
	{
		private static readonly int IMAGE_SIZE = 32;
		private static readonly int CIRCLE_SIZE = 20;

		private PluginInitContext _context { get; set; }
		private DirectoryInfo? _coloursDirectory { get; set; }

		public ColourIconProvider(PluginInitContext context)
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
			using (var bitmap = new Bitmap(ColourIconProvider.IMAGE_SIZE, ColourIconProvider.IMAGE_SIZE))
			using (var graphics = Graphics.FromImage(bitmap))
			{
				var colour = ColorTranslator.FromHtml(colourCode);
				graphics.Clear(Color.Transparent);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;

				int centre = (ColourIconProvider.IMAGE_SIZE - ColourIconProvider.CIRCLE_SIZE) / 2;
				graphics.FillEllipse(new SolidBrush(colour), centre, centre, ColourIconProvider.CIRCLE_SIZE, ColourIconProvider.CIRCLE_SIZE);

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