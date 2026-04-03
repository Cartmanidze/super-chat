using Scalar.AspNetCore;

namespace SuperChat.Api.Features.Documentation;

public static class ApiDocumentationExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi("v1");
        return services;
    }

    public static IEndpointRouteBuilder MapApiDocumentation(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOpenApi("/openapi/{documentName}.json");
        endpoints.MapScalarApiReference("/docs");

        return endpoints;
    }
}
