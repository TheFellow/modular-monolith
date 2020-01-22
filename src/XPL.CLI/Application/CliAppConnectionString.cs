using XPL.Framework.Infrastructure.Persistence;

namespace XPL.CLI.Application
{
    public class CliAppConnectionString : ConnectionString
    {
        // TODO: Pull this from configuration
        public override string Value => @"Server=(localdb)\mssqllocaldb;Database=CodeCamps;Trusted_Connection=True;";
    }
}
