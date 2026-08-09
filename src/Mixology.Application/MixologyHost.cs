using Microsoft.Extensions.Hosting;

namespace Mixology.Application;

public static class MixologyHost
{
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        HostApplicationBuilderSettings settings = new()
        {
            ApplicationName = "Mixology",
            Args = args,
        };

        return Host.CreateApplicationBuilder(settings);
    }
}

