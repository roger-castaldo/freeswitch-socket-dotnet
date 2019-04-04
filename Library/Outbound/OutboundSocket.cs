using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using Sockets = System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Net;
using Org.Reddragonit.FreeSwitchSockets.Messages;

namespace Org.Reddragonit.FreeSwitchSockets.Outbound
{
    public class OutboundSocket : ASocket
    {
        private const string RELOAD_CONFIGS_COMMAND = "reloadxml";
        public const string DEFAULT_EVENT_SOCKET_LISTEN_IP = "127.0.0.1";
        public const int DEFAULT_EVENT_SOCKET_LISTEN_PORT = 8021;
        public const string DEFAULT_EVENT_SOCKET_PASSWORD = "ClueCon";
        private const string AUTH_COMMAND = "auth {0}\n\n";

        private struct sCommand
        {
            long _id;
            public long ID
            {
                get { return _id; }
            }

            private string _command;
            public string Command
            {
                get { return _command; }
            }

            private bool _api;
            public bool API
            {
                get { return _api; }
            }

            public sCommand(string command, bool api,MT19937 random)
            {
                _id = random.NextLong();
                _api = api;
                _command = command;
            }
        }

        public delegate void delProcessLogMessage(SocketLogMessage message);
        public delegate void delReloadXml();
        private bool _exit = false;
        
        private delProcessLogMessage _logDelegate;
        private delProcessEventMessage _eventDelegate;
        private delReloadXml _preReloadCall;
        private delReloadXml _postReloadCall;
        private string _password;

        public OutboundSocket(IPAddress ip,int port,string password,delProcessEventMessage eventDelegate,delProcessLogMessage logDelegate,delReloadXml preReloadCall,delReloadXml postReloadCall)
            : base(ip,port)
        {
            _eventDelegate = eventDelegate;
            _logDelegate = logDelegate;
            _preReloadCall = preReloadCall;
            _postReloadCall = postReloadCall;
            _password = password;
        }

        public void IssueCommand(string command,out string response)
        {
            _IssueAPICommand(command,out response);
        }

        public void IssueBackgroundAPICommand(string command)
        {
            _IssueBackgroundAPICommand(command);
        }

        public void RegisterCustomEventCallBack(string eventName)
        {
            RegisterEvent("CUSTOM." + eventName);
        }

        public void ReloadConfigs()
        {
            if (_preReloadCall != null)
                _preReloadCall.Invoke();
            string tmp;
            IssueCommand(RELOAD_CONFIGS_COMMAND,out tmp);
            if (_postReloadCall != null)
                _postReloadCall.Invoke();
        }

        protected override void _processMessageQueue(Queue<ASocketMessage> messages)
        {
            while (messages.Count > 0)
            {
                ASocketMessage asm = messages.Dequeue();
                if (asm is AuthenticationRequestMessage)
                {
                    socket.Send(ASCIIEncoding.ASCII.GetBytes(string.Format(AUTH_COMMAND, _password)));
                    IsConnected = true;
                    lock (_awaitingCommands)
                    {
                        if (!_exit)
                        {
                            while (_awaitingCommands.Count > 0)
                            {
                                byte[] commandBytes = _awaitingCommands.Dequeue();
                                socket.Send(commandBytes, 0, commandBytes.Length, SocketFlags.None);
                            }
                        }
                    }
                }else
                    new Thread(new ParameterizedThreadStart(_processMessage)).Start(asm);
            }
        }

        private void _processMessage(object obj)
        {
            ASocketMessage asm = (ASocketMessage)obj;
            if (asm is SocketEvent)
            {
                if (_eventDelegate != null)
                    _eventDelegate.Invoke((SocketEvent)asm);
            }
            else if (asm is SocketLogMessage)
            {
                if (_logDelegate != null)
                    _logDelegate.Invoke((SocketLogMessage)asm);
            }
        }

        protected override void _close()
        {
            _exit = true;
        }

        protected override void _preSocketReady()
        {
            
        }
    }
}
