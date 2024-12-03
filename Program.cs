using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YourNamespace.Services;
using Microsoft.OpenApi.Models;
using System.Threading;
using System.Net;
using Microsoft.AspNetCore.Cors.Infrastructure;
using System;
using System.IO;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

ThreadPool.SetMinThreads(100, 100);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "wwwroot",
    ContentRootPath = Directory.GetCurrentDirectory()
});

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Data Analysis API", Version = "v1" });
    
    c.OperationFilter<FileUploadOperation>();
});

builder.Services.AddScoped<IDataAnalysisService, DataAnalysisService>();

var app = builder.Build();

app.UseCors("AllowAllOrigins");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Data Analysis API v1");
    c.RoutePrefix = string.Empty;
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();

public class FileUploadOperation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileUploadAttributes = context.MethodInfo.GetCustomAttributes(typeof(HttpPostAttribute), false).Any();
        if (fileUploadAttributes)
        {
            operation.Parameters.Clear();
            operation.RequestBody = new OpenApiRequestBody
            {
                Content =
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties =
                            {
                                ["File1"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                },
                                ["File2"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                },
                                ["File1FieldsJson"] = new OpenApiSchema
                                {
                                    Type = "string"
                                },
                                ["File2FieldsJson"] = new OpenApiSchema
                                {
                                    Type = "string"
                                },
                                ["ConfigurationJson"] = new OpenApiSchema
                                {
                                    Type = "string"
                                }
                            },
                            Required = new HashSet<string> { "File1", "File2" }
                        }
                    }
                }
            };
        }
    }
}