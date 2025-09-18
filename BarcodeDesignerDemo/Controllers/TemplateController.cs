using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using ZXing;


[ApiController]
[Route("[controller]")]
//Not using this CONTROLLER
public class TemplateController : ControllerBase
{
    [HttpPost("create-template-pdf2")]
    public IActionResult CreateTemplatePdf([FromBody] TemplateRequest request)
    {
        try
        {
            var json = JObject.Parse(request.TemplateJson);
            var objects = json["objects"];

            var pdf = new PdfDocument();
            var page = pdf.AddPage();
            page.Width = XUnit.FromMillimeter(210);
            page.Height = XUnit.FromMillimeter(297);

            var gfx = XGraphics.FromPdfPage(page);

            var imageStreams = new List<MemoryStream>();

            // Draw template objects
            foreach (var obj in objects)
            {
                //DrawObject(gfx, obj, request.FieldValues, imageStreams);
            }

            //// ✅ Handle logo from base64 string
            //if (request.FieldValues != null && request.FieldValues.TryGetValue("LogoImage", out var base64Logo))
            //{
            //    var imageBytes = Convert.FromBase64String(base64Logo);
            //    using var ms = new MemoryStream(imageBytes);
            //    var xImage = XImage.FromStream(() => new MemoryStream(imageBytes));

            //    // Example placement (top-left corner, 100x100)
            //    gfx.DrawImage(xImage, 50, 50, 100, 100);
            //}

            using var pdfStream = new MemoryStream();
            pdf.Save(pdfStream, false);
            pdfStream.Position = 0;

            foreach (var ms in imageStreams)
                ms.Dispose();

            return File(pdfStream.ToArray(), "application/pdf", "PriceTag.pdf");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }


    private void DrawObject(XGraphics gfx, JToken obj, Dictionary<string, string> fieldValues, List<MemoryStream> imageStreams)
    {
        string type = obj.Value<string>("type");
        float left = obj.Value<float>("left");
        float top = obj.Value<float>("top");
        float width = obj.Value<float?>("width") ?? 100;
        float height = obj.Value<float?>("height") ?? 50;

        switch (type)
        {
            case "rect":
                {
                    // Extract style from JSON
                    string fillColor = obj.Value<string>("fill") ?? "#ffffff";
                    string strokeColor = obj.Value<string>("stroke") ?? "#000000";
                    double strokeWidth = obj.Value<double?>("strokeWidth") ?? 1;

                    // Convert hex to XColor safely
                    var fillXColor = string.IsNullOrEmpty(fillColor) ||
                                     fillColor.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                                     ? XColors.Transparent
                                     : ParseHexColor(fillColor);

                    var strokeXColor = ParseHexColor(strokeColor);

                    XBrush brush = new XSolidBrush(fillXColor);
                    XPen pen = new XPen(strokeXColor, strokeWidth);

                    // Optional: handle rounded corners rx, ry
                    double rx1 = obj.Value<double?>("rx") ?? 0;
                    double ry1 = obj.Value<double?>("ry") ?? 0;

                    if (rx1 > 0 || ry1 > 0)
                    {
                        gfx.DrawRoundedRectangle(pen, brush, left, top, width, height, rx1, ry1);
                    }
                    else
                    {
                        gfx.DrawRectangle(pen, brush, left, top, width, height);
                    }
                }
                break;

            //case "textbox":
            //    string text = obj.Value<string>("text") ?? "";
            //    if (fieldValues != null && fieldValues.ContainsKey(text))
            //        text = fieldValues[text];

            //    float fontSize = obj.Value<float?>("fontSize") ?? 16;
            //    gfx.DrawString(text, new XFont("Times New Roman", fontSize), XBrushes.Black,
            //        new XRect(left, top, width, height), XStringFormats.TopLeft);
            //    break;
            case "textbox":
            case "text":
                {
                    string label = obj.Value<string>("text") ?? "";
                    string value = "";

                    if (fieldValues != null && fieldValues.ContainsKey(label))
                    {
                        value = fieldValues[label];
                    }

                    // Final text will be "Label: Value" or just "Label" if no match
                    string finalText = string.IsNullOrEmpty(value) ? label : $"{label} {value}";

                    // Font properties
                    float fontSize = obj.Value<float?>("fontSize") ?? 16;
                    string fontFamily = obj.Value<string>("fontFamily") ?? "Arial";
                    string fontWeight = obj.Value<string>("fontWeight") ?? "normal";
                    string fontStyle = obj.Value<string>("fontStyle") ?? "normal";

                    // PdfSharp doesn’t support bold/italic as separate props; 
                    XFontStyle style = XFontStyle.Regular;
                    if (fontWeight.Equals("bold", StringComparison.OrdinalIgnoreCase))
                        style |= XFontStyle.Bold;
                    if (fontStyle.Equals("italic", StringComparison.OrdinalIgnoreCase))
                        style |= XFontStyle.Italic;

                    XFont font = new XFont(fontFamily, fontSize, style);

                    // Brush based on JSON fill (directly use it)
                    string fillColor = obj.Value<string>("fill") ?? "black";
                    XColor color = GetXColor(fillColor);
                    XBrush brush = new XSolidBrush(color);

                    // Draw text (ignore height constraint to avoid clipping)
                    gfx.DrawString(finalText, font, brush,
                        new XRect(left, top, width, fontSize * 1.5),  // dynamic height
                        XStringFormats.TopLeft);

                    break;
                }


            case "line":
                {
                    float leftLine = obj.Value<float?>("left") ?? 0;
                    float topLine = obj.Value<float?>("top") ?? 0;
                    float widthLine = obj.Value<float?>("width") ?? 0;
                    float heightLine = obj.Value<float?>("height") ?? 0;
                    float angle = obj.Value<float?>("angle") ?? 0;
                    float scaleX = obj.Value<float?>("scaleX") ?? 1;
                    float scaleY = obj.Value<float?>("scaleY") ?? 1;

                    // Apply scaling
                    widthLine *= scaleX;
                    heightLine *= scaleY;

                    // Compute endpoints of the line in local coords
                    float x1 = 0;
                    float y1 = 0;
                    float x2 = widthLine;
                    float y2 = heightLine;

                    // Rotate
                    double rad = angle * Math.PI / 180.0;
                    float rx1 = (float)(Math.Cos(rad) * x1 - Math.Sin(rad) * y1);
                    float ry1 = (float)(Math.Sin(rad) * x1 + Math.Cos(rad) * y1);
                    float rx2 = (float)(Math.Cos(rad) * x2 - Math.Sin(rad) * y2);
                    float ry2 = (float)(Math.Sin(rad) * x2 + Math.Cos(rad) * y2);

                    // Shift to page
                    float fx1 = leftLine + rx1;
                    float fy1 = topLine + ry1;
                    float fx2 = leftLine + rx2;
                    float fy2 = topLine + ry2;

                    gfx.DrawLine(XPens.Black, fx1, fy1, fx2, fy2);
                    break;
                }
            case "circle":
                float radius = obj.Value<float?>("radius") ?? width / 2;
                gfx.DrawEllipse(XPens.Black, XBrushes.White, left, top, radius * 2, radius * 2);
                break;

            case "ellipse":
                float rx = obj.Value<float?>("rx") ?? (width / 2);
                float ry = obj.Value<float?>("ry") ?? (height / 2);
                gfx.DrawEllipse(XPens.Black, XBrushes.White, left, top, rx * 2, ry * 2);
                break;

            //case "group":
            //case "barcode":
            //default:
            //    // Treat barcode groups as a barcode
            //    string barcodeValue = obj.Value<string>("text") ?? "123456";
            //    if (fieldValues != null && fieldValues.ContainsKey(barcodeValue))
            //        barcodeValue = fieldValues[barcodeValue];

            //    var writer = new BarcodeWriterPixelData
            //    {
            //        Format = BarcodeFormat.CODE_128,
            //        Options = new ZXing.Common.EncodingOptions
            //        {
            //            Height = (int)height,
            //            Width = (int)width
            //        }
            //    };

            //    var pixelData = writer.Write(barcodeValue);

            //    using (var bmp = new SKBitmap(new SKImageInfo(pixelData.Width, pixelData.Height)))
            //    {
            //        System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmp.GetPixels(), pixelData.Pixels.Length);
            //        using (var image = SKImage.FromBitmap(bmp))
            //        using (var ms = new MemoryStream())
            //        {
            //            image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
            //            ms.Position = 0;

            //            imageStreams.Add(ms); // Keep stream alive until PDF is saved

            //            var xImage = XImage.FromStream(() => ms);
            //            gfx.DrawImage(xImage, left, top, width, height);
            //        }
            //    }
            //    break;

            case "image":
            case "imagePlaceholder":
                {
                    // Coordinates and dimensions from JSON
                    float leftImage = obj.Value<float>("left");
                    float topImage = obj.Value<float>("top");
                    float widthImage = obj.Value<float?>("width") ?? 100;
                    float heightImage = obj.Value<float?>("height") ?? 50;

                    byte[] bytes = null;

                    // Base64 embedded in JSON
                    string base64Data = obj.Value<string>("data");
                    if (!string.IsNullOrEmpty(base64Data))
                    {
                        var base64 = base64Data.Contains(",") ? base64Data.Split(',')[1] : base64Data;
                        bytes = Convert.FromBase64String(base64);
                    }
                    else
                    {
                        // Load from fieldValues (uploaded image)
                        string fieldName = obj.Value<string>("fieldName") ?? "LogoImage"; // default to LogoImage if not set
                        if (fieldValues != null && fieldValues.TryGetValue(fieldName, out string imageData) && !string.IsNullOrEmpty(imageData))
                        {
                            var base64 = imageData.Contains(",") ? imageData.Split(',')[1] : imageData;
                            bytes = Convert.FromBase64String(base64);
                        }
                    }

                    // Draw image or placeholder
                    if (bytes != null)
                    {
                        try
                        {
                            using (var ms = new MemoryStream(bytes))
                            {
                                imageStreams.Add(ms); // Keep stream alive until PDF saved
                                var xImage = XImage.FromStream(() => ms);

                                // Preserve aspect ratio and fit inside placeholder
                                double scaleX = widthImage / xImage.PixelWidth;
                                double scaleY = heightImage / xImage.PixelHeight;
                                double scale = Math.Min(scaleX, scaleY);

                                double drawWidth = xImage.PixelWidth * scale;
                                double drawHeight = xImage.PixelHeight * scale;

                                // Center image inside placeholder
                                double offsetX = leftImage + (widthImage - drawWidth) / 2;
                                double offsetY = topImage + (heightImage - drawHeight) / 2;

                                gfx.DrawImage(xImage, offsetX, offsetY, drawWidth, drawHeight);
                            }
                        }
                        catch
                        {
                            gfx.DrawRectangle(XPens.Red, leftImage, topImage, widthImage, heightImage);
                            gfx.DrawString("Invalid Image", new XFont("Arial", 10), XBrushes.Red,
                                new XRect(leftImage, topImage, widthImage, heightImage), XStringFormats.Center);
                        }
                    }
                    else
                    {
                        // Draw placeholder if image missing
                        gfx.DrawRectangle(XPens.Red, leftImage, topImage, widthImage, heightImage);
                        gfx.DrawString("Image Missing", new XFont("Arial", 10), XBrushes.Red,
                            new XRect(leftImage, topImage, widthImage, heightImage), XStringFormats.Center);
                    }

                    break;
                }


            case "barcode":
            case "group":
                {
                    string barcodeType = obj.Value<string>("barcodeType") ?? "CODE128";
                    string labelText = "";
                    string barcodeValue = "123456789012"; // Default fallback

                    // Try to extract text from child
                    var children = obj["objects"];
                    if (children != null)
                    {
                        var label = children.FirstOrDefault(c => c.Value<string>("type") == "text");
                        if (label != null)
                        {
                            labelText = label.Value<string>("text") ?? "";

                            // Try to get dynamic value
                            if (fieldValues != null && fieldValues.TryGetValue(labelText, out string dynamicValue))
                            {
                                barcodeValue = dynamicValue;
                            }
                        }
                    }

                    // Map barcodeType to ZXing BarcodeFormat
                    BarcodeFormat format = BarcodeFormat.CODE_128;
                    switch (barcodeType)
                    {
                        case "EAN13": format = BarcodeFormat.EAN_13; break;
                        case "UPC": format = BarcodeFormat.UPC_A; break;
                        case "CODE39": format = BarcodeFormat.CODE_39; break;
                        case "ITF14": format = BarcodeFormat.ITF; break;
                        case "CODE128":
                        default:
                            format = BarcodeFormat.CODE_128;
                            break;
                    }

                    var writer = new BarcodeWriterPixelData
                    {
                        Format = format,
                        Options = new ZXing.Common.EncodingOptions
                        {
                            Height = (int)height,
                            Width = (int)width,
                            Margin = 0
                        }
                    };

                    var pixelData = writer.Write(barcodeValue);

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

                            // Draw barcode value below
                            var font = new XFont("Arial", 8, XFontStyle.Regular); // Tiny font
                            var textHeight = font.GetHeight();
                            gfx.DrawString(barcodeValue, font, XBrushes.Black,
                                new XRect(left, top + height + 2, width, textHeight),
                                XStringFormats.TopCenter);
                        }
                    }
                }
                break;


            default:
                // Optionally log or ignore
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


    private XColor GetXColor(string fillColor)
    {
        if (string.IsNullOrWhiteSpace(fillColor))
            return XColors.Black;

        // HEX format (#RRGGBB or #AARRGGBB)
        if (fillColor.StartsWith("#"))
        {
            string hex = fillColor.TrimStart('#');

            if (hex.Length == 6) // RRGGBB
            {
                int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return XColor.FromArgb(r, g, b);
            }
            else if (hex.Length == 8) // AARRGGBB
            {
                int a = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                int r = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                int g = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                int b = int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                return XColor.FromArgb(a, r, g, b);
            }
        }

        // RGB format rgb(r,g,b)
        if (fillColor.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var parts = fillColor.Replace("rgb(", "").Replace(")", "").Split(',');
            if (parts.Length == 3)
            {
                int r = int.Parse(parts[0]);
                int g = int.Parse(parts[1]);
                int b = int.Parse(parts[2]);
                return XColor.FromArgb(r, g, b);
            }
        }

        // Named colors (e.g., "black", "red")
        var prop = typeof(XColors).GetProperty(fillColor,
                     System.Reflection.BindingFlags.IgnoreCase |
                     System.Reflection.BindingFlags.Public |
                     System.Reflection.BindingFlags.Static);
        if (prop != null)
        {
            return (XColor)prop.GetValue(null);
        }

        // Fallback
        return XColors.Black;
    }

}

public class TemplateRequest
{
    public string TemplateJson { get; set; }
    //public Dictionary<string, string> FieldValues { get; set; }
}
