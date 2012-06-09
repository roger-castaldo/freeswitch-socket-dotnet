using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;

namespace Org.Reddragonit.FreeSwitchSockets.MonoFix
{
    internal class WrappedTcpListenerAsyncResult : IAsyncResult
    {
        private Socket _socket;
        public Socket Socket
        {
            get { return _socket; }
        }

        private TcpClient _client;
        public TcpClient Client
        {
            get { return _client; }
        }

        private AsyncCallback _callBack;
        public AsyncCallback CallBack
        {
            get { return _callBack; }
        }

        private bool _acceptSocket;
        public bool AcceptSocket
        {
            get { return _acceptSocket; }
        }

        public WrappedTcpListenerAsyncResult(object asyncState, WaitHandle waitHandle, AsyncCallback callback,bool acceptSocket)
        {
            _asyncState = asyncState;
            _waitHandle = waitHandle;
            _callBack = callback;
            _acceptSocket = acceptSocket;
        }

        internal void CompleteSynchronously()
        {
            _completedSynchronously = true;
            _isCompleted = true;
        }

        internal void Complete(Socket socket)
        {
            _isCompleted = true;
            _socket = socket;
        }

        internal void Complete(TcpClient client)
        {
            _isCompleted = true;
            _client = client;
        }

        #region IAsyncResult Members

        private object _asyncState;
        public object AsyncState
        {
            get { return _asyncState; }
        }

        private WaitHandle _waitHandle;
        public WaitHandle AsyncWaitHandle
        {
            get { return _waitHandle; }
        }

        private bool _completedSynchronously;
        public bool CompletedSynchronously
        {
            get { return _completedSynchronously; }
        }

        private bool _isCompleted;
        public bool IsCompleted
        {
            get { return _isCompleted; }
        }

        #endregion
    }
}