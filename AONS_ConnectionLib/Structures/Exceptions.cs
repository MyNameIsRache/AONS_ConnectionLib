using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AONS_ConnectionLib.Structures.Exceptions
{
    public class ConnectionLibException : Exception
    {
        public ConnectionLibException() : base() { }
        public ConnectionLibException(string pMsg) : base (pMsg) { }
    }

    public class NoDestinationException : ConnectionLibException
    {
        public NoDestinationException(string pMsg) : base(pMsg) { }
    }

    public class IPInvalidException : ConnectionLibException
    {
        public IPInvalidException(string pMsg) : base(pMsg) { }
    }
}
