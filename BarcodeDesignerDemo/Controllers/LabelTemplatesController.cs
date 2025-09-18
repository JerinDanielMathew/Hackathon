using BarcodeDesignerDemo.Data.DbContextFile;
using BarcodeDesignerDemo.Data.Dtos;
using BarcodeDesignerDemo.Generator;
using BarcodeDesignerDemo.Requests.Command;
using BarcodeDesignerDemo.Requests.Handler;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Text.Json;
using LabelTemplate = BarcodeDesignerDemo.Data.Entity.LabelTemplate;

namespace BarcodeDesignerDemo.Controllers
{
    /// <summary>
    /// LabelTemplatesController
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class LabelTemplatesController : Controller
    {
        private readonly BarcodeDbContext _context;
        private readonly GeneratePriceTagPdfHandler _handler;
        private readonly GenerateTagNewPdfHandler _handlerNew;
        private readonly GenerateCSVPDFHandler _handlerCSV;

        public LabelTemplatesController(BarcodeDbContext context, GeneratePriceTagPdfHandler handler, GenerateTagNewPdfHandler generateTagNewPdfHandler, GenerateCSVPDFHandler handlerCSV)
        {
            _context = context;
            _handler = handler;
            _handlerNew = generateTagNewPdfHandler;
            _handlerCSV = handlerCSV;
        }

        [HttpPost("generate-pdf")]
        public IActionResult CreateTemplatePdf([FromBody] TemplateRequest request)
        {
            try
            {
                var command = new GenerateTagNewPdfHandlerCommand
                {
                    TemplateJson = request.TemplateJson,
                    //FieldValues = request.FieldValues
                };

                var pdfStream = _handlerNew.Handle(command);

                return File(pdfStream.ToArray(), "application/pdf", "PriceTag.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("generate-csv-pdf")]
        public async Task<IActionResult> GenerateCsvPdf(IFormFile csv, [FromForm] string templateId)
        {
            if (csv == null || csv.Length == 0)
                return BadRequest("CSV file is required.");

            if (string.IsNullOrWhiteSpace(templateId))
                return BadRequest("TemplateId is required.");

            var template = await _context.LabelTemplates
    .FirstOrDefaultAsync(t => t.Id == Convert.ToInt64(templateId));

            if (template == null)
                return NotFound($"Template with id {templateId} not found.");

            // Read CSV contents
            string csvContent;
            using (var reader = new StreamReader(csv.OpenReadStream()))
            {
                csvContent = await reader.ReadToEndAsync();
            }
            var lines = csvContent.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            var headers = lines.First().Split(',').Select(h => h.Trim()).ToArray();
            var rows = lines.Skip(1).Select(l => l.Split(',')).ToList();

            var command = new GenerateCSVDataTagPdfHandlerCommand
            {
                TemplateJson = template.TemplateJson,
                Rows = rows,
                Header = headers
            };

            var pdfStream = _handlerCSV.Handle(command);


            pdfStream.Position = 0; // reset stream position
            string fileName = $"Labels_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(pdfStream, "application/pdf", fileName);
        }

        [HttpPost("create-template-pdf-new")]
        public IActionResult CreateTemplatePdfNew([FromBody] TemplateRequest request)
        {
            try
            {
                //request.FieldValues = new Dictionary<string, string>
                //{
                //    { "text", "Product: ABC" },
                //    { "text2", "Batch: 12345" },
                //    { "text3", "Batch: 12345" },
                //    { "Barcode_1", "9876543210" }
                //};

//                request.TemplateJson = @"{
//  ""name"": ""jdm"",
//  ""createdAt"": ""2025-09-16T10:32:57.084Z"",
//  ""version"": ""2.0"",
//  ""canvasSize"": {
//    ""width"": 50,
//    ""height"": 40
//  },
//  ""dpi"": 300,
//  ""thumbnail"": ""data:image/png;base64,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"",
//  ""fabric"": {
//    ""canvas"": {
//      ""width_mm"": 50,
//      ""height_mm"": 40,
//      ""dpi"": 300
//    },
//    ""elements"": [
//      {
//        ""id"": ""element-1"",
//        ""type"": ""border-rectangle"",
//        ""x_mm"": 1.69,
//        ""y_mm"": 1.69,
//        ""width_mm"": 25.57,
//        ""height_mm"": 22.69,
//        ""content"": """",
//        ""additionalData"": {
//          ""backgroundColor"": ""rgba(0, 0, 0, 0)"",
//          ""borderColor"": ""rgb(0, 0, 0)"",
//          ""borderWidth"": 0,
//          ""borderWidthMm"": 0
//        },
//        ""rotation_deg"": 0,
//        ""locked"": false,
//        ""z_index"": 1
//      },
//      {
//        ""id"": ""element-3"",
//        ""type"": ""barcode"",
//        ""x_mm"": 2.2,
//        ""y_mm"": 9.99,
//        ""width_mm"": 11.94,
//        ""height_mm"": 5.08,
//        ""content"": ""036000291452"",
//        ""additionalData"": {
//          ""barcodeType"": ""upca""
//        },
//        ""rotation_deg"": 0,
//        ""locked"": false,
//        ""z_index"": 2
//      },
//      {
//        ""id"": ""element-4"",
//        ""type"": ""text"",
//        ""x_mm"": 9.74,
//        ""y_mm"": 16.93,
//        ""width_mm"": 10.16,
//        ""height_mm"": 2.54,
//        ""content"": ""Sample Text"",
//        ""additionalData"": {
//          ""fontSize"": 14,
//          ""fontSizePt"": 3.36,
//          ""fontFamily"": ""Arial"",
//          ""color"": ""#000000"",
//          ""backgroundColor"": ""transparent"",
//          ""fontWeight"": ""400"",
//          ""fontStyle"": ""normal""
//        },
//        ""rotation_deg"": 0,
//        ""locked"": false,
//        ""z_index"": 3
//      },
//      {
//        ""id"": ""element-5"",
//        ""type"": ""barcode"",
//        ""x_mm"": 14.9,
//        ""y_mm"": 3.64,
//        ""width_mm"": 11.94,
//        ""height_mm"": 5.08,
//        ""content"": ""5901234123457"",
//        ""additionalData"": {
//          ""barcodeType"": ""ean13""
//        },
//        ""rotation_deg"": 0,
//        ""locked"": false,
//        ""z_index"": 4
//      },
//      {
//        ""id"": ""element-5"",
//        ""type"": ""text"",
//        ""x_mm"": 16.59,
//        ""y_mm"": 12.53,
//        ""width_mm"": 10.16,
//        ""height_mm"": 2.54,
//        ""content"": ""Sample Text"",
//        ""additionalData"": {
//          ""fontSize"": 14,
//          ""fontSizePt"": 3.36,
//          ""fontFamily"": ""Arial"",
//          ""color"": ""#000000"",
//          ""backgroundColor"": ""transparent"",
//          ""fontWeight"": ""400"",
//          ""fontStyle"": ""normal""
//        },
//        ""rotation_deg"": 0,
//        ""locked"": false,
//        ""z_index"": 5
//      },
//      {
//        ""id"": ""element-6"",
//        ""type"": ""image"",
//        ""x_mm"": 17.36,
//        ""y_mm"": 16.34,
//        ""width_mm"": 8.47,
//        ""height_mm"": 6.77,
//        ""content"": ""data:image/png;base64,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"",
//        ""additionalData"": {},
//        ""rotation_deg"": 0,
//        ""locked"": false,
//        ""z_index"": 6
//      }
//    ]
//  }
//}
//";
                var command = new GenerateTagNewPdfHandlerCommand
                {
                    TemplateJson = request.TemplateJson,
                    //FieldValues = request.FieldValues
                };

                var pdfStream = _handlerNew.Handle(command);

                return File(pdfStream.ToArray(), "application/pdf", "PriceTag.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("create-template")]
        public async Task<IActionResult> CreateTemplate([FromBody] LabelTemplateDto dto)
        {
            var template = new LabelTemplate
            {
                Name = dto.Name,
                TemplateJson = dto.TemplateJson
            };

            _context.LabelTemplates.Add(template);
            await _context.SaveChangesAsync();
            return Ok(template.Id);
        }

        [HttpGet("get-templates")]
        public async Task<IActionResult> GetTemplate()
        {
            var templates = await _context.LabelTemplates.Where(x => x.Id != 0).ToListAsync();
            return Ok(templates);
        }

        [HttpPost("genrate-pdf")]
        public async Task<IActionResult> SaveTemplate([FromBody] LabelTemplateDto dto)
        {
            var fieldValues = new Dictionary<string, string>
                {
                    { "text", "Product: ABC" },
                    { "text2", "Batch: 12345" },
                    { "text3", "Batch: 12345" },
                    { "Barcode_1", "9876543210" }
                };

            string templateJson = @"{""version"":""5.1.0"",""objects"":[{""type"":""rect"",""version"":""5.1.0"",""originX"":""left"",""originY"":""top"",""left"":50,""top"":50,""width"":135,""height"":138,""fill"":""white"",""stroke"":""black"",""strokeWidth"":2,""strokeDashArray"":null,""strokeLineCap"":""butt"",""strokeDashOffset"":0,""strokeLineJoin"":""miter"",""strokeUniform"":false,""strokeMiterLimit"":4,""scaleX"":1,""scaleY"":1,""angle"":0,""flipX"":false,""flipY"":false,""opacity"":1,""shadow"":null,""visible"":true,""backgroundColor"":"""",""fillRule"":""nonzero"",""paintFirst"":""fill"",""globalCompositeOperation"":""source-over"",""skewX"":0,""skewY"":0,""rx"":0,""ry"":0},{""type"":""textbox"",""version"":""5.1.0"",""originX"":""left"",""originY"":""top"",""left"":53,""top"":89,""width"":101.46,""height"":29.154000000000003,""fill"":""black"",""stroke"":null,""strokeWidth"":1,""strokeDashArray"":null,""strokeLineCap"":""butt"",""strokeDashOffset"":0,""strokeLineJoin"":""miter"",""strokeUniform"":false,""strokeMiterLimit"":4,""scaleX"":1,""scaleY"":1,""angle"":0,""flipX"":false,""flipY"":false,""opacity"":1,""shadow"":null,""visible"":true,""backgroundColor"":"""",""fillRule"":""nonzero"",""paintFirst"":""fill"",""globalCompositeOperation"":""source-over"",""skewX"":0,""skewY"":0,""fontFamily"":""Times New Roman"",""fontWeight"":""normal"",""fontSize"":20,""text"":""Order Code:"",""underline"":false,""overline"":false,""linethrough"":false,""textAlign"":""left"",""fontStyle"":""normal"",""lineHeight"":1.16,""textBackgroundColor"":"""",""charSpacing"":0,""styles"":{},""direction"":""ltr"",""path"":null,""pathStartOffset"":0,""pathSide"":""left"",""pathAlign"":""baseline"",""minWidth"":20,""splitByGrapheme"":false},{""type"":""textbox"",""version"":""5.1.0"",""originX"":""left"",""originY"":""top"",""left"":58,""top"":58,""width"":121.24980000000001,""height"":14.238000000000001,""fill"":""black"",""stroke"":null,""strokeWidth"":1,""strokeDashArray"":null,""strokeLineCap"":""butt"",""strokeDashOffset"":0,""strokeLineJoin"":""miter"",""strokeUniform"":false,""strokeMiterLimit"":4,""scaleX"":1,""scaleY"":1,""angle"":0,""flipX"":false,""flipY"":false,""opacity"":1,""shadow"":null,""visible"":true,""backgroundColor"":"""",""fillRule"":""nonzero"",""paintFirst"":""fill"",""globalCompositeOperation"":""source-over"",""skewX"":0,""skewY"":0,""fontFamily"":""Times New Roman"",""fontWeight"":""normal"",""fontSize"":20,""text"":""Code:"",""underline"":false,""overline"":false,""linethrough"":false,""textAlign"":""left"",""fontStyle"":""normal"",""lineHeight"":1.16,""textBackgroundColor"":"""",""charSpacing"":0,""styles"":{},""direction"":""ltr"",""path"":null,""pathStartOffset"":0,""pathSide"":""left"",""pathAlign"":""baseline"",""minWidth"":20,""splitByGrapheme"":false},{""type"":""group"",""version"":""5.1.0"",""originX"":""left"",""originY"":""top"",""left"":62,""top"":139,""width"":115.07039999999999,""height"":35.27,""fill"":""rgb(0,0,0)"",""stroke"":null,""strokeWidth"":0,""strokeDashArray"":null,""strokeLineCap"":""butt"",""strokeDashOffset"":0,""strokeLineJoin"":""miter"",""strokeUniform"":false,""strokeMiterLimit"":4,""scaleX"":1,""scaleY"":1,""angle"":0,""flipX"":false,""flipY"":false,""opacity"":1,""shadow"":null,""visible"":true,""backgroundColor"":"""",""fillRule"":""nonzero"",""paintFirst"":""fill"",""globalCompositeOperation"":""source-over"",""skewX"":0,""skewY"":0,""objects"":[{""type"":""rect"",""version"":""5.1.0"",""originX"":""left"",""originY"":""top"",""left"":-81.14,""top"":-25.73,""width"":200,""height"":60,""fill"":""white"",""stroke"":""black"",""strokeWidth"":1,""strokeDashArray"":null,""strokeLineCap"":""butt"",""strokeDashOffset"":0,""strokeLineJoin"":""miter"",""strokeUniform"":false,""strokeMiterLimit"":4,""scaleX"":1,""scaleY"":1,""angle"":0,""flipX"":false,""flipY"":false,""opacity"":1,""shadow"":null,""visible"":true,""backgroundColor"":"""",""fillRule"":""nonzero"",""paintFirst"":""fill"",""globalCompositeOperation"":""source-over"",""skewX"":0,""skewY"":0,""rx"":0,""ry"":0},{""type"":""text"",""version"":""5.1.0"",""originX"":""center"",""originY"":""center"",""left"":-81.14,""top"":-25.73,""width"":76.45,""height"":18.08,""fill"":""black"",""stroke"":null,""strokeWidth"":1,""strokeDashArray"":null,""strokeLineCap"":""butt"",""strokeDashOffset"":0,""strokeLineJoin"":""miter"",""strokeUniform"":false,""strokeMiterLimit"":4,""scaleX"":1,""scaleY"":1,""angle"":0,""flipX"":false,""flipY"":false,""opacity"":1,""shadow"":null,""visible"":true,""backgroundColor"":"""",""fillRule"":""nonzero"",""paintFirst"":""fill"",""globalCompositeOperation"":""source-over"",""skewX"":0,""skewY"":0,""fontFamily"":""Times New Roman"",""fontWeight"":""normal"",""fontSize"":16,""text"":""BARCODE"",""underline"":false,""overline"":false,""linethrough"":false,""textAlign"":""left"",""fontStyle"":""normal"",""lineHeight"":1.16,""textBackgroundColor"":"""",""charSpacing"":0,""styles"":{},""direction"":""ltr"",""path"":null,""pathStartOffset"":0,""pathSide"":""left"",""pathAlign"":""baseline""}]},{""type"":""line"",""version"":""5.1.0"",""originX"":""left"",""originY"":""top"",""left"":139,""top"":50,""width"":69,""height"":0,""fill"":""rgb(0,0,0)"",""stroke"":""black"",""strokeWidth"":2,""strokeDashArray"":null,""strokeLineCap"":""butt"",""strokeDashOffset"":0,""strokeLineJoin"":""miter"",""strokeUniform"":false,""strokeMiterLimit"":4,""scaleX"":1,""scaleY"":1,""angle"":47.83,""flipX"":false,""flipY"":false,""opacity"":1,""shadow"":null,""visible"":true,""backgroundColor"":"""",""fillRule"":""nonzero"",""paintFirst"":""fill"",""globalCompositeOperation"":""source-over"",""skewX"":0,""skewY"":0,""x1"":-150,""x2"":150,""y1"":0,""y2"":0}],""background"":""#fff""}";
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var templates = JsonSerializer.Deserialize<LabelTemplateNew>(templateJson, options);
            QuestPDF.Settings.License = LicenseType.Community;
            using var stream = new MemoryStream();

            // Generate PDF into memory instead of file path

            var document = LabelGenerator.GenerateLabelPdf(templates, fieldValues, "LabelOutput.pdf");
            document.GeneratePdf(stream);

            stream.Position = 0; // reset stream pointer

            return File(stream.ToArray(), "application/pdf", "label.pdf");

            //return Ok(template.Id);
        }

    }
}
