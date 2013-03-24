using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.FreeSwitchSockets.Messages
{
    public class DisconnectNoticeMessage : ASocketMessage
    {
        public DisconnectNoticeMessage(string message)
            : base(message) { }

        public int LingerTime
        {
            get { return int.Parse(this["Linger-Time"]); }
        }

        public string ChannelName
        {
            get { return this["Channel-Name"]; }
        }

        public string SessionUUID
        {
            get { return this["Controlled-Session-UUID"]; }
        }
    }
}
