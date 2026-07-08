using System;
using System.Drawing;
using System.Linq;
using TED.Program;
using TED.Utils;

namespace TED.DrawModes
{
    /// <summary>
    /// Draws the TED desktop tag onto an existing graphics surface.
    /// </summary>
    internal static class DesktopTagRenderer
    {
        internal static void Draw(Graphics graphics, Options options)
        {
            var wallpaperLuminance = ImageUtilities.CalculateWallpaperLuminance();
            var primaryAreaRect = SystemUtilities.GetPrimaryScreenRect();
            var primaryWorkingArea = SystemUtilities.GetPrimaryScreenWorkingArea();
            var imagePath = options.GetImagePath(wallpaperLuminance);
            var textColor = wallpaperLuminance > 0.5 ? Color.Black : Color.White;

            using (var font = new Font(options.FontName, options.FontSize, FontStyle.Regular))
            {
                var formattedLines = options.Lines.Select(RichTextInlineParser.Parse).ToList();
                var scaleX = graphics.DpiX / 96.0f;
                var scaleY = graphics.DpiY / 96.0f;
                var scaledWorkingAreaWidth = primaryAreaRect.X / scaleX;
                var scaledWorkingAreaHeight = primaryAreaRect.Y / scaleY;

                float maxWidth;
                if (options.FixedWidth > 0)
                {
                    maxWidth = options.FixedWidth;
                }
                else
                {
                    maxWidth = formattedLines.Select(line => MeasureFormattedLine(graphics, line, font).Width)
                                             .DefaultIfEmpty(0)
                                             .Max();
                }

                var lineHeights = formattedLines.Select(line => MeasureFormattedLine(graphics, line, font).Height).ToList();
                var textBlockHeight = lineHeights.Sum() + (options.LineSpacing * Math.Max(options.Lines.Count - 1, 0));

                var rightEdge = scaledWorkingAreaWidth + primaryWorkingArea.Width;
                var bottomEdge = scaledWorkingAreaHeight + primaryWorkingArea.Height;

                // The image and the text share a single layout cell (the "group") instead of
                // stacking vertically: the logo becomes a backdrop rendered behind the text.
                // The group is as wide as the resolved width (-w) and as tall as whichever
                // element is taller, and it is anchored to the bottom-right corner using the
                // horizontal/vertical padding (-hp/-vp) so the logo and text move as one block.
                var groupWidth = maxWidth;
                var groupHeight = textBlockHeight;
                var groupLeft = rightEdge - groupWidth - options.PaddingHorizontal;
                var groupTop = bottomEdge - groupHeight - options.PaddingVertical;

                if (!string.IsNullOrEmpty(imagePath))
                {
                    using (var overlayImage = Image.FromFile(imagePath))
                    {
                        ImageUtilities.ScaleImageAndMaintainAspectRatio(overlayImage.Width, overlayImage.Height, maxWidth, int.MaxValue, out int newWidth, out int newHeight);

                        // Grow the group to contain the logo when it is taller than the text,
                        // then re-anchor so the merged block still hugs the bottom-right corner.
                        groupHeight = Math.Max(newHeight, textBlockHeight);
                        groupTop = bottomEdge - groupHeight - options.PaddingVertical;

                        // Bottom layer: center the logo within the group so it backs the text.
                        var imageX = groupLeft + (groupWidth - newWidth) / 2f;
                        var imageY = groupTop + (groupHeight - newHeight) / 2f;

                        graphics.DrawImage(overlayImage, new RectangleF(imageX, imageY, newWidth, newHeight));
                    }
                }

                // Top layer: the text is drawn last, sharing the group's origin so it renders
                // directly over the logo backdrop and stays readable.
                var textX = groupLeft;
                var textY = groupTop;

                for (var i = 0; i < options.Lines.Count; i++)
                {
                    DrawFormattedLine(graphics, formattedLines[i], font, textColor, textX, textY, maxWidth, options.TextAlignment);

                    textY += lineHeights[i];

                    if (i < options.Lines.Count)
                    {
                        textY += options.LineSpacing;
                    }
                }
            }
        }

        private static SizeF MeasureFormattedLine(Graphics graphics, FormattedLine line, Font baseFont)
        {
            var width = 0f;
            var height = graphics.MeasureString(string.Empty, baseFont, int.MaxValue, StringFormat.GenericTypographic).Height;

            foreach (var run in line.Runs)
            {
                using (var runFont = CreateRunFont(baseFont, run))
                using (var format = CreateStringFormat())
                {
                    var size = graphics.MeasureString(run.Text, runFont, int.MaxValue, format);
                    width += size.Width;
                    height = Math.Max(height, size.Height);
                }
            }

            return new SizeF(width, height);
        }

        private static void DrawFormattedLine(Graphics graphics, FormattedLine line, Font baseFont, Color defaultTextColor, float x, float y, float maxWidth, StringAlignment alignment)
        {
            var lineSize = MeasureFormattedLine(graphics, line, baseFont);
            var runX = x + GetAlignedOffset(maxWidth, lineSize.Width, alignment);

            foreach (var run in line.Runs)
            {
                using (var runFont = CreateRunFont(baseFont, run))
                using (var brush = new SolidBrush(run.Color ?? defaultTextColor))
                using (var format = CreateStringFormat())
                {
                    graphics.DrawString(run.Text, runFont, brush, new PointF(runX, y), format);
                    runX += graphics.MeasureString(run.Text, runFont, int.MaxValue, format).Width;
                }
            }
        }

        private static Font CreateRunFont(Font baseFont, TextRun run)
        {
            var style = baseFont.Style;
            if (run.Bold)
            {
                style |= FontStyle.Bold;
            }

            if (run.Italic)
            {
                style |= FontStyle.Italic;
            }

            if (run.Underline)
            {
                style |= FontStyle.Underline;
            }

            return new Font(baseFont.FontFamily, baseFont.Size, style, baseFont.Unit);
        }

        private static float GetAlignedOffset(float maxWidth, float lineWidth, StringAlignment alignment)
        {
            switch (alignment)
            {
                case StringAlignment.Center:
                    return (maxWidth - lineWidth) / 2;
                case StringAlignment.Far:
                    return maxWidth - lineWidth;
                default:
                    return 0;
            }
        }

        private static StringFormat CreateStringFormat()
        {
            var format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            return format;
        }
    }
}
