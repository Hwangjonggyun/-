﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using log4net;
using Microsoft.Win32;
using ScpControl.Shared.XInput;

namespace ScpXInputBridge
{
    public partial class XInputDll
    {
        #region Private delegates

        private static readonly Lazy<XInputEnableFunction> OriginalXInputEnableFunction =
            new Lazy<XInputEnableFunction>(
                () => (XInputEnableFunction) GetMethod<XInputEnableFunction>(NativeDllHandle, "XInputEnable"));

        private static readonly Lazy<XInputGetStateFunction> OriginalXInputGetStateFunction = new Lazy
            <XInputGetStateFunction>(
            () => (XInputGetStateFunction) GetMethod<XInputGetStateFunction>(NativeDllHandle, "XInputGetState"));

        private static readonly Lazy<XInputSetStateFunction> OriginalXInputSetStateFunction = new Lazy
            <XInputSetStateFunction>(
            () => (XInputSetStateFunction) GetMethod<XInputSetStateFunction>(NativeDllHandle, "XInputSetState"));

        private static readonly Lazy<XInputGetCapabilitiesFunction> OriginalXInputGetCapabilitiesFunction = new Lazy
            <XInputGetCapabilitiesFunction>(
            () => (XInputGetCapabilitiesFunction)
                GetMethod<XInputGetCapabilitiesFunction>(NativeDllHandle, "XInputGetCapabilities"));

        private static readonly Lazy<XInputGetDSoundAudioDeviceGuidsFunction>
            OriginalXInputGetDSoundAudioDeviceGuidsFunction = new Lazy<XInputGetDSoundAudioDeviceGuidsFunction>(
                () => (XInputGetDSoundAudioDeviceGuidsFunction)
                    GetMethod<XInputGetDSoundAudioDeviceGuidsFunction>(NativeDllHandle,
                        "XInputGetDSoundAudioDeviceGuids"));

        private static readonly Lazy<XInputGetBatteryInformationFunction> OriginalXInputGetBatteryInformationFunction = new Lazy
            <XInputGetBatteryInformationFunction>(
            () => (XInputGetBatteryInformationFunction)
                GetMethod<XInputGetBatteryInformationFunction>(NativeDllHandle, "XInputGetBatteryInformation"));

        private static readonly Lazy<XInputGetKeystrokeFunction> OriginalXInputGetKeystrokeFunction = new Lazy
            <XInputGetKeystrokeFunction>(
            () =>
                (XInputGetKeystrokeFunction)
                    GetMethod<XInputGetKeystrokeFunction>(NativeDllHandle, "XInputGetKeystroke"));

        #endregion

        #region Private properties

        private static string BasePath
        {
            get
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    using (
                        var regKey = hklm.OpenSubKey(@"SOFTWARE\Nefarius Software Solutions\ScpToolkit", false)
                        )
                    {
                        return (regKey != null) ? (string) regKey.GetValue("Path") : string.Empty;
                    }
                }
            }
        }

        private static string ScpControlPath
        {
            get
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    using (
                        var regKey =
                            hklm.OpenSubKey(
                                @"SOFTWARE\Nefarius Software Solutions\ScpToolkit\ScpControl", false))
                    {
                        return (regKey != null) ? (string) regKey.GetValue("Path") : string.Empty;
                    }
                }
            }
        }

        private static string XInputDllPath
        {
            get
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    using (
                        var regKey =
                            hklm.OpenSubKey(@"SOFTWARE\Nefarius Software Solutions\ScpToolkit\XInput",
                                false))
                    {
                        return (regKey != null) ? (string) regKey.GetValue("DllPathOverride") : string.Empty;
                    }
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Translates a native method into a managed delegate.
        /// </summary>
        /// <typeparam name="T">The type of the target delegate.</typeparam>
        /// <param name="module">The module name to search the function in.</param>
        /// <param name="methodName">The native finctions' name.</param>
        /// <returns>Returns the managed delegate.</returns>
        private static Delegate GetMethod<T>(IntPtr module, string methodName)
        {
            return Marshal.GetDelegateForFunctionPointer(Kernel32Natives.GetProcAddress(module, methodName), typeof (T));
        }

        /// <summary>
        ///     Initializes library.
        /// </summary>
        static XInputDll()
        {
            Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            Log.InfoFormat("Library loaded by process {0} [{1}]",
                Process.GetCurrentProcess().ProcessName,
                Process.GetCurrentProcess().MainWindowTitle);

            var myself = Assembly.GetExecutingAssembly().GetName();
            var myPath = Assembly.GetExecutingAssembly().Location;
            var myName = Path.GetFileName(myPath);

            Log.InfoFormat("Initializing library {0} [{1}]", myName, myself.Version);

            try
            {
                var basePath = BasePath;
                Log.DebugFormat("ScpToolkit bin path: {0}", basePath);
                var controlPath = ScpControlPath;
                Log.DebugFormat("ScpControl bin path: {0}", controlPath);

                // resolve assembly dependencies
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    var asmName = new AssemblyName(args.Name).Name;
                    var asmPath = Path.Combine(basePath, string.Format("{0}.dll", asmName));

                    Log.DebugFormat("Loading assembly {0} from {1}", asmName, asmPath);

                    return Assembly.LoadFrom(asmPath);
                };

                var scpControl = Assembly.LoadFrom(controlPath);
                var scpProxyType = scpControl.GetType("ScpControl.ScpProxy");

                Proxy = Activator.CreateInstance(scpProxyType);

                Proxy.Start();
            }
            catch (Exception ex)
            {
                Log.FatalFormat("Error during library initialization: {0}", ex);
                return;
            }

            // if no custom path specified by user, use DLL in system32 dir
            var xinputPath = (!string.IsNullOrEmpty(XInputDllPath) && File.Exists(XInputDllPath))
                ? XInputDllPath
                : Path.Combine(Environment.SystemDirectory, myName);
            Log.DebugFormat("Original XInput DLL path: {0}", xinputPath);

            NativeDllHandle = Kernel32Natives.LoadLibrary(xinputPath);

            if (NativeDllHandle == IntPtr.Zero)
            {
                Log.FatalFormat("Couldn't load native DLL: {0}", new Win32Exception(Marshal.GetLastWin32Error()));
                return;
            }

            Log.Info("Library initialized");
        }

        #endregion

        #region Private fields

        private static readonly IntPtr NativeDllHandle = IntPtr.Zero;
        private static readonly dynamic Proxy;
        private const string CfgFile = "ScpXInput.ini";
        private static readonly string WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly ILog Log;

        #endregion

        #region Delegates for GetProcAddress

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void XInputEnableFunction(bool enable);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint XInputGetStateFunction(uint dwUserIndex, ref XINPUT_STATE pState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint XInputSetStateFunction(uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint XInputGetCapabilitiesFunction(uint dwUserIndex, uint dwFlags,
            ref XINPUT_CAPABILITIES pCapabilities);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint XInputGetDSoundAudioDeviceGuidsFunction(uint dwUserIndex, ref Guid pDSoundRenderGuid,
            ref Guid pDSoundCaptureGuid);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint XInputGetBatteryInformationFunction(uint dwUserIndex, byte devType,
            ref XINPUT_BATTERY_INFORMATION pBatteryInformation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint XInputGetKeystrokeFunction(
            uint dwUserIndex, uint dwReserved, ref XINPUT_KEYSTROKE pKeystroke);

        #endregion
    }
}