﻿using System;
using System.IO;
using System.Reflection;
using log4net;
using ScpControl.ScpCore;

namespace ScpControl.Sound
{
    public class AudioPlayer
    {
        private static readonly Lazy<AudioPlayer> LazyInstance = new Lazy<AudioPlayer>(() => new AudioPlayer());
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly dynamic _soundEngine;

        /// <summary>
        ///     Initializes the irrKlang engine.
        /// </summary>
        private AudioPlayer()
        {
            // build path depending on process architecture
            var irrKlangPath = Path.Combine(GlobalConfiguration.AppDirectory,
                (Environment.Is64BitProcess)
                    ? @"irrKlang\amd64\irrKlang.NET4.dll"
                    : @"irrKlang\x86\irrKlang.NET4.dll");
            Log.DebugFormat("Loading irrKlang engine from {0}", irrKlangPath);

            try
            {
                var currentDir = Directory.GetCurrentDirectory();
                var ikPluginPath = Path.GetDirectoryName(irrKlangPath);

                /* irrKlang looks for plugins in the host EXE´s directory by default,
                * so we need to change it temporarly while instantiating the sound engine.
                * */
                Directory.SetCurrentDirectory(ikPluginPath ?? currentDir);

                // load assembly
                var irrKlangAssembly = Assembly.LoadFile(irrKlangPath);

                // get type of ISoundEngine class
                var soundEngineType = irrKlangAssembly.GetType("IrrKlang.ISoundEngine");

                // instantiate  ISoundEngine
                _soundEngine = Activator.CreateInstance(soundEngineType);

                // restore original working directory
                Directory.SetCurrentDirectory(currentDir);
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Couldn't initialize sound engine: {0}", ex);
            }
        }

        public static AudioPlayer Instance
        {
            get { return LazyInstance.Value; }
        }

        public void PlayCustomFile(string path)
        {
            if (_soundEngine == null
                || !GlobalConfiguration.Instance.SoundsEnabled
                || string.IsNullOrEmpty(path)
                || !File.Exists(path))
                return;

            _soundEngine.Play2D(path);
        }
    }
}
