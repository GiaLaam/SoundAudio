using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

namespace MyWebApp.Api.Helpers
{
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var hasFileUpload = context.ApiDescription.ParameterDescriptions
                .Any(p =>
                    p.ModelMetadata?.ModelType == typeof(IFormFile) ||
                    (p.ModelMetadata?.ModelType?.IsClass ?? false) &&
                    p.ModelMetadata.ModelType.GetProperties()
                        .Any(prop => prop.PropertyType == typeof(IFormFile)));

            if (!hasFileUpload) return;

            operation.RequestBody = new OpenApiRequestBody
            {
                Content =
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["file"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
