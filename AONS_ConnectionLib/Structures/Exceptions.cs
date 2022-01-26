using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AONS_ConnectionLib.Structures.Exceptions
{
    public class NoDestinationException : Exception
    {
        public NoDestinationException(string pMsg) : base(pMsg) { }
    }
}
