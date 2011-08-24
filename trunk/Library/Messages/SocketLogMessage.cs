using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.FreeSwitchSockets.Outbound;
using System.Xml;

namespace Org.Reddragonit.FreeSwitchSockets.Messages
{
    public class SocketLogMessage : ASocketMessage
    {
        public FreeSwitchLogLevels? Level
        {
            get
            {
                if (this["Log-Level"] == null)
                    return null;
                int num = int.Parse(this["Log-Level"]);
                if (num > (int)FreeSwitchLogLevels.DEBUG)
                    return FreeSwitchLogLevels.DEBUG;
                return (FreeSwitchLogLevels)num;
            }
        }

        public string TextChannel
        {
            get { return this["Text-Channel"]; }
        }

        public string LogFile
        {
            get { return this["Log-File"]; }
        }

        public string LogFunc
        {
            get { return this["Log-Func"]; }
        }

        public string LogLine
        {
            get{return this["Log-Line"];}
        }

        public string UserData
        {
            get { return this["User-Data"]; }
        }

        public string ContentLength
        {
            get { return this["Content-Length"]; }
        }

        public DateTime MsgTime
        {
            get
            {
                return DateTime.Parse(_fullMessage.Substring(0, _fullMessage.IndexOf("[")).Trim());
            }
        }

        public string LogLineText
        {
            get
            {
                return _fullMessage.Substring(_fullMessage.IndexOf(LogFile + ":" + LogLine) + (LogFile + ":" + LogLine).Length);
            }
        }

        private string _fullMessage;
        public string FullMessage
        {
            get { return _fullMessage; }
            set { _fullMessage = value; }
        }

        public SocketLogMessage(string EventText) : base(EventText)
        {
            
        }

        public override void ReadXml(XmlReader reader)
        {
            reader.MoveToContent();
            XmlReader xr = reader.ReadSubtree();
            xr.MoveToContent();
            if (xr.Value != "")
                _fullMessage = xr.Value;
            base.ReadXml(xr);
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Message");
            if (_fullMessage != null)
                writer.WriteValue(_fullMessage);
            writer.WriteEndElement();
            writer.WriteStartElement("Data");
            base.WriteXml(writer);
            writer.WriteEndElement();
        }
    }
}
