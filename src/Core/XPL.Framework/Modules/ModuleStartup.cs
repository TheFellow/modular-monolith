using Lamar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XPL.Framework.Modules
{
    public abstract class ModuleStartup
    {
        public abstract string ModuleName { get; }

        public abstract ServiceRegistry ModuleRegistry { get; }
    }
}
