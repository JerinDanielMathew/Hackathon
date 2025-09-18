using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;

namespace BarcodeDesignerDemo.Generator
{
    public class BarcodeHelper
    {
        public static byte[] GenerateBarcode(string value, string barcodeType, double width, double height)
        {
            // Map string type to ZXing BarcodeFormat
            BarcodeFormat format = barcodeType.ToUpper() switch
            {
                "CODE128" => BarcodeFormat.CODE_128,
                "EAN13" => BarcodeFormat.EAN_13,
                "EAN8" => BarcodeFormat.EAN_8,
                "UPC" => BarcodeFormat.UPC_A,
                "QR" => BarcodeFormat.QR_CODE,
                _ => BarcodeFormat.CODE_128
            };

            var writer = new BarcodeWriterPixelData
            {
                Format = format,
                Options = new EncodingOptions
                {
                    Width = (int)width,
                    Height = (int)height,
                    Margin = 0,
                    PureBarcode = true
                }
            };

            var pixelData = writer.Write(value);

            using var image = Image.LoadPixelData<Rgba32>(pixelData.Pixels, pixelData.Width, pixelData.Height);
            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());

            return ms.ToArray();
        }
    }
}
