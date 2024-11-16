using Microsoft.Extensions.Primitives;

namespace gtsCore.Helpers;

public class IpAddressHelper
{
    private readonly ICollection<string> _allowedProxies;

    public IpAddressHelper(IConfiguration config)
    {
        _allowedProxies = config["AllowedProxies"]?.Split(',').Select(s => s.Trim()).ToArray() ?? [];
    }

    public string GetIpAddress(HttpRequest request)
    {
        string hostAddress = request.Host.Host;

        if (!_allowedProxies.Contains(hostAddress)) return hostAddress; // return real IP if not a blessed proxy
        if (request.Headers["X-Forwarded-For"] == StringValues.Empty) return hostAddress;

        var xForwardedFor = request.Headers["X-Forwarded-For"].Select(s => RemovePort(s.Trim()));

        foreach (string s in xForwardedFor.Reverse())
        {
            if (!_allowedProxies.Contains(s)) return s; // return LAST IP in the proxy chain that's not trusted. (everything coming earlier could be spoofed)
        }

        // these conditions can only happen if the real user is at a blessed proxy IP address. (probably localhost)
        return xForwardedFor.FirstOrDefault() ?? hostAddress;
    }

    private static string RemovePort(string ip)
    {
        if (ip.Contains(':') && ip.Contains('.'))
            return ip[..ip.IndexOf(':')];
        else
            return ip;
    }

    public static uint Ipv4ToBinary(string ip)
    {
        string[] split = ip.Split('.');
        if (split.Length != 4) throw new FormatException("Format not valid for an IPV4 address.");

        return BitConverter.ToUInt32(split.Select(s => Convert.ToByte(s)).Reverse().ToArray(), 0);
    }
}
