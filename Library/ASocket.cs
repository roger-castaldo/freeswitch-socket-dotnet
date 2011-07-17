using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using Org.Reddragonit.FreeSwitchSockets.Messages;
using System.Threading;
using System.Net;
using Org.Reddragonit.FreeSwitchSockets.Outbound;
using System.Xml;
using System.IO;

namespace Org.Reddragonit.FreeSwitchSockets
{
    public abstract class ASocket
    {
        private struct sEventHandler{
            private string _eventName;
            public string EventName{
                get{return _eventName;}
            }

            private string _uuid;
            public string UUID{
                get{return _uuid;}
            }

            private string _callerUUID;
            public string CallerUUID
            {
                get { return _callerUUID; }
            }

            private string _channelName;
            public string ChannelName
            {
                get { return _channelName; }
            }

            private delProcessEventMessage _handler;
            public delProcessEventMessage Handler
            {
                get { return _handler; }
            }

            private long _id;
            public long ID
            {
                get { return _id; }
            }

            public sEventHandler(string eventName, string uuid,string callerUUID,string channelName, delProcessEventMessage handler,long id)
            {
                _eventName = eventName;
                _uuid = uuid;
                _callerUUID = callerUUID;
                _channelName = channelName;
                _handler = handler;
                _id = id;
            }

            public bool HandlesEvent(SocketEvent Event){
                return ASocket.StringsEqual((EventName == null ? Event.EventName : EventName), Event.EventName) &&
                    ASocket.StringsEqual((UUID == null ? Event.UniqueID : UUID), Event.UniqueID) &&
                    ASocket.StringsEqual((ChannelName == null ? Event.ChannelName : ChannelName), Event.ChannelName) &&
                    ASocket.StringsEqual((CallerUUID == null ? Event.CallerUUID : CallerUUID), Event.CallerUUID);
            }
        }

        internal static bool StringsEqual(string str1, string str2)
        {
            if ((str1 == null) && (str2 != null))
                return false;
            else if ((str1 != null) && (str2 == null))
                return false;
            else if ((str1 == null) && (str2 == null))
                return true;
            else
                return str1.Equals(str2);
        }

        private const int BUFFER_SIZE = 500;
        private const string MESSAGE_END_STRING = "\n\n";
        private const string REGISTER_EVENT_COMMAND = "event {0}";
        private const string REMOVE_EVENT_COMMAND = "nixevent {0}";
        private const string AUTH_COMMAND = "auth {0}";
        private const string BACKGROUND_API_RESPONSE_EVENT = "SWITCH_EVENT_BACKGROUND_JOB";
        private const string API_ISSUE_COMMAND = "bgapi {0}";

        public delegate void delProcessEventMessage(SocketEvent message);

        private Socket _socket;
        protected Socket socket
        {
            get { return _socket; }
        }

        private MT19937 _random = new MT19937(DateTime.Now.Ticks);
        protected MT19937 Random
        {
            get { return _random; }
        }

        private bool _isConnected = false;
        protected bool IsConnected
        {
            get { return _isConnected; }
        }

        private FreeSwitchLogLevels _currentLevel = FreeSwitchLogLevels.CONSOLE;
        public FreeSwitchLogLevels LogLevel
        {
            get { return _currentLevel; }
            set
            {
                if ((int)value > (int)_currentLevel)
                {
                    _sendCommand("log " + value.ToString());
                    _currentLevel = value;
                }
            }
        }

        private string _textReceived;
        private byte[] buffer;
        private List<sEventHandler> _handlers;
        private Queue<byte[]> _awaitingCommands;
        private bool _exit = false;
        private IPAddress _ipAddress;
        private int _port;
        private string _password;
        private string _currentCommandID;
        private delProcessEventMessage _eventProcessor;
        private Queue<ManualResetEvent> _awaitingCommandsEvents;
        private Dictionary<string, ManualResetEvent> _commandThreads;
        private Dictionary<string, string> _awaitingCommandReturns;

        protected ASocket(Socket socket)
        {
            _awaitingCommandsEvents = new Queue<ManualResetEvent>();
            _awaitingCommandReturns = new Dictionary<string, string>();
            _commandThreads = new Dictionary<string, ManualResetEvent>();
            _awaitingCommandReturns = new Dictionary<string, string>();
            _eventProcessor = new delProcessEventMessage(ProcessEvent);
            _handlers = new List<sEventHandler>();
            _socket = socket;
            _isConnected = _socket.Connected;
            if (!_isConnected)
                throw new Exception("Unable to construct an instance of the abstract class ASocket using the contructor with a socket without passing a connected socket.");
            _ipAddress = ((IPEndPoint)_socket.RemoteEndPoint).Address;
            _port = ((IPEndPoint)_socket.RemoteEndPoint).Port;
            _preSocketReady();
            buffer = new byte[BUFFER_SIZE];
            _socket.BeginReceive(buffer, 0, BUFFER_SIZE,
                                 SocketFlags.None, new AsyncCallback(ReceiveCallback),
                                 null);
            this.RegisterEvent(BACKGROUND_API_RESPONSE_EVENT);
        }

        protected string _IssueAPICommand(string command, bool api)
        {
            ManualResetEvent mre = new ManualResetEvent(false);
            lock (_awaitingCommandsEvents)
            {
                _awaitingCommandsEvents.Enqueue(mre);
            }
            string comID="";
            _sendCommand(string.Format(API_ISSUE_COMMAND, command));
            mre.WaitOne();
            if (api)
            {
                lock (_commandThreads)
                {
                    mre = new ManualResetEvent(false);
                    _commandThreads.Add(_currentCommandID, mre);
                    comID = _currentCommandID;
                }
                string ret = "";
                mre.WaitOne();
                lock (_awaitingCommandReturns)
                {
                    ret = _awaitingCommandReturns[comID];
                    _awaitingCommandReturns.Remove(comID);
                }
                return ret.Trim('\n');
            }
            return "";
        }

        protected ASocket(IPAddress ip, int port,string password)
        {
            _eventProcessor = new delProcessEventMessage(ProcessEvent);
            _exit = false;
            _isConnected = false;
            _ipAddress = ip;
            _port = port;
            _password = password;
            _awaitingCommands = new Queue<byte[]>();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _preSocketReady();
            Thread th = new Thread(new ThreadStart(BackgroundRun));
            th.Start();
        }

        private void BackgroundRun()
        {
            while (!_exit)
            {
                if (_isConnected == false)
                {
                    try
                    {
                        _socket.Connect(_ipAddress, _port);
                        byte[] data = ASCIIEncoding.ASCII.GetBytes(string.Format(AUTH_COMMAND, _password) + MESSAGE_END_STRING);
                        _socket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendComplete), null);
                        buffer = new byte[BUFFER_SIZE];
                        _socket.BeginReceive(buffer, 0, BUFFER_SIZE,
                                             SocketFlags.None, new AsyncCallback(ReceiveCallback),
                                             null);
                        _isConnected = true;
                    }
                    catch (Exception e)
                    {
                    }
                    if (!_isConnected)
                        Thread.Sleep(100);
                    else
                        break;
                }
            }
            if (!_exit)
            {
                lock (_awaitingCommands)
                {
                    while (_awaitingCommands.Count > 0)
                        _sendCommand(_awaitingCommands.Dequeue());
                }
            }
        }

        protected void _sendCommand(string commandString)
        {
            if (!commandString.EndsWith(MESSAGE_END_STRING))
                commandString += MESSAGE_END_STRING;
            _sendCommand(System.Text.ASCIIEncoding.ASCII.GetBytes(commandString));
        }

        protected void _sendCommand(byte[] commandBytes)
        {
            if (!_isConnected)
            {
                lock (_awaitingCommands)
                {
                    if (_isConnected)
                        _socket.BeginSend(commandBytes, 0, commandBytes.Length, SocketFlags.None, new AsyncCallback(SendComplete), null);
                    else
                        _awaitingCommands.Enqueue(commandBytes);
                }
            }
            else
            {
                _socket.BeginSend(commandBytes, 0, commandBytes.Length, SocketFlags.None, new AsyncCallback(SendComplete), null);
            }
        }

        public void Close()
        {
            _exit = true;
            _close();
            _sendCommand("exit");
            try
            {
                _socket.Disconnect(false);
                _socket.Close();
            }
            catch (Exception e)
            {
            }
        }

        public long RegisterEventHandler(string eventName, string uuid, string callerUUID, string channelName, delProcessEventMessage handler)
        {
            long id = _random.NextLong();
            lock (_handlers)
            {
                _handlers.Add(new sEventHandler(eventName, uuid, callerUUID, channelName, handler, id));
            }
            return id;
        }

        public void UnRegisterEventHandler(long id)
        {
            lock (_handlers)
            {
                for (int x = 0; x < _handlers.Count; x++)
                {
                    if (_handlers[x].ID == id)
                    {
                        _handlers.RemoveAt(x);
                        break;
                    }
                }
            }
        }

        public void RegisterEvent(string eventName)
        {
            _sendCommand(string.Format(REGISTER_EVENT_COMMAND, eventName));
        }

        public void UnRegister(string eventName)
        {
            _sendCommand(string.Format(REMOVE_EVENT_COMMAND, eventName));
        }

        protected abstract void _processMessageQueue(Queue<ASocketMessage> messages);
        protected abstract void _close();
        protected abstract void _preSocketReady();

        private void SendComplete(IAsyncResult ar)
        {
            try
            {
                _socket.EndSend(ar);
            }
            catch (Exception e) { }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                int bytesRead = _socket.EndReceive(ar);
                Queue<ASocketMessage> msgs = new Queue<ASocketMessage>();
                if (bytesRead > 0)
                {
                    _textReceived += ASCIIEncoding.ASCII.GetString(buffer, 0, bytesRead);
                    _textReceived = _textReceived.TrimStart('\n');
                    while (_textReceived.Contains("Content-Type:"))
                    {
                        string origMsg = _textReceived.Substring(0, _textReceived.IndexOf(MESSAGE_END_STRING)).TrimEnd('\n');
                        Dictionary<string, string> pars = ASocketMessage.ParseProperties(origMsg);
                        _textReceived = _textReceived.Substring(_textReceived.IndexOf(MESSAGE_END_STRING)).TrimStart('\n');
                        string subMsg = "";
                        if (pars.ContainsKey("Content-Length"))
                        {
                            int msgLen = int.Parse(pars["Content-Length"]);
                            while (_textReceived.Length < msgLen)
                            {
                                buffer = new byte[msgLen - _textReceived.Length];
                                _socket.Receive(buffer, msgLen - _textReceived.Length, SocketFlags.None);
                                _textReceived += System.Text.ASCIIEncoding.ASCII.GetString(buffer);
                            }
                            subMsg = _textReceived.Substring(0, msgLen);
                            _textReceived = _textReceived.Substring(subMsg.Length).TrimStart('\n');
                        }
                        if (pars["Content-Type"] == "text/event-plain")
                        {
                            SocketEvent se;
                            if (subMsg.Contains(MESSAGE_END_STRING))
                                se = new SocketEvent(subMsg.Substring(0, subMsg.IndexOf(MESSAGE_END_STRING)));
                            else
                                se = new SocketEvent(subMsg);
                            if (se.EventName == "BACKGROUND_JOB")
                            {
                                if (se["Content-Length"] != null)
                                {
                                    string eventMsg = subMsg.Substring(subMsg.IndexOf(MESSAGE_END_STRING) + MESSAGE_END_STRING.Length);
                                    int msgLen = int.Parse(se["Content-Length"]);
                                    if (eventMsg.Length < msgLen)
                                    {
                                        if (_textReceived.Length > 0)
                                        {
                                            if (_textReceived.Length > msgLen - eventMsg.Length)
                                                eventMsg += _textReceived.Substring(0, msgLen - eventMsg.Length);
                                            else
                                            {
                                                eventMsg += _textReceived;
                                                _textReceived = "";
                                            }
                                        }
                                        if (eventMsg.Length < msgLen)
                                        {
                                            buffer = new byte[msgLen - eventMsg.Length];
                                            _socket.Receive(buffer, msgLen - eventMsg.Length, SocketFlags.None);
                                            eventMsg += ASCIIEncoding.ASCII.GetString(buffer);
                                        }
                                    }
                                    se.Message = eventMsg;
                                }
                                lock (_commandThreads)
                                {
                                    if (_commandThreads.ContainsKey(se["Job-UUID"]))
                                    {
                                        lock (_awaitingCommandReturns)
                                        {
                                            _awaitingCommandReturns.Add(se["Job-UUID"], se.Message.Trim('\n'));
                                        }
                                        ManualResetEvent mre = _commandThreads[se["Job-UUID"]];
                                        _commandThreads.Remove(se["Job-UUID"]);
                                        mre.Set();
                                    }
                                }
                            }
                            msgs.Enqueue(se);
                        }
                        else if (pars["Content-Type"] == "command/reply")
                        {
                            CommandReplyMessage crm = new CommandReplyMessage(origMsg, subMsg);
                            msgs.Enqueue(crm);
                            if (crm["Job-UUID"] != null)
                            {
                                lock (_awaitingCommandsEvents)
                                {
                                    _currentCommandID = crm["Job-UUID"];
                                    _awaitingCommandsEvents.Dequeue().Set();
                                }
                            }
                        }
                        else if (pars["Content-Type"] == "log/data")
                        {
                            SocketLogMessage lg;
                            if (subMsg.Contains(MESSAGE_END_STRING))
                                lg = new SocketLogMessage(subMsg.Substring(0, subMsg.IndexOf(MESSAGE_END_STRING)));
                            else
                                lg = new SocketLogMessage(subMsg);
                            string eventMsg = subMsg.Substring(subMsg.IndexOf(MESSAGE_END_STRING) + MESSAGE_END_STRING.Length);
                            int msgLen = int.Parse(lg.ContentLength);
                            if (eventMsg.Length < msgLen)
                            {
                                if (_textReceived.Length > 0)
                                {
                                    if (_textReceived.Length > msgLen - eventMsg.Length)
                                        eventMsg += _textReceived.Substring(0, msgLen - eventMsg.Length);
                                    else
                                    {
                                        eventMsg += _textReceived;
                                        _textReceived = "";
                                    }
                                }
                                if (eventMsg.Length < msgLen)
                                {
                                    buffer = new byte[msgLen - eventMsg.Length];
                                    _socket.Receive(buffer, msgLen - eventMsg.Length, SocketFlags.None);
                                    eventMsg += ASCIIEncoding.ASCII.GetString(buffer);
                                }
                            }
                            lg.FullMessage = eventMsg;
                            msgs.Enqueue(lg);
                        }
                        else if (_textReceived.Contains(MESSAGE_END_STRING))
                            _textReceived = _textReceived.Substring(_textReceived.IndexOf(MESSAGE_END_STRING) + MESSAGE_END_STRING.Length);
                    }
                }
                buffer = new byte[BUFFER_SIZE];
                _socket.BeginReceive(buffer, 0, BUFFER_SIZE,
                                     SocketFlags.None, new AsyncCallback(ReceiveCallback),
                                     null);
                if (msgs.Count > 0)
                    _processMessageQueue(msgs);
            }
            catch (Exception e)
            {
            }
        }

        private void ProcessEvent(SocketEvent message)
        {
            sEventHandler[] handlers;
            lock (_handlers)
            {
                handlers = new sEventHandler[_handlers.Count];
                _handlers.CopyTo(handlers, 0);
            }
            foreach (sEventHandler eh in handlers)
            {
                if (eh.HandlesEvent(message))
                    eh.Handler.BeginInvoke(message, new AsyncCallback(ProcessingComplete), this);
            }
        }

        private void ProcessingComplete(IAsyncResult res)
        {
        }
    }
}
