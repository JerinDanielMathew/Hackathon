namespace BarcodeDesignerDemo.Requests.Command
{
    public class GenerateTagNewPdfHandlerCommand
    {
        public string TemplateJson { get; set; }
        public Dictionary<string, string> FieldValues { get; set; }
    }
}
