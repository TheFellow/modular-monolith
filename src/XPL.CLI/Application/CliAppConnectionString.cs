using Microsoft.Extensions.Configuration;
using XPL.Framework.Infrastructure.Persistence;

namespace XPL.CLI.Application
{
    public class CliAppConnectionString : ConnectionString
    {
        private readonly IConfiguration _configuration;
        public CliAppConnectionString(IConfiguration configuration) => _configuration = configuration;

        public override string Value => _configuration.GetConnectionString("CliDb");
    }
}
