using BarcodeDesignerDemo.Data.Dtos;
using BarcodeDesignerDemo.Data.Entity;

namespace BarcodeDesignerDemo.Data.Extensions
{
    /// <summary>
    /// static class for extension methods
    /// </summary>
    public static class LabelTemplateExtension
    {
        /// <summary>
        /// Return label Entity from Dto
        /// </summary>
        /// <param name="labelTemplateDto"></param>
        /// <returns></returns>
        public static Data.Entity.LabelTemplate AsStudentEntity(this LabelTemplateDto labelTemplateDto)
        {
            var label = new Data.Entity.LabelTemplate()
            {
                Id = labelTemplateDto.Id,
                Name = labelTemplateDto.Name,
                TemplateJson = labelTemplateDto.TemplateJson
            };
            return label;
        }

        /// <summary>
        /// Return Label Entity from Dto
        /// </summary>
        /// <param name="labelTemplate"></param>
        /// <returns></returns>
        public static List<LabelTemplateDto> AsLabelTemplateDto(this List<LabelTemplate> labelTemplates)
        {
            return labelTemplates.Select(x => new LabelTemplateDto
            {
                Id = x.Id,
                Name = x.Name,
                TemplateJson = x.TemplateJson
            }).ToList();
        }
    }
}
