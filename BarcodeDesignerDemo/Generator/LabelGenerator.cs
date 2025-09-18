using Microsoft.EntityFrameworkCore.Metadata.Internal;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZXing;
using ZXing.Common;
using static QuestPDF.Helpers.Colors;


namespace BarcodeDesignerDemo.Generator
{
    public class LabelTemplateNew
    {
        public Label Label { get; set; }
        public List<Field> Fields { get; set; }
    }
    public class Label {
        [JsonPropertyName("left")]
        public double Left { get; set; }

        [JsonPropertyName("top")]
        public double Top { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }
    }
    public class Field
    {
        public string Type { get; set; }          // "text", "number", "barcode"
        public string Placeholder { get; set; }   // "TextField_1" etc
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string BarcodeType { get; set; } // "Code128", "QR", etc.
    }

    public static class LabelGenerator
    {
        // If your template values are in pixels at 96 DPI, convert to PDF points:
        // points = px * 72 / DPI  (72 points per inch)
        private static double PxToPoints(double px, double dpi = 96.0) => px * 72.0 / dpi;

        //public static void GenerateLabelPdf(string templateJson, Dictionary<string, string> fieldValues, string outputPdfPath)
        //{
        //    var options = new JsonSerializerOptions
        //    {
        //        PropertyNameCaseInsensitive = true
        //    };

        //    var template = JsonSerializer.Deserialize<LabelTemplate>(templateJson, options);

        //    var pageWidthPt = (float)PxToPoints(template.Label.Width);
        //    var pageHeightPt = (float)PxToPoints(template.Label.Height);

        //    Document.Create(container =>
        //    {
        //        container.Page(page =>
        //        {
        //            page.Size(pageWidthPt, pageHeightPt);
        //            page.Margin(0);

        //            // Use a stack to place multiple items freely
        //            page.Content().Column(column =>
        //            {
        //                foreach (var field in template.Fields)
        //                {
        //                    var value = fieldValues.ContainsKey(field.Placeholder) ? fieldValues[field.Placeholder] : string.Empty;
        //                    var leftPt = (float)PxToPoints(field.Left);
        //                    var topPt = (float)PxToPoints(field.Top);
        //                    var widthPt = (float)PxToPoints(field.Width);
        //                    var heightPt = (float)PxToPoints(field.Height);

        //                    column.Item().Element(container =>
        //                    {
        //                        container
        //                            .TranslateX(leftPt)
        //                            .TranslateY(topPt)
        //                            .Width(widthPt)
        //                            //.Height(heightPt)
        //                            .Element(inner =>
        //                            {
        //                                if (field.Type == "text" || field.Type == "number")
        //                                    inner.Text(value ?? string.Empty).FontSize(12).FontColor(Colors.Black);
        //                                else if (field.Type == "barcode")
        //                                {
        //                                    using var bmp = GenerateBarcodeBitmap(value ?? string.Empty, field.Width, field.Height);
        //                                    using var ms = new MemoryStream();
        //                                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        //                                    inner.Image(ms.ToArray(), ImageScaling.FitArea);
        //                                }
        //                            });
        //                    });
        //                }
        //            });
        //        });
        //    })
        //    .GeneratePdf(outputPdfPath);
        //}
        public static IDocument GenerateLabelPdf(LabelTemplateNew template, Dictionary<string, string> fieldValues, string outputPdfPath)
        {
            var pageWidthPt = PxToPoints(template.Label.Width);
            var pageHeightPt = PxToPoints(template.Label.Height);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Content().Layers(layers =>
                    {
                        layers.PrimaryLayer().Element(c =>
                        {
                            c.TranslateX((float)template.Label.Left)
                             .TranslateY((float)template.Label.Top)
                             .Width((float)template.Label.Width)
                             .Height((float)template.Label.Height)
                             .Border(2);
                        });

                        foreach (var field in template.Fields)
                        {
                            string value = fieldValues.ContainsKey(field.Placeholder)
                                ? fieldValues[field.Placeholder]
                                : field.Placeholder;

                            layers.Layer().Element(e =>
                            {
                                e.TranslateX((float)(template.Label.Left + field.Left))
                                 .TranslateY((float)(template.Label.Top + field.Top))
                                 .Width((float)field.Width)
                                 .Height((float)field.Height)
                                 .Border(1)
                                 .Element(inner =>
                                 {
                                     if (field.Type == "text")
                                     {
                                         inner.AlignMiddle()
                                              .AlignLeft()
                                              .Text(value);
                                     }
                                     else if (field.Type == "barcode")
                                     {
                                         var barcodeBytes = BarcodeHelper.GenerateBarcode(
                                             value,
                                             field.BarcodeType ?? "CODE128",   // fallback if null
                                             field.Width,
                                             field.Height
                                         );

                                         inner.Image(barcodeBytes);
                                     }
                                 });
                            });

                        }

                    });
                });
            });
            document.GeneratePdf(outputPdfPath);
            return document;

        }


        private static byte[] GenerateBarcode(string value, string barcodeType, int width, int height)
        {
            var writer = new ZXing.BarcodeWriterPixelData
            {
                Format = barcodeType switch
                {
                    "QR" => ZXing.BarcodeFormat.QR_CODE,
                    "EAN13" => ZXing.BarcodeFormat.EAN_13,
                    "Code128" => ZXing.BarcodeFormat.CODE_128,
                    _ => ZXing.BarcodeFormat.CODE_128
                },
                Options = new ZXing.Common.EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 0
                }
            };

            var pixelData = writer.Write(value);
            using var bitmap = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
            System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
            bitmap.UnlockBits(bitmapData);

            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }


        public static float PxToPoints(float px) => px * 0.75f;




        //private static void AddFieldToLayer(QuestPDF.Fluent.LayersDescriptor layers, Field field, string value, float leftPt, float topPt, float widthPt, float heightPt)
        //{
        //    layers.Layer(layer =>
        //    {
        //        if (field.Type == "text" || field.Type == "number")
        //        {
        //            layer.Element(container =>
        //            {
        //                container
        //                    .TranslateX(leftPt)
        //                    .TranslateY(topPt)
        //                    .Width(widthPt)
        //                    .Height(heightPt)
        //                    .Text(value ?? string.Empty)
        //                    .FontSize(12)
        //                    .FontColor(Colors.Black);
        //            });
        //        }
        //        else if (field.Type == "barcode")
        //        {
        //            using var bmp = GenerateBarcodeBitmap(value ?? string.Empty, field.Width, field.Height);
        //            using var ms = new MemoryStream();
        //            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        //            var imageBytes = ms.ToArray();

        //            layer.Element(container =>
        //            {
        //                container
        //                    .TranslateX(leftPt)
        //                    .TranslateY(topPt)
        //                    .Width(widthPt)
        //                    .Height(heightPt)
        //                    .Image(imageBytes, ImageScaling.FitArea);
        //            });
        //        }
        //    });

        //}


        private static Bitmap GenerateBarcodeBitmap(string text, int pxWidth, int pxHeight)
        {
            // Use ZXing to generate CODE_128 (change format if you want QR/EAN etc.)
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width = Math.Max(1, pxWidth),
                    Height = Math.Max(1, pxHeight),
                    Margin = 0
                }
            };

            var pixelData = writer.Write(text);

            // Create bitmap from pixel bytes
            var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                ImageLockMode.WriteOnly, bitmap.PixelFormat);

            try
            {
                System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }
    }
}
