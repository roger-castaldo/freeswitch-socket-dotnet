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
        private Thread _thread = null;
        private bool _closed;

        public WrappedTcpListener(TcpListener listener)
        {
            _listener = listener;
            if (Constants.MonoVersion == null)
                _override = false;
            else
                _override = Constants.MonoVersion < Constants._OVERRIDE_VERSION;
            if (_override)
            {
                _closed = false;
                _waitHandle = new ManualResetEvent(false);
            }
        }

        private void _BackgroundAccept(object obj)
        {
            WrappedTcpListenerAsyncResult result = (WrappedTcpListenerAsyncResult)obj;
            Socket socket = null;
            TcpClient client = null;
            try
            {
                if (result.AcceptSocket)
                    socket = _listener.AcceptSocket();
                else
                    client = _listener.AcceptTcpClient();
            }
            catch (ThreadAbortException tae)
            {
                result = null;
                throw tae;
            }
            catch (Exception e)
            {
                socket = null;
                client = null;
            }
            if (!_closed)
            {
                if (result.AcceptSocket)
                    result.Complete(socket);
                else
                    result.Complete(client);
                _waitHandle.Set();
                ThreadPool.QueueUserWorkItem(new WaitCallback(_ProcCallBack), result);
            }
        }

        private void _ProcCallBack(object obj)
        {
            if ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped)
            {
                try
                {
                    _thread.Join();
                }
                catch (Exception e) { }
            }
            WrappedTcpListenerAsyncResult result = (WrappedTcpListenerAsyncResult)obj;
            try
            {
                if (result.CallBack != null)
                    result.CallBack(result);
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

        public IAsyncResult BeginAcceptSocket(AsyncCallback callback, object state)
        {
            if (!_override)
                return _listener.BeginAcceptSocket(callback, state);
            else
            {
                if (_thread != null)
                {
                    if (((int)(_thread.ThreadState & ThreadState.Unstarted) != (int)ThreadState.Unstarted)
                        && ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped))
                        throw new Exception("Unable to begin Accepting Socket, already asynchronously waiting");
                }
                WrappedTcpListenerAsyncResult result = new WrappedTcpListenerAsyncResult(state, _waitHandle, callback, true);
                _thread = new Thread(new ParameterizedThreadStart(_BackgroundAccept));
                _thread.IsBackground = true;
                _thread.Start(result);
                return result;
            }
        }

        public Socket EndAcceptSocket(IAsyncResult asyncResult)
        {
            if (!_override)
                return _listener.EndAcceptSocket(asyncResult);
            else
            {
                if (asyncResult == null)
                    throw new Exception("Unable to handle null async result.");
                WrappedTcpListenerAsyncResult result = (WrappedTcpListenerAsyncResult)asyncResult;
                Socket ret = result.Socket;
                result = null;
                return ret;
            }
        }

        public IAsyncResult BeginAcceptTcpClient(AsyncCallback callback, object state)
        {
            if (!_override)
                return _listener.BeginAcceptSocket(callback, state);
            else
            {
                if (_thread != null)
                {
                    if (((int)(_thread.ThreadState & ThreadState.Unstarted) != (int)ThreadState.Unstarted)
                        && ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped))
                        throw new Exception("Unable to begin Accepting Socket, already asynchronously waiting");
                }
                WrappedTcpListenerAsyncResult result = new WrappedTcpListenerAsyncResult(state, _waitHandle, callback, false);
                _thread = new Thread(new ParameterizedThreadStart(_BackgroundAccept));
                _thread.IsBackground = true;
                _thread.Start(result);
                return result;
            }
        }

        public TcpClient EndAcceptTcpClient(IAsyncResult asyncResult)
        {
            if (!_override)
                return _listener.EndAcceptTcpClient(asyncResult);
            else
            {
                if (asyncResult == null)
                    throw new Exception("Unable to handle null async result.");
                WrappedTcpListenerAsyncResult result = (WrappedTcpListenerAsyncResult)asyncResult;
                TcpClient ret = result.Client;
                result = null;
                return ret;
            }
        }

        public void Stop()
        {
            if (_override)
            {
                _closed = true;
                if (_thread != null)
                {
                    if (((int)(_thread.ThreadState & ThreadState.Unstarted) != (int)ThreadState.Unstarted)
                        && ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped))
                    {
                        try
                        {
                            _thread.Abort();
                        }
                        catch (Exception e) { }
                    }
                    _thread = null;
                }
            }
            _listener.Stop();
        }
    }
}
