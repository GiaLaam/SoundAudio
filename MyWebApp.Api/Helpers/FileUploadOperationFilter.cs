using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

namespace MyWebApp.Api.Helpers
{
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Kiểm tra nếu action có [Consumes("multipart/form-data")]
            var hasFormFile = context.ApiDescription.ParameterDescriptions
                .Any(p => p.ModelMetadata?.ModelType == typeof(IFormFile));

            if (!hasFormFile) return;

            // Xóa các parameters cũ
            var parametersToRemove = context.ApiDescription.ParameterDescriptions
                .Where(p => p.Source.Id == "Body" || p.Source.Id == "Form")
                .Select(p => p.Name)
                .ToList();

            if (operation.Parameters != null)
            {
                var toRemove = operation.Parameters
                    .Where(p => parametersToRemove.Contains(p.Name))
                    .ToList();
                
                foreach (var param in toRemove)
                {
                    operation.Parameters.Remove(param);
                }
            }

            // Tạo schema cho multipart/form-data
            var properties = new Dictionary<string, OpenApiSchema>();
            var required = new HashSet<string>();

            foreach (var param in context.ApiDescription.ParameterDescriptions.Where(p => p.Source.Id == "Body" || p.Source.Id == "Form"))
            {
                if (param.ModelMetadata?.ModelType == typeof(IFormFile))
                {
                    properties[param.Name] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    };
                    
                    if (param.IsRequired)
                        required.Add(param.Name);
                }
                else
                {
                    properties[param.Name] = new OpenApiSchema
                    {
                        Type = "string"
                    };
                    
                    if (param.IsRequired)
                        required.Add(param.Name);
                }
            }

            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = properties,
                            Required = required
                        }
                    }
                }
            };
        }
    }
}
