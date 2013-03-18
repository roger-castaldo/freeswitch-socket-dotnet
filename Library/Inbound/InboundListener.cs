using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Org.Reddragonit.FreeSwitchSockets.MonoFix;

namespace Org.Reddragonit.FreeSwitchSockets.Inbound
{
    public class InboundListener
    {
        private IPAddress _ip;
        public IPAddress IP
        {
            get { return _ip; }
        }

        private int _port;
        public int Port
        {
            get { return _port; }
        }

        private ASocket.delDisposeInvalidMessage _disposeInvalidMessage;
        public ASocket.delDisposeInvalidMessage DisposeInvalidMessage
        {
            get { return _disposeInvalidMessage; }
            set { _disposeInvalidMessage = value; }
        }

        public delegate void delProcessConnection(InboundConnection conn);

        private WrappedTcpListener _listener;
        private delProcessConnection _connectionProcessor;

        public InboundListener(IPAddress ip,int port,delProcessConnection connectionProcessor)
        {
            _ip = ip;
            _port = port;
            _listener = new WrappedTcpListener(new TcpListener(ip, port));
            _listener.Start();
            _listener.BeginAcceptSocket(new AsyncCallback(RecieveClient), null);
            _connectionProcessor = connectionProcessor;
            if (_connectionProcessor == null)
                throw new Exception("Unable to construct inbound listener without providing a call back to process connections.");
        }

        private void RecieveClient(IAsyncResult res)
        {
            TcpClient clnt = null;
            try
            {
                clnt = _listener.EndAcceptTcpClient(res);
            }
            catch (Exception e)
            {
                clnt = null;
            }
            try
            {
                _listener.BeginAcceptTcpClient(new AsyncCallback(RecieveClient), null);
            }
            catch (Exception e)
            {
            }
            if (clnt != null)
            {
                InboundConnection conn = new InboundConnection(clnt);
                conn.DisposeInvalidMessage = _disposeInvalidMessage;
                _connectionProcessor.Invoke(conn);
            }
        }

        public void Stop()
        {
            try
            {
                _listener.EndAcceptSocket(null);
            }
            catch (Exception e)
            {
            }
            try
            {
                _listener.Stop();
            }
            catch (Exception e)
            {
            }
        }
    }
}
