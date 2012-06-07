using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Org.Reddragonit.FreeSwitchSockets.MonoFix
{
    internal class WrappedTcpListenerAsyncResult : IAsyncResult
    {
        public WrappedTcpListenerAsyncResult(object asyncState, WaitHandle waitHandle)
        {
            _asyncState = asyncState;
        }

        internal void CompleteSynchronously()
        {
            _completedSynchronously = true;
            _isCompleted = true;
        }

        internal void Complete()
        {
            _isCompleted = true;
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