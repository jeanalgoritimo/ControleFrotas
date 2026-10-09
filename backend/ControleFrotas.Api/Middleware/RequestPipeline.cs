using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace ControleFrotas;

public static class RequestPipeline
{
    public static void UseFleetPipeline(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try { await next(); }
            catch (BusinessException ex) { await Error(context, 400, ex.Message); }
            catch (RecordNotFoundException) { await Error(context, 404, "Registro não encontrado."); }
            catch (DbUpdateConcurrencyException) { await Error(context, 409, "Registro alterado por outra sessão. Atualize e tente novamente."); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { await Error(context, 409, "Não foi possível gravar. Verifique duplicidade de placa, CPF, marca ou modelo."); }
            catch (AntiforgeryValidationException) { await Error(context, 400, "Sessão de formulário inválida. Recarregue a página."); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Falha ao processar {Path}", context.Request.Path);
                await Error(context, 500, "Falha interna. Consulte o log do servidor.");
            }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.Use(async (context, next) =>
        {
            if (new[] { "POST", "PUT", "DELETE", "PATCH" }.Contains(context.Request.Method))
                await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
            await next();
        });
    }
    static async Task Error(HttpContext c, int status, string message) { c.Response.StatusCode = status; await c.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = message, Instance = c.Request.Path, Extensions = { ["message"] = message, ["traceId"] = c.TraceIdentifier } }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json"); }

}
