﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using H.Hooks.Core.Interop;
using H.Hooks.Core.Interop.WinUser;

namespace H.Hooks
{
    public abstract class Hook : IDisposable
    {
        public static Keys FromString(string text) => Enum.TryParse<Keys>(text, true, out var result) ? result : Keys.None;

        #region Properties

        public string Name { get; }
        public bool IsStarted { get; private set; }

        private IntPtr HookHandle { get; set; }
        private HookProc? HookAction { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<Exception>? ExceptionOccurred;

        private void OnExceptionOccurred(Exception value)
        {
            ExceptionOccurred?.Invoke(this, value);
        }

        #endregion

        #region Constructors

        protected Hook(string name)
        {
            Name = name;
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Start hook process
        /// </summary>
        /// <exception cref="Win32Exception">If SetWindowsHookEx return error code</exception>
        internal void Start(HookProcedureType type)
        {
            if (IsStarted)
            {
                return;
            }

            Trace.WriteLine($"Starting hook '{Name}'...", $"Hook.StartHook [{Thread.CurrentThread.Name}]");

            HookAction = Callback;
            var moduleHandle = Kernel32.GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);

            HookHandle = User32.SetWindowsHookEx(type, HookAction, moduleHandle, 0);
            if (HookHandle == null || HookHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            IsStarted = true;
        }

        /// <summary>
        /// Stop hook process
        /// </summary>
        public void Stop()
        {
            if (!IsStarted)
            {
                return;
            }

            Trace.WriteLine($"Stopping hook '{Name}'...", $"Hook.StartHook [{Thread.CurrentThread.Name}]");

            User32.UnhookWindowsHookEx(HookHandle);

            IsStarted = false;
        }

        #endregion

        #region Protected methods

        protected abstract IntPtr InternalCallback(int nCode, int wParam, IntPtr lParam);

        protected static T ToStructure<T>(IntPtr ptr) where T : struct => (T)Marshal.PtrToStructure(ptr, typeof(T));

        #endregion

        #region Private methods

        private IntPtr Callback(int nCode, int wParam, IntPtr lParam)
        {
            try
            {
                return InternalCallback(nCode, wParam, lParam);
            }
            catch (Exception exception)
            {
                OnExceptionOccurred(exception);
                
                return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        /// <summary>
        /// Dispose internal system hook resources
        /// </summary>
        public void Dispose() => Stop();

        #endregion
    }
}