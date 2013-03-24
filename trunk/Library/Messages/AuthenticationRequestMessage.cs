using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.FreeSwitchSockets.Messages
{
    public class AuthenticationRequestMessage : ASocketMessage
    {
        public AuthenticationRequestMessage(string message)
            : base(message) { }
    }
}
