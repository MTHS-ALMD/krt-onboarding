using KRT.Onboarding.Application.Common;
using KRT.Onboarding.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace KRT.Onboarding.Api.ErrorHandling;

/// <summary>
/// Converte exceções em respostas ProblemDetails (RFC 7807), mantendo
/// os controllers livres de try/catch.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflito"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Erro não tratado");

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Não vaza detalhes internos em erros inesperados.
                Detail = status == StatusCodes.Status500InternalServerError ? "Ocorreu um erro inesperado." : exception.Message
            }
        });
    }
}
