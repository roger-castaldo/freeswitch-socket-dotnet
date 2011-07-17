using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Org.Reddragonit.FreeSwitchSockets.Messages;
using System.Text.RegularExpressions;

namespace Org.Reddragonit.FreeSwitchSockets.Inbound
{
    public class InboundConnection : ASocket
    {

        private const string REGISTRATIONS_FOR_PROFILE_CHECK_COMMAND = "sofia status profile {0} reg";
        private const string REGEX_EXTENSION_CHECK = "^MWI-Account:\\s+{0}@{1}\\s*$";

        private const string RANDOM_VAR_NAME_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
        private const string EXECUTE_COMPLETE_EVENT_NAME="CHANNEL_EXECUTE_COMPLETE";
        private const string CHANNEL_END_EVENT_NAME = "CHANNEL_HANGUP_COMPLETE";
        private const string APP_RESPONSE_VARIABLE_NAME = "Application-Response";
        private const string APP_NAME_VARIABLE_NAME = "Application";
        private const string APP_DATA_VARIABLE_NAME = "Application-Data";

        private Dictionary<string, string> _properties;
        private ManualResetEvent _awaitingCommandEvent = new ManualResetEvent(false);
        private SocketEvent _currentEvent;
        private string _awaitingCommand = "";
        private Queue<ManualResetEvent> _awaitingCommands;

        private DateTime _startTime;
        public DateTime StartTime
        {
            get { return _startTime; }
        }

        private bool _isHungup;
        public bool IsHungUp
        {
            get { return _isHungup; }
        }
        
        public InboundConnection(TcpClient client)
            : base(client.Client)
        {
            _isHungup = false;
            _awaitingCommands = new Queue<ManualResetEvent>();
        }

        private string RandomVariable
        {
            get
            {
                string ret = "";
                while (ret.Length < 10)
                {
                    ret += RANDOM_VAR_NAME_CHARS[Random.Next(0, RANDOM_VAR_NAME_CHARS.Length - 1)].ToString();
                }
                return ret;
            }
        }

        #region Operations
        #region BasicOps
        public void RingReady()
        {
            ExecuteApplication("ring_ready", false);
        }
        
        public void Answer()
        {
            ExecuteApplication("answer", false);
        }
        
        public void Sleep(int milliSeconds)
        {
            ExecuteApplication("sleep", "data=" + milliSeconds.ToString(), false);
            Thread.Sleep(milliSeconds);
        }

        public void Hangup()
        {
            ExecuteApplication("hangup", true);
        }

        public bool IsExtensionLive(string extensionNumber, string domain, string profile)
        {
            string apiRes = _IssueAPICommand(string.Format(REGISTRATIONS_FOR_PROFILE_CHECK_COMMAND, profile), true);
            return new Regex(string.Format(REGEX_EXTENSION_CHECK, extensionNumber, domain),RegexOptions.Compiled|RegexOptions.ECMAScript).Matches(extensionNumber).Count>0;
        }

        #endregion
        #region Bridging
        public SocketEvent BridgeToExtension(string extension,string domain,bool eventLock)
        {
            return ExecuteApplication("bridge", "user/" + extension + "@" + domain, eventLock);
        }

        public SocketEvent BridgeToMultipleExtensions(sDomainExtensionPair[] extensions, bool sequential, bool eventLock)
        {
            string dstring = "";
            foreach (sDomainExtensionPair sdep in extensions)
            {
                dstring+=(sequential ? "," : "|")+"user/"+sdep.Extension+"@"+sdep.Domain;
            }
            if (dstring.Length > 1)
                dstring = dstring.Substring(1);
            return ExecuteApplication("bridge", dstring, eventLock);
        }

        public SocketEvent Voicemail(string context, string domain, string extension)
        {
            return ExecuteApplication("voicemail", context + " " + domain + " " + extension, true);
        }

        public SocketEvent BridgeOutGateway(string gateway, string number,bool eventLock)
        {
            return ExecuteApplication("bridge", "sofia/gateway/" + gateway + "/" + number,eventLock);
        }
        #endregion
        #region Variables
        private void SetVariable(string name, string value)
        {
            ExecuteApplication("set", name + "=" + value,false);
        }

        public void ExportSetting(string name, string value)
        {
            ExecuteApplication("export", name + "=" + value,false);
        }
        #endregion
        #region Conferencing
        public void ConferenceSetAutoCallExtension(string extension, string domain)
        {
            ExecuteApplication("conference_set_auto_outcall", "USER/" + extension + "@" + domain, true);
        }

        public void JoinConference(string name,bool eventLock)
        {
            ExecuteApplication("conference", name, eventLock);
        }

        public void KickFromConference(string conferenceName, string extension, bool eventLock)
        {
            ExecuteApplication("conference", conferenceName + " kick " + extension, eventLock);
        }
        #endregion
        #region Audio
        public void PlayAudioFile(string filePath,bool eventLock)
        {
            ExecuteApplication("playback", filePath, eventLock);
        }

        public string PlayAndGetDigits(int minDigits, int maxDigits, int tries,long timeout, string terminators, string file, string invalidFile, string regexp, int? digitTimeout)
        {
            string var = RandomVariable;
            SocketEvent ev = ExecuteApplication("play_and_get_digits", minDigits.ToString() + " " + maxDigits.ToString() + " " + tries.ToString() + " " + timeout.ToString() + " " + terminators + " " + file + " " + (invalidFile != null ? invalidFile : "silence_stream://250")+" "+var+" "+(regexp ==null ? "\\d+" : regexp)+" "+ (digitTimeout.HasValue ? digitTimeout.ToString() : ""), true);
            string ret = ev[var];
            SetVariable(var, "");
            if (ret != null)
            {
                if (ret.Length < minDigits)
                    ret = null;
                else if (ret == "")
                    ret = null;
            }
            return ret;
        }
        #endregion
        private SocketEvent ExecuteApplication(string applicationName, bool eventLock)
        {
            return ExecuteApplication(applicationName, null, eventLock);
        }

        private SocketEvent ExecuteApplication(string applicationName, string applicationArguements, bool eventLock) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("sendmsg");
            if (eventLock)
                sb.AppendLine("event-lock: true");
            sb.AppendLine("call-command: execute");
            sb.AppendLine("execute-app-name: " + applicationName);
            if (applicationArguements != null)
                sb.AppendLine("execute-app-arg: " + applicationArguements);
            sb.AppendLine("");
            ManualResetEvent mreComm = new ManualResetEvent(false);
            SocketEvent ret = null;
            lock (_awaitingCommands)
            {
                _awaitingCommands.Enqueue(mreComm);
            }
            _sendCommand(sb.ToString());
            if (eventLock)
            {
                lock (_awaitingCommand)
                {
                    _awaitingCommand = applicationName + " " + (applicationArguements == null ? "" : applicationArguements);
                }
            }
            mreComm.WaitOne();
            if (eventLock)
            {
                _awaitingCommandEvent.WaitOne();
                lock (_awaitingCommand)
                {
                    _awaitingCommand = "";
                    ret = _currentEvent;
                    _currentEvent = null;
                    _awaitingCommandEvent.Reset();
                }
            }
            return ret;
        }
        #endregion

        #region Variables
        public string this[string name]{
            get
            {
                string ret = null;
                lock (_properties)
                {
                    if (_properties.ContainsKey(name))
                        ret = _properties[name];
                }
                return ret;
            }
            set
            {
                lock (_properties)
                {
                    _properties.Remove(name);
                    if (value != null)
                    {
                        SetVariable(name, value);
                        _properties.Add(name, value);
                    }
                    else
                        SetVariable(name, "");
                }
            }
        }

        public string UUID
        {
            get { return this["Unique-ID"]; }
        }

        public string CallerUUID
        {
            get { return this["Caller-Unique-ID"]; }
        }

        public string DestinationNumber
        {
            get { return this["Channel-Destination-Number"]; }
            set { this["Channel-Destination-Number"] = value; }
        }

        public string Domain
        {
            get { return this["variable_domain_name"]; }
            set { 
                this["variable_domain_name"] = value; 
            }
        }

        public string Context
        {
            get { return this["variable_user_context"]; }
            set { this["variable_user_context"] = value; }
        }

        public string ChannelName
        {
            get { return this["Channel-Name"]; }
        }

        private string _baseDir;
        public string BASE_DIR
        {
            get { return _baseDir; }
            set { _baseDir = value; }
        }
        #endregion

        protected override void _processMessageQueue(Queue<ASocketMessage> messages)
        {
            while (messages.Count > 0)
            {
                ASocketMessage asm = messages.Dequeue();
                if (asm is CommandReplyMessage)
                {
                    if (asm["Job-UUID"] == null)
                    {
                        ManualResetEvent mre = null;
                        Monitor.Enter(_awaitingCommands);
                        if (_awaitingCommands.Count > 0)
                            mre = _awaitingCommands.Dequeue();
                        Monitor.Exit(_awaitingCommands);
                        if (mre != null)
                            mre.Set();
                    }
                }
                else if (asm is SocketEvent)
                {
                    SocketEvent se = (SocketEvent)asm;
                    lock (_properties)
                    {
                        se.CopyParameters(ref _properties);
                    }
                    if (se.EventName == EXECUTE_COMPLETE_EVENT_NAME)
                    {
                        if ((se.CallerUUID == CallerUUID) && (se.ChannelName == ChannelName))
                        {
                            lock (_awaitingCommand)
                            {
                                if (_awaitingCommand == se[APP_NAME_VARIABLE_NAME] + " " + (se[APP_DATA_VARIABLE_NAME] == null ? "" : se[APP_DATA_VARIABLE_NAME]))
                                {
                                    _currentEvent = se;
                                    _awaitingCommandEvent.Set();
                                }
                            }
                        }
                    }
                    else if (se.EventName == CHANNEL_END_EVENT_NAME)
                    {
                        if ((se.CallerUUID == CallerUUID) && (se.ChannelName == ChannelName))
                        {
                            _isHungup = true;
                            _currentEvent = se;
                            _awaitingCommandEvent.Set();
                            Close();
                        }
                    }
                }
            }
        }

        protected override void _close()
        {
        }

        #region INIT
        protected override void _preSocketReady()
        {
            BufferedStream _in = new BufferedStream(new NetworkStream(socket));
            _sendCommand("connect");
            _properties = ASocketMessage.ParseProperties(ReadMessage(_in));
            string[] keys = new string[_properties.Count];
            _properties.Keys.CopyTo(keys,0);
            foreach (string str in keys)
            {
                string val = _properties[str];
                _properties.Remove(str);
                _properties.Add(str, Uri.UnescapeDataString(val));
            }
            _sendCommand("linger");
            ReadMessage(_in);
            _sendCommand("api strftime %Y-%m-%d-%H-%M");
            string[] split = new CommandReplyMessage(ReadMessage(_in)).ReplyMessage.Split('-');
            _startTime = new DateTime(int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]), int.Parse(split[3]), int.Parse(split[4]), 0);
            RegisterEvent(CHANNEL_END_EVENT_NAME);
            RegisterEvent(EXECUTE_COMPLETE_EVENT_NAME);
        }

        private string ReadMessage(BufferedStream _in)
        {
            string ret = "";
            string line;
            while ((line = streamReadLine(_in)) != null)
            {
                ret += line + "\n";
                if (ret.ToString().EndsWith("\n\n"))
                    break;
            }
            if (ret.Contains("Content-Length:"))
            {
                int conLen = int.Parse(ASocketMessage.ParseProperties(ret)["Content-Length"]);
                for (int x = 0; x < conLen; x++)
                {
                    ret += Convert.ToChar(_in.ReadByte());
                }
            }
            return ret.Trim();
        }

        private string streamReadLine(BufferedStream _in)
        {
            int next_char;
            string data = "";
            while (true)
            {
                next_char = _in.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }
            return data;
        }
        #endregion
    }
}
