namespace BarcodeDesignerDemo.Requests.Command
{
    public class GeneratePriceTagPdfCommand
    {
        public string TemplateJson { get; set; }
        public Dictionary<string, string> FieldValues { get; set; }
    }
}
