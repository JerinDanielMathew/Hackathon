namespace BarcodeDesignerDemo.Requests.Command
{
    public class GenerateCSVDataTagPdfHandlerCommand
    {
        public string TemplateJson { get; set; }

        public List<string[]> Rows { get; set; }

        public string[] Header { get; set; }

    }
}
