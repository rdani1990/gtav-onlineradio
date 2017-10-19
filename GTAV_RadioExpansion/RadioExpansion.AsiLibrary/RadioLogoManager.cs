using RadioExpansion.Core;
using RadioExpansion.Core.RadioPlayers;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text;

namespace RadioExpansion.AsiLibrary
{
    public static class RadioLogoManager
    {
        private const float BORDER_WIDTH = 8;
        private const int FINAL_SIZE = 144;
        private const int ORIGINAL_SIZE = 128;
        private const string TMP_DIR = "GTAV_RadioExpansion";

        private static readonly Color HudColor_Michael = Color.FromArgb(101, 180, 212); // blue
        private static readonly Color HudColor_Trevor = Color.FromArgb(255, 163, 87); // orange
        private static readonly Color HudColor_Franklin = Color.FromArgb(171, 237, 171); // green

        /// <summary>
        /// Creates a version of the image similar to the radio logos in GTA V: within a circle, with a blue/green/orange border.
        /// </summary>
        private static void TransformImage(string from, string to, Color outlineColor)
        {
            var img = Image.FromFile(from);
            var bmp = new Bitmap(FINAL_SIZE, FINAL_SIZE);
            var g = Graphics.FromImage(bmp);
            var clipPath = new GraphicsPath();
            int padding = (FINAL_SIZE - ORIGINAL_SIZE) / 2;
            
            clipPath.AddEllipse(padding, padding, ORIGINAL_SIZE, ORIGINAL_SIZE); // only the inside of the ellipse will be shown

            g.SmoothingMode = SmoothingMode.HighQuality;
            g.SetClip(clipPath, CombineMode.Replace);
            g.DrawImage(img, padding, padding); // draw the image inside the selected ellipse area
            g.ResetClip();
            g.DrawEllipse(new Pen(outlineColor, BORDER_WIDTH), padding, padding, ORIGINAL_SIZE, ORIGINAL_SIZE); // draw the border
            g.Save();

            string dir = Path.GetDirectoryName(to);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bmp.Save(to);
        }

        /// <summary>
        /// Creates a version of the image similar to the radio logos in GTA V: within a circle, with a blue/green/orange border.
        /// </summary>
        private static void TransformImage(Image imgFrom, string to, Color outlineColor)
        {
            var bmp = new Bitmap(FINAL_SIZE, FINAL_SIZE);
            var g = Graphics.FromImage(bmp);
            var clipPath = new GraphicsPath();
            int padding = (FINAL_SIZE - ORIGINAL_SIZE) / 2;

            clipPath.AddEllipse(padding, padding, ORIGINAL_SIZE, ORIGINAL_SIZE); // only the inside of the ellipse will be shown

            g.SmoothingMode = SmoothingMode.HighQuality;
            g.SetClip(clipPath, CombineMode.Replace);
            g.DrawImage(imgFrom, padding + Math.Min(ORIGINAL_SIZE - imgFrom.Width, 0), padding + Math.Min(ORIGINAL_SIZE - imgFrom.Height, 0)); // draw the image inside the selected ellipse area
            g.ResetClip();
            g.DrawEllipse(new Pen(outlineColor, BORDER_WIDTH), padding, padding, ORIGINAL_SIZE, ORIGINAL_SIZE); // draw the border
            g.Save();

            string dir = Path.GetDirectoryName(to);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bmp.Save(to);
        }

        /// <summary>
        /// Create outlined versions of logos with for each hud color into a temporary folder.
        /// </summary>
        public static void CreateTempLogos(Radio[] radios)
        {
            var colors = new[]
            {
                HudColor_Trevor,
                HudColor_Michael,
                HudColor_Franklin
            };

            foreach (var radio in radios)
            {
                foreach (var color in colors)
                {
                    string logoPath = Path.Combine(radio.Folder, "logo.jpg");
                    if (File.Exists(logoPath))
                    {
                        TransformImage(Image.FromFile(logoPath), GetTempLogoPath(radio.Name, color), color);
                    }
                    else
                    {
                        Logger.Instance.Log($"Looking for logo '{logoPath}'... File not found.");

                        var scriptAssembly = typeof(RadioLogoManager).Assembly;
                        using (var stream = scriptAssembly.GetManifestResourceStream($"{scriptAssembly.GetName().Name}.Logo_UnknownRadio.jpg"))
                        {
                            TransformImage(Image.FromStream(stream), GetTempLogoPath(radio.Name, color), color);
                        }
                    }
                }
            }
        }

        private static string GetTempLogoPath(string radioName, Color hudColor)
        {
            return Path.Combine(Path.GetTempPath(), TMP_DIR, String.Format("{0}_{1}.png", Convert.ToBase64String(Encoding.UTF8.GetBytes(radioName)), hudColor.Name));
        }

        public static string GetTempLogoPathForMichael(string radioName)
        {
            return GetTempLogoPath(radioName, HudColor_Michael);
        }

        public static string GetTempLogoPathForFranklin(string radioName)
        {
            return GetTempLogoPath(radioName, HudColor_Franklin);
        }

        public static string GetTempLogoPathForTrevor(string radioName)
        {
            return GetTempLogoPath(radioName, HudColor_Trevor);
        }

        public static void Cleanup()
        {
            foreach (string file in Directory.GetFiles(Path.GetTempPath(), TMP_DIR))
            {
                File.Delete(file);
            }
        }
    }
}
