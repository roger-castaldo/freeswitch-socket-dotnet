using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using Org.Reddragonit.FreeSwitch.Sockets;

namespace Org.Reddragonit.FreeSwitchSockets.MonoFix
{
    internal class WrappedTcpListener
    {
        private TcpListener _listener;
        private bool _override;
        private ManualResetEvent _waitHandle;
        private bool _closed=false;
        private Thread _thread;
        private ManualResetEvent _resBeginAccept;
        private AsyncCallback _callBack;
        private WrappedTcpListenerAsyncResult _result;
        private bool _accepting;
        private bool _acceptSocket;

        public WrappedTcpListener(TcpListener listener)
        {
            _listener = listener;
            if (Constants.MonoVersion == null)
                _override = false;
            else
                _override = Constants.MonoVersion < Constants._OVERRIDE_VERSION;
            if (_override){
                _closed = false;
                _waitHandle = new ManualResetEvent(false);
                _resBeginAccept = new ManualResetEvent(false);
                _thread = new Thread(new ThreadStart(_BackgroundAccept));
                _thread.IsBackground = true;
            }
        }

        private void _BackgroundAccept()
        {
            while (!_closed)
            {
                _resBeginAccept.WaitOne();
                _resBeginAccept.Reset();
                if (!_closed)
                {
                    Socket _socket = null;
                    TcpClient _client = null;
                    try
                    {
                        _accepting = true;
                        if (_acceptSocket)
                            _socket = _listener.AcceptSocket();
                        else
                            _client = _listener.AcceptTcpClient();
                        _accepting = false;
                    }
                    catch (Exception e)
                    {
                        _socket = null;
                    }
                    if (!_closed)
                    {
                        if (_acceptSocket)
                            _result.Complete(_socket);
                        else
                            _result.Complete(_client);
                        _waitHandle.Set();
                        ThreadPool.QueueUserWorkItem(new WaitCallback(_ProcCallBack), new object[] { _callBack, _result });
                    }
                }
            }
        }

        private void _ProcCallBack(object obj)
        {
            AsyncCallback callBack = (AsyncCallback)((object[])obj)[0];
            WrappedTcpListenerAsyncResult result = (WrappedTcpListenerAsyncResult)((object[])obj)[1];
            try
            {
                if (callBack != null)
                    callBack.Invoke(result);
            }
            catch (Exception e) { }
        }

        public void Start(int backlog)
        {
            _listener.Start(backlog);
        }

        public void Start()
        {
            _listener.Start();
        }

        public IAsyncResult BeginAcceptSocket(AsyncCallback callback,object state)
        {
            if (!_override)
                return _listener.BeginAcceptSocket(callback, state);
            else
            {
                if (_result != null)
                    throw new Exception("Unable to BeginAcceptSocket, already asynchronously waiting");
                _acceptSocket = true;
                _callBack = callback;
                _result = new WrappedTcpListenerAsyncResult(state, _waitHandle);
                if ((int)(_thread.ThreadState & ThreadState.Unstarted) == (int)ThreadState.Unstarted)
                    _thread.Start();
                _resBeginAccept.Set();
                return _result;
            }
        }

        public Socket EndAcceptSocket(IAsyncResult asyncResult)
        {
            if (!_override)
                return _listener.EndAcceptSocket(asyncResult);
            else
            {
                if (asyncResult==null)
                    throw new Exception("Unable to process null asyncResult");
                _result = null;
                return ((WrappedTcpListenerAsyncResult)asyncResult).Socket;
            }
        }

        public IAsyncResult BeginAcceptTcpClient(AsyncCallback callback, object state)
        {
            if (!_override)
                return _listener.BeginAcceptTcpClient(callback, state);
            else
            {
                if (_result != null)
                    throw new Exception("Unable to BeginAcceptTcpClient, already asynchronously waiting");
                _acceptSocket = false;
                _callBack = callback;
                _result = new WrappedTcpListenerAsyncResult(state, _waitHandle);
                if ((int)(_thread.ThreadState & ThreadState.Unstarted) == (int)ThreadState.Unstarted)
                    _thread.Start();
                _resBeginAccept.Set();
                return _result;
            }
        }

        public TcpClient EndAcceptTcpClient(IAsyncResult asyncResult)
        {
            if (!_override)
                return _listener.EndAcceptTcpClient(asyncResult);
            else
            {
                if (asyncResult==null)
                    throw new Exception("Unable to process null asyncResult");
                _result = null;
                return ((WrappedTcpListenerAsyncResult)asyncResult).Client;
            }
        }

        public void Stop()
        {
            if (_override)
            {
                _closed = true;
                if (!_accepting)
                    _resBeginAccept.Set();
                else
                {
                    try
                    {
                        _thread.Abort();
                    }
                    catch (Exception e) { }
                }
            }
            _listener.Stop();
        }
    }
}
