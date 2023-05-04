using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;

namespace Flow.Launcher.Plugin.TogglTrack
{
    internal class ColourIcon
    {
        private static readonly int IMAGE_SIZE = 32;
		private static readonly int CIRCLE_SIZE = 20;
		private static DirectoryInfo? _coloursDirectory { get; set; }

		private PluginInitContext _context { get; set; }

		private string _colourCode;
		private string _fallbackIcon;

		internal ColourIcon(PluginInitContext context, string colourCode, string fallbackIcon)
		{
			this._context = context;
			this._colourCode = colourCode;
			this._fallbackIcon = fallbackIcon;

			if (ColourIcon._coloursDirectory is not null)
            {
				return;
			}

			var imageCacheDirectoryPath = Path.Combine(this._context.CurrentPluginMetadata.PluginDirectory, "CachedImages");

            if (!Directory.Exists(imageCacheDirectoryPath))
            {
                ColourIcon._coloursDirectory = Directory.CreateDirectory(imageCacheDirectoryPath);
            }
            else
            {
                ColourIcon._coloursDirectory = new DirectoryInfo(imageCacheDirectoryPath);
            }
		}

		private string CreateCacheImage(string path)
        {
            using (var bitmap = new Bitmap(ColourIcon.IMAGE_SIZE, ColourIcon.IMAGE_SIZE))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var colour = ColorTranslator.FromHtml(this._colourCode);
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                int centre = (ColourIcon.IMAGE_SIZE - ColourIcon.CIRCLE_SIZE) / 2;
                graphics.FillEllipse(new SolidBrush(colour), centre, centre, ColourIcon.CIRCLE_SIZE, ColourIcon.CIRCLE_SIZE);

                bitmap.Save(path, ImageFormat.Png);
                return path;
            }
        }

		internal string GetColourIcon()
		{
            try
            {
                var path = Path.Combine(ColourIcon._coloursDirectory?.FullName ?? string.Empty, $"{this._colourCode}.png");

				return (File.Exists(path))
					? path
                    : CreateCacheImage(path);
			}
            catch
            {
				return this._fallbackIcon;
			}
		}
    }
}