using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawSocket.Server
{
    public class HandshakeState
    {
        public uint ServerSeq { get; set; }
        public uint ClientSeq { get; set; }
        public DateTime Timestamp { get; set; }
    }

}
