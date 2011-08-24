using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Org.Reddragonit.FreeSwitchSockets.Messages
{
    public class SocketEvent : ASocketMessage
    {

        public SocketEvent(string message) : this(message, null) { }

        public SocketEvent(string message,string eventMessage) : base(message)
        {
            _message = eventMessage;
        }

        public string EventName{
            get{
                if (this["Event-Name"] == "CUSTOM")
                    return this["Event-Name"]+"."+this["Event-Subclass"];
                return this["Event-Name"];
            }
        }

        public string CoreUUID{
            get{return this["Core-UUID"];}
        }

        public DateTime EventDateLocal{
            get { return DateTime.Parse(this["Event-Date-Local"]); }
        }

        public DateTime EventDateGMT{
            get{return DateTime.Parse(this["Event-Date-GMT"]);}
        }

        public string EventCallingFile{
            get{return this["Event-Calling-File"];}
        }

        public string EventCallingFunction{
            get{return this["Event-Calling-Function"];}
        }

        public string EventCallingLineNumber{
            get{return this["Event-Calling-Line-Number"];}
        }

        public string UniqueID
        {
            get { return this["Unique-ID"]; }
        }

        public string CallerUUID
        {
            get { return this["Caller-Unique-ID"]; }
        }

        public string ChannelName
        {
            get { return this["Channel-Name"]; }
        }

        public string ValidatingPIN
        {
            get { return this["variable_inputted_validation_pin"]; }
        }

        private string _message;
        public string Message
        {
            get { return _message; }
            set { _message = value; }
        }

        public override void ReadXml(XmlReader reader)
        {
            reader.MoveToContent();
            XmlReader xr = reader.ReadSubtree();
            xr.MoveToContent();
            if (xr.Value != "")
                _message = xr.Value;
            base.ReadXml(xr);
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Message");
            if (_message != null)
                writer.WriteValue(_message);
            writer.WriteEndElement();
            writer.WriteStartElement("Data");
            base.WriteXml(writer);
            writer.WriteEndElement();
        }
    }
}
