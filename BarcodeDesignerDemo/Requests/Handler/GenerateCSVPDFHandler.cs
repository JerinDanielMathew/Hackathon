using BarcodeDesignerDemo.Requests.Command;
using Newtonsoft.Json.Linq;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace BarcodeDesignerDemo.Requests.Handler
{
    public class GenerateCSVPDFHandler
    {
        public MemoryStream Handle(GenerateCSVDataTagPdfHandlerCommand command)
        {
            var json = JObject.Parse(command.TemplateJson);
            var fabric = json["fabric"];
            var elements = fabric?["elements"];
            var image = json["thumbnail"]?.ToString();

            var canvas = fabric?["canvas"];
            int dpi = canvas?["dpi"]?.Value<int>() ?? 300;

            double ToPixels(double mm) => mm * dpi / 25.4;

            // Page size (A4)
            const double pageWidthMm = 210;
            const double pageHeightMm = 297;
            double pageWidthPx = ToPixels(pageWidthMm);
            double pageHeightPx = ToPixels(pageHeightMm);

            // Template layout settings
            double templateWidthPx = 150;   // fixed template width (px)
            double templateHeightPx = 100;  // fixed template height (px)
            double marginPx = 10;           // gap between templates (px)

            var pdf = new PdfDocument();
            var imageStreams = new List<MemoryStream>();

            // Start first page
            var page = pdf.AddPage();
            page.Width = XUnit.FromMillimeter(pageWidthMm);
            page.Height = XUnit.FromMillimeter(pageHeightMm);
            var gfx = XGraphics.FromPdfPage(page);

            double cursorX = 0;
            double cursorY = 0;

            foreach (var row in command.Rows)
            {
                // Wrap to new page if vertical overflow
                if (cursorY + templateHeightPx > pageHeightPx)
                {
                    page = pdf.AddPage();
                    page.Width = XUnit.FromMillimeter(pageWidthMm);
                    page.Height = XUnit.FromMillimeter(pageHeightMm);
                    gfx = XGraphics.FromPdfPage(page);

                    cursorX = 0;
                    cursorY = 0;
                }

                // Wrap to next line if horizontal overflow BEFORE drawing
                if (cursorX + templateWidthPx > pageWidthPx)
                {
                    cursorX = 0;
                    cursorY += templateHeightPx + marginPx;
                }

                // Build dictionary of field values from CSV row
                var fieldValues = new Dictionary<string, string>();
                for (int i = 0; i < command.Header.Length && i < row.Length; i++)
                {
                    var val = row[i] ?? string.Empty;
                    if (val.Contains("{barcode}", StringComparison.OrdinalIgnoreCase))
                        val = val.Replace("{barcode}", string.Empty);

                    fieldValues[command.Header[i]] = val;
                }

                // Draw elements at current cursor
                foreach (var elem in elements.OrderBy(e => (int)e["z_index"]))
                {
                    DrawElement(gfx, elem, fieldValues, imageStreams, dpi, image, cursorX, cursorY);
                }

                // Advance cursor for next template
                cursorX += templateWidthPx + marginPx;
            

            // Wrap to new line if horizontal overflow
            if (cursorX + templateWidthPx > pageWidthPx)
                {
                    cursorX = 0;
                    cursorY += templateHeightPx + marginPx;
                }
            }

            // Save PDF
            var pdfStream = new MemoryStream();
            pdf.Save(pdfStream, false);
            pdfStream.Position = 0;

            // Dispose temp images
            foreach (var ms in imageStreams) ms.Dispose();

            return pdfStream;
        }



        private void DrawElement(
     XGraphics gfx,
     JToken elem,
     Dictionary<string, string> fieldValues,
     List<MemoryStream> imageStreams,
     int dpi,
     string image = null,
     double offsetX = 0,
     double offsetY = 0,
     double templateWidthPx = 150,   // fixed template width in pixels
     double templateHeightPx = 100,  // fixed template height in pixels
     double jsonCanvasWidthMm = 50,  // original JSON canvas width
     double jsonCanvasHeightMm = 30  // original JSON canvas height
 )
        {
            string type = elem.Value<string>("type");
            double x = elem.Value<double>("x_mm");
            double y = elem.Value<double>("y_mm");
            double width = elem.Value<double?>("width_mm") ?? 10;
            double height = elem.Value<double?>("height_mm") ?? 10;

            double ToPixels(double mm) => mm * dpi / 25.4;

            // Compute scale factors to fit template box
            double scaleX = templateWidthPx / ToPixels(jsonCanvasWidthMm);
            double scaleY = templateHeightPx / ToPixels(jsonCanvasHeightMm);

            // Apply offset + scaling to all coordinates
            double left = offsetX + ToPixels(x) * scaleX;
            double top = offsetY + ToPixels(y) * scaleY;
            double pw = ToPixels(width) * scaleX;
            double ph = ToPixels(height) * scaleY;

            var data = elem["additionalData"] ?? new JObject();

            switch (type)
            {
                case "text":
                    {
                        string text = elem.Value<string>("content") ?? "";

                        if (fieldValues != null && fieldValues.TryGetValue(text, out var val))
                            text = val;

                        double fontSize = (data.Value<double?>("fontSize") ?? 12) * scaleY;
                        string fontFamily = data.Value<string>("fontFamily") ?? "Calibri";
                        string fontWeight = data.Value<string>("fontWeight") ?? "normal";
                        string fontStyle = data.Value<string>("fontStyle") ?? "normal";
                        string color = data.Value<string>("color") ?? "#000000";

                        XFontStyle style = XFontStyle.Regular;
                        if (fontWeight.Equals("bold", StringComparison.OrdinalIgnoreCase))
                            style |= XFontStyle.Bold;
                        if (fontStyle.Equals("italic", StringComparison.OrdinalIgnoreCase))
                            style |= XFontStyle.Italic;

                        var font = new XFont(fontFamily, fontSize, style);
                        var brush = new XSolidBrush(ParseHexColor(color));

                        gfx.DrawString(text, font, brush,
                            new XRect(left, top, pw, ph),
                            XStringFormats.TopLeft);
                    }
                    break;

                case "line":
                    {
                        string strokeColor = data.Value<string>("strokeColor") ?? "#000000";
                        double strokeWidth = data.Value<double?>("strokeWidth") ?? 1;

                        var pen = new XPen(ParseHexColor(strokeColor), strokeWidth);

                        double x2 = left + pw;
                        double y2 = top; // horizontal line
                        gfx.DrawLine(pen, left, top, x2, y2);
                    }
                    break;

                case "border":
                case "rectangle":
                    {
                        string strokeColor = data.Value<string>("borderColor") ?? "#000000";
                        double strokeWidth = data.Value<double?>("borderWidth") ?? 1;
                        string fillColor = data.Value<string>("backgroundColor") ?? "transparent";

                        var pen = new XPen(ParseHexColor(strokeColor), strokeWidth);
                        var brush = fillColor.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                            ? XBrushes.Transparent
                            : new XSolidBrush(ParseHexColor(fillColor));

                        gfx.DrawRectangle(pen, brush, left, top, pw, ph);
                    }
                    break;

                case "circle":
                case "ellipse":
                    {
                        string strokeColor = data.Value<string>("borderColor") ?? "#000000";
                        double strokeWidth = data.Value<double?>("borderWidth") ?? 1;
                        string fillColor = data.Value<string>("backgroundColor") ?? "transparent";

                        var pen = new XPen(ParseHexColor(strokeColor), strokeWidth);
                        var brush = fillColor.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                            ? XBrushes.Transparent
                            : new XSolidBrush(ParseHexColor(fillColor));

                        gfx.DrawEllipse(pen, brush, left, top, pw, ph);
                    }
                    break;

                case "triangle":
                    {
                        string strokeColor = data.Value<string>("borderColor") ?? "#000000";
                        double strokeWidth = data.Value<double?>("borderWidth") ?? 1;
                        string fillColor = data.Value<string>("backgroundColor") ?? "transparent";

                        var pen = new XPen(ParseHexColor(strokeColor), strokeWidth);
                        var brush = fillColor.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                            ? XBrushes.Transparent
                            : new XSolidBrush(ParseHexColor(fillColor));

                        var points = new[]
                        {
                    new XPoint(left + pw/2, top),
                    new XPoint(left, top + ph),
                    new XPoint(left + pw, top + ph)
                };

                        gfx.DrawPolygon(pen, brush, points, XFillMode.Winding);
                    }
                    break;

                case "image":
                    {
                        string base64Image = elem.Value<string>("content");
                        if (string.IsNullOrWhiteSpace(base64Image) || base64Image.Contains("Click to upload"))
                            base64Image = image;

                        if (!string.IsNullOrWhiteSpace(base64Image) && base64Image.StartsWith("data:image"))
                        {
                            try
                            {
                                var base64Data = base64Image.Substring(base64Image.IndexOf(",") + 1);
                                byte[] imageBytes = Convert.FromBase64String(base64Data);
                                var ms = new MemoryStream(imageBytes);
                                var xImg = XImage.FromStream(() => ms);
                                imageStreams.Add(ms);

                                gfx.DrawImage(xImg, left, top, pw, ph);
                            }
                            catch
                            {
                                gfx.DrawRectangle(XPens.Red, left, top, pw, ph);
                                gfx.DrawString("Image error", new XFont("Arial", 8), XBrushes.Red,
                                    new XRect(left, top, pw, ph), XStringFormats.Center);
                            }
                        }
                        else
                        {
                            gfx.DrawRectangle(XPens.Gray, left, top, pw, ph);
                            gfx.DrawString("No image", new XFont("Arial", 8), XBrushes.Gray,
                                new XRect(left, top, pw, ph), XStringFormats.Center);
                        }
                    }
                    break;

                case "barcode":
                    {
                        string text = elem.Value<string>("content") ?? "";

                        if (fieldValues != null && text.StartsWith("{") && text.EndsWith("}"))
                        {
                            string key = text.Trim('{', '}');  // remove braces
                            if (fieldValues.TryGetValue(key, out var val))
                                text = val;                     // replace with CSV value
                        }
                        string barcodeType = data.Value<string>("barcodeType") ?? "code128";

                        DrawBarcode(gfx, text, barcodeType, left, top, pw, ph, imageStreams);
                    }
                    break;
            }
        }


        private static XColor ParseHexColor(string color)
        {
            if (string.IsNullOrEmpty(color))
                return XColors.Transparent;

            // Handle common named colors
            switch (color.ToLower())
            {
                case "white": return XColors.White;
                case "black": return XColors.Black;
                case "red": return XColors.Red;
                case "blue": return XColors.Blue;
                case "green": return XColors.Green;
                case "yellow": return XColors.Yellow;
                case "transparent": return XColors.Transparent;
            }

            if (color.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Extract numbers
                    var nums = color
                        .Replace("rgb(", "")
                        .Replace(")", "")
                        .Split(',')
                        .Select(s => byte.Parse(s.Trim()))
                        .ToArray();

                    if (nums.Length == 3)
                    {
                        return XColor.FromArgb(nums[0], nums[1], nums[2]);
                    }
                }
                catch
                {
                    return XColors.MintCream; // fallback
                }
            }

            // Handle hex (#RRGGBB or #AARRGGBB)
            if (color.StartsWith("#"))
            {
                color = color.TrimStart('#');

                if (color.Length == 6) // RRGGBB
                {
                    var r = Convert.ToByte(color.Substring(0, 2), 16);
                    var g = Convert.ToByte(color.Substring(2, 2), 16);
                    var b = Convert.ToByte(color.Substring(4, 2), 16);
                    return XColor.FromArgb(r, g, b);
                }
                else if (color.Length == 8) // AARRGGBB
                {
                    var a = Convert.ToByte(color.Substring(0, 2), 16);
                    var r = Convert.ToByte(color.Substring(2, 2), 16);
                    var g = Convert.ToByte(color.Substring(4, 2), 16);
                    var b = Convert.ToByte(color.Substring(6, 2), 16);
                    return XColor.FromArgb(a, r, g, b);
                }
            }

            return XColors.Black; // fallback
        }

        private void DrawBarcode(
            XGraphics gfx,
            string content,
            string barcodeType,
            double left,
            double top,
            double width,
            double height,
            List<MemoryStream> imageStreams)
        {
            BarcodeFormat format = BarcodeFormat.CODE_128;
            switch (barcodeType.ToLower())
            {
                case "ean13": format = BarcodeFormat.EAN_13; break;
                case "qr": format = BarcodeFormat.QR_CODE; break;
                case "code39": format = BarcodeFormat.CODE_39; break;
                case "upca": format = BarcodeFormat.UPC_A; break;
                case "upce": format = BarcodeFormat.UPC_E; break;
            }

            var writer = new BarcodeWriterPixelData
            {
                Format = format,
                Options = new EncodingOptions
                {
                    Width = (int)Math.Max(width, 1),
                    Height = (int)Math.Max(height, 1),
                    Margin = 0,
                    PureBarcode = true
                }
            };

            try
            {
                var pixelData = writer.Write(content);

                using (var bmp = new SKBitmap(new SKImageInfo(pixelData.Width, pixelData.Height)))
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmp.GetPixels(), pixelData.Pixels.Length);

                    using (var image = SKImage.FromBitmap(bmp))
                    using (var ms = new MemoryStream())
                    {
                        image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
                        ms.Position = 0;

                        imageStreams.Add(ms); // Keep reference alive
                        var xImage = XImage.FromStream(() => ms);
                        gfx.DrawImage(xImage, left, top, width, height);

                        // ✅ Only draw human-readable text if not QR
                        if (!string.Equals(barcodeType, "qr", StringComparison.OrdinalIgnoreCase))
                        {
                            var font = new XFont("Arial", 2, XFontStyle.Regular);
                            var textHeight = font.GetHeight();
                            gfx.DrawString(content, font, XBrushes.Black,
                                new XRect(left, top + height + 2, width, textHeight),
                                XStringFormats.TopCenter);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback if barcode fails
                gfx.DrawRectangle(XPens.Red, left, top, width, height);
                gfx.DrawString("Barcode Err", new XFont("Arial", 8), XBrushes.Red,
                    new XRect(left, top, width, height), XStringFormats.Center);
            }
        }


    }
}
