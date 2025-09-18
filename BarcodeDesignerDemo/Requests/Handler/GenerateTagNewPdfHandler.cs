using BarcodeDesignerDemo.Requests.Command;
using DocumentFormat.OpenXml.Spreadsheet;
using iTextSharp.text;
using Newtonsoft.Json.Linq;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;
using ZXing.PDF417.Internal;

namespace BarcodeDesignerDemo.Requests.Handler
{
    public class GenerateTagNewPdfHandler
    {
        double MmToPoints(double mm) => mm * 72.0 / 25.4;
        double MmToPixels(double mm, double dpi) => mm * dpi / 25.4;
        public MemoryStream Handle(GenerateTagNewPdfHandlerCommand command)
        {
            var json = JObject.Parse(command.TemplateJson);

            // Navigate into new structure
            //var fabric = json["fabric"];
            var elements = json["elements"];
            var image = json["thumbnail"]?.ToString();
            // Extract canvas size
            double widthMm = json["canvas"]?["width_mm"]?.Value<double>() ?? 210;
            double heightMm = json["canvas"]?["height_mm"]?.Value<double>() ?? 297;
            int dpi = json["canvas"]?["dpi"]?.Value<int>() ?? 300;
            double widthPx = widthMm * dpi / 25.4;
            double heightPx = heightMm * dpi / 25.4;
            // Create PDF page with those dimensions
            var pdf = new PdfDocument();
            var page = pdf.AddPage();
            page.Width = XUnit.FromMillimeter(widthPx);
            page.Height = XUnit.FromMillimeter(heightPx);

            var gfx = XGraphics.FromPdfPage(page);
            var imageStreams = new List<MemoryStream>();

            foreach (var elem in elements.OrderBy(e => (int)e["z_index"]))
            {
                
                DrawElement(gfx, elem, command.FieldValues, imageStreams, dpi, image);
            }

            var pdfStream = new MemoryStream();
            pdf.Save(pdfStream, false);
            pdfStream.Position = 0;

            foreach (var ms in imageStreams) ms.Dispose();

            return pdfStream;
        }

        private void DrawElement(
    XGraphics gfx,
    JToken elem,
    Dictionary<string, string> fieldValues,
    List<MemoryStream> imageStreams,
    int dpi, string image= null)
        {
            string type = elem.Value<string>("type");
            double x = elem.Value<double>("x_mm");
            double y = elem.Value<double>("y_mm");
            double width = elem.Value<double?>("width_mm") ?? 10;
            double height = elem.Value<double?>("height_mm") ?? 10;
            //double dpi = dpi; // your DPI

            // Convert mm → pixels
            double toPixels(double mm) => mm * dpi / 25.4;

            double left = toPixels(x);
            double top = toPixels(y);
            double pw = toPixels(width);
            double ph = toPixels(height);

            var data = elem["additionalData"] ?? new JObject();

            switch (type)
            {
                case "text":
                    {
                        string text = elem.Value<string>("content") ?? "";
                        //if (fieldValues != null && text.StartsWith("{") && text.EndsWith("}"))
                        //{
                        //    string key = text.Trim('{', '}');
                        //    if (fieldValues.TryGetValue(key, out var val))
                        //        text = val;
                        //}

                        double fontSize = data.Value<double?>("fontSize") ?? 12;
                        string fontFamily = data.Value<string>("fontFamily") ?? "Calibre";
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
                        gfx.DrawLine(pen, left, top, left + pw, top);
                    }
                    break;

                case "border":
                    {
                        string borderColor = data.Value<string>("borderColor") ?? "#000000";
                        double borderWidth = data.Value<double?>("borderWidth") ?? 1;
                        string bg = "#FFFFFF";
                            //data.Value<string>("backgroundColor") ?? "transparent";

                        var pen = new XPen(ParseHexColor(borderColor), borderWidth);
                        var brush = bg == "transparent"
                            ? XBrushes.Transparent
                            : new XSolidBrush(ParseHexColor(bg));

                        gfx.DrawRectangle(pen, brush, left, top, pw, ph);
                    }
                    break;

                case "image":
                    {
                        string base64Image = elem.Value<string>("content");

                        // Fallback to "thumbnail" at the root of the JSON
                        if (string.IsNullOrWhiteSpace(base64Image) || base64Image.Contains("Click to upload"))
                        {
                            base64Image = image;
                        }

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
                            catch (Exception ex)
                            {
                                gfx.DrawRectangle(XPens.Red, left, top, pw, ph);
                                gfx.DrawString("Image error", new XFont("Arial", 8), XBrushes.Red,
                                    new XRect(left, top, pw, ph), XStringFormats.Center);
                            }
                        }
                        else
                        {
                            // Draw placeholder if no image
                            gfx.DrawRectangle(XPens.Gray, left, top, pw, ph);
                            gfx.DrawString("No image", new XFont("Arial", 8), XBrushes.Gray,
                                new XRect(left, top, pw, ph), XStringFormats.Center);
                        }
                    }
                    break;


                case "barcode":
                    {
                        string barcodeType = data.Value<string>("barcodeType") ?? "code128";
                        string content = elem.Value<string>("content") ?? "123456";

                        //if (fieldValues != null && content.StartsWith("{") && content.EndsWith("}"))
                        //{
                        //    string key = content.Trim('{', '}');
                        //    if (fieldValues.TryGetValue(key, out var val))
                        //        content = val;
                        //}

                        DrawBarcode(gfx, content, barcodeType, left, top, pw, ph, imageStreams);
                    }
                    break;
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
                    {
                        string strokeColor = data.Value<string>("borderColor") ?? "#000000";
                        double strokeWidth = data.Value<double?>("borderWidth") ?? 1;
                        string fillColor = data.Value<string>("backgroundColor") ?? "transparent";

                        var pen = new XPen(ParseHexColor(strokeColor), strokeWidth);
                        var brush = fillColor.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                            ? XBrushes.Transparent
                            : new XSolidBrush(ParseHexColor(fillColor));

                        // Circle is just an ellipse with equal width and height
                        double size = Math.Min(pw, ph);
                        gfx.DrawEllipse(pen, brush, left, top, size, size);
                    }
                    break;

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

                        // Define 3 points of a triangle (equilateral-ish)
                        var points = new[]
                        {
                new XPoint(left + pw / 2, top),            // top center
                new XPoint(left, top + ph),                // bottom left
                new XPoint(left + pw, top + ph)            // bottom right
            };

                        gfx.DrawPolygon(pen, brush, points, XFillMode.Winding);
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
                            var font = new XFont("Arial", 8, XFontStyle.Regular);
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
