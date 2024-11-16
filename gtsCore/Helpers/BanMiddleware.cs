using GamestatsBase;
using Microsoft.AspNetCore.Http.Features;
using PkmnFoundations.Structures;
using PkmnFoundations.Wfc;

namespace gtsCore.Helpers;

public class BanMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IpAddressHelper _ipAddressHelper;

    public BanMiddleware(RequestDelegate next, IpAddressHelper ipAddressHelper)
    {
        _next = next;
        _ipAddressHelper = ipAddressHelper;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var pid = context.Items["pid"] as int?;
        if (pid is null)
        {
            await _next(context);
            return;
        }

        var endpoint = context.Features.Get<IEndpointFeature>()?.Endpoint;
        var config = endpoint?.Metadata.GetMetadata<BanMiddlewareAttribute>();
        if (config == null)
        {
            await _next(context);
            return;
        }

        foreach (var gen in config.Generations)
        {
            var ban = BanHelper.GetBanStatus(pid.Value, _ipAddressHelper.GetIpAddress(context.Request), gen);
            if (ban != null && ban.Level > BanLevels.Restricted)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await _next(context);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class BanMiddlewareAttribute : Attribute
{
    public IEnumerable<Generations> Generations { get; }
    public BanMiddlewareAttribute(params Generations[] generations)
    {
        Generations = generations;
    }
}
