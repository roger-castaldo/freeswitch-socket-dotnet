using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.FreeSwitchSockets.Messages
{
    public class APIResponseMessage : ASocketMessage
    {
        internal APIResponseMessage(string message) : base(message)
        {
        }
    }
}
