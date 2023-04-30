using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.TogglTrack
{
    internal class ColourIcon
    {
        private static readonly int IMAGE_SIZE = 32;
		private static readonly int CIRCLE_SIZE = 20;
		private static DirectoryInfo? _coloursDirectory { get; set; }

		private PluginInitContext _context { get; set; }

		private string _colourCode;

		internal ColourIcon(PluginInitContext context, string colourCode)
		{
			this._context = context;
			this._colourCode = colourCode;

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

        private static FileInfo? FindFileImage(string name)
        {
            var file = $"{name}.png";
            return ColourIcon._coloursDirectory?.GetFiles(file, SearchOption.TopDirectoryOnly).FirstOrDefault();
        }

		private static string CreateCacheImage(string name)
        {
            using (var bitmap = new Bitmap(ColourIcon.IMAGE_SIZE, ColourIcon.IMAGE_SIZE))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var colour = ColorTranslator.FromHtml(name);
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                int centre = (ColourIcon.IMAGE_SIZE - ColourIcon.CIRCLE_SIZE) / 2;
                graphics.FillEllipse(new SolidBrush(colour), centre, centre, ColourIcon.CIRCLE_SIZE, ColourIcon.CIRCLE_SIZE);

                var path = Path.Combine(ColourIcon._coloursDirectory?.FullName ?? string.Empty, $"{name}.png");
                bitmap.Save(path, ImageFormat.Png);
                return path;
            }
        }

		internal string GetColourIcon()
		{
			var cached = FindFileImage(this._colourCode);

			return (cached is null)
				? CreateCacheImage(this._colourCode)
				: cached.FullName;
		}
    }
}