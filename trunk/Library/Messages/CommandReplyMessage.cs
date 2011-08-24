using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Org.Reddragonit.FreeSwitchSockets.Messages
{
    public class CommandReplyMessage : ASocketMessage
    {
        private string _replyMessage;

        internal CommandReplyMessage(string message,string subMsg) : 
            base(message)
        {
            _replyMessage = subMsg;
        }

        internal CommandReplyMessage(string message)
            : base((message.Contains("\n\n") ? message.Substring(0,message.IndexOf("\n\n")) : message))
        {
            _replyMessage = message.Substring(message.IndexOf("\n\n") + 2).Trim();
        }

        public bool Success
        {
            get
            {
                return (this["Reply-Text"] != null ? this["Reply-Text"] == "+OK" : false);
            }
        }

        public string Value
        {
            get { return this["Reply-Text"]; }
        }

        public string ReplyMessage
        {
            get { return _replyMessage; }
        }

        public override void ReadXml(XmlReader reader)
        {
            reader.MoveToContent();
            XmlReader xr = reader.ReadSubtree();
            xr.MoveToContent();
            if (xr.Value != "")
                _replyMessage = xr.Value;
            base.ReadXml(xr);
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Message");
            writer.WriteValue(_replyMessage);
            writer.WriteEndElement();
            writer.WriteStartElement("Data");
            base.WriteXml(writer);
            writer.WriteEndElement();
        }
    }
}
