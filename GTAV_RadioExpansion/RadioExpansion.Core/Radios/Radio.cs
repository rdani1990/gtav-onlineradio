using System;
using NAudio.Wave;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Timer = System.Timers.Timer;
using System.Threading.Tasks;
using NAudio;
using System.Xml.Linq;
using System.Globalization;
using System.Reflection;
using System.Net.Configuration;
using RadioExpansion.Core.Logging;
using RadioExpansion.Core.Serialization;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace RadioExpansion.Core.RadioPlayers
{
    public enum StreamingPlaybackState
    {
        Stopped,
        Playing,
        Buffering,
        Paused,
        Suspended
    }

    [XmlWhitelistSerialization]
    public abstract class Radio : IDisposable
    {
        /// <summary>
        /// After this time in milliseconds elapses, the playing is suspended
        /// </summary>
        private const int TIME_WITHOUT_SCRIPT_NOTIFY = 1500;

        /// <summary>
        /// Playing won't start until buffer reach this limit.
        /// </summary>
        private const double BUFFERED_SECONDS_BEFORE_PLAY = 4;

        /// <summary>
        /// Logs every track, if the radio station is playing.
        /// </summary>
        protected const bool LOG_EVERY_PLAYED_TRACK = false;

        private const int DEFAULT_BUFFER_LENGTH_IN_SECONDS = 20;

        /// <summary>
        /// If this value is true, the playing's gonna stop if <see cref="KeepAlive" /> method wasn't invoked within <see cref="TIME_WITHOUT_SCRIPT_NOTIFY" /> milliseconds.
        /// </summary>
        public static bool PauseIfNotNofified { get; set; }

        private BufferedWaveProvider _bufferedWaveProvider;
        private IWavePlayer _waveOut;
        private volatile StreamingPlaybackState playbackState;
        private VolumeWaveProvider16 volumeProvider;

        /// <summary>
        /// Timer for metadata synchronization.
        /// </summary>
        protected Timer _metaSyncTimer;

        private Timer _playbackTimer;
        private Stopwatch _stopWatch;
        private object _metaDataLock = new object();
        private bool _restartPlayingInSuspendedStateIfNotified;
        private readonly float _defaultVolume;
        private bool _hasOngoingConversation;
        private float _volume;
        private MetaData _currentTrackMetaData;
        private string _relativeDirectoryPath;

        /// <summary>
        /// Radio has some problems (e. g. empty folder for clustered radio, no Uri for online radios, etc.), and should be ignored.
        /// </summary>
        public bool ShouldIgnore { get; protected set; }

        /// <summary>
        /// Radio volume used in GTA V. 0 = Off, 1 = Max
        /// </summary>
        private static float _defaultVolumeMultiplier = 1;

        [XmlWhitelisted]
        public float Volume
        {
            get { return _volume; }
            set
            {
                _volume = value;
                RefreshVolume();
            }
        }

        public bool HasOngoingConversation
        {
            get { return _hasOngoingConversation; }
            set
            {
                if (value != _hasOngoingConversation)
                {
                    _hasOngoingConversation = value;
                    RefreshVolume();
                }
            }
        }

        public MetaData CurrentTrackMetaData
        {
            get
            {
                lock (_metaDataLock)
                {
                    return _currentTrackMetaData;
                }
            }
            set
            {
                lock (_metaDataLock)
                {
                    if (_currentTrackMetaData?.ToString() != value?.ToString())
                    {
                        _currentTrackMetaData = value;
                    }
                }
            }
        }

        public string AbsoluteDirectoryPath => Path.Combine(RadioConfigManager.GetRadioFolder(), RelativeDirectoryPath);

        [XmlWhitelisted]
        public string Name { get; set; }


        /// <summary>
        /// The path of the directory, relative to the root directory (the 'radios' folder)
        /// </summary>
        [XmlWhitelisted, XmlAttribute("Folder")]
        public virtual string RelativeDirectoryPath
        {
            get { return _relativeDirectoryPath; }
            set
            {
                _relativeDirectoryPath = value;

                OnPathChanged();
            }
        }

        public bool IsPlaying => (playbackState == StreamingPlaybackState.Playing || playbackState == StreamingPlaybackState.Buffering);

        public virtual float BufferLengthInSeconds => DEFAULT_BUFFER_LENGTH_IN_SECONDS;

        protected virtual bool AlwaysSleepWhenBufferIsFull => true;

        /// <summary>
        /// This is the interval in ms for synchronizing the metadata for the currently played track.
        /// </summary>
        protected abstract int MetaDataSyncInterval { get; }

        static Radio()
        {
            SetDefaultVolumeMultiplier();
            EnableUnsafeHeaderParsing();
        }

        public Radio()
        {
            _volume = 1;

            _playbackTimer = new Timer();
            _playbackTimer.Interval = 250;
            _playbackTimer.Elapsed += PlaybackTimerElapsed;

            _metaSyncTimer = new Timer();
            _metaSyncTimer.Interval = MetaDataSyncInterval;
            _metaSyncTimer.Elapsed += (s, e) => RefreshMetaInfo();

            _stopWatch = new Stopwatch();

            _restartPlayingInSuspendedStateIfNotified = true;
        }

        public void StartMetaSyncing()
        {
            if (!_metaSyncTimer.Enabled)
            {
                Task.Run(() =>
                {
                    RefreshMetaInfo();
                    _metaSyncTimer.Enabled = true;
                });
            }
        }

        protected void GetFilesFromRadioFolder(out IEnumerable<string> audioFiles, out IEnumerable<string> playlistFiles)
        {
            var allFiles = Directory.GetFiles(AbsoluteDirectoryPath);

            audioFiles = allFiles.Where(f => HasAllowedAudioExtension(f));
            playlistFiles = allFiles.Where(f => HasAllowedPlaylistExtension(f));
        }
        
        /// <summary>
        /// Called when the <see cref="RelativeDirectoryPath" /> is changed.
        /// </summary>
        protected virtual void OnPathChanged() { }

        protected void RefreshVolume()
        {
            if (volumeProvider != null)
            {
                volumeProvider.Volume = _volume * _defaultVolumeMultiplier * (_hasOngoingConversation ? 0.5f : 1);
            }
        }

        private void EnableTimers(bool enable)
        {
            _playbackTimer.Enabled = enable;
            if (enable)
            {
                _stopWatch.Start();
            }
            else
            {
                _stopWatch.Stop();
            }
        }

        /// <summary>
        /// Resets the stopwatch, so the online player is noticed that the script is still running, and not suspended by menu, etc.
        /// </summary>
        public void KeepAlive()
        {
            _stopWatch.Restart();
        }

        public void Play()
        {
            switch (playbackState)
            {
                case StreamingPlaybackState.Stopped:
                    playbackState = StreamingPlaybackState.Buffering;
                    _bufferedWaveProvider = null;
                    Task.Run((Action)StreamAudio);
                    EnableTimers(true);

                    break;
                case StreamingPlaybackState.Paused:
                case StreamingPlaybackState.Suspended:
                    playbackState = StreamingPlaybackState.Buffering;
                    break;
            }

#pragma warning disable CS0162 // Unreachable code detected
            if (LOG_EVERY_PLAYED_TRACK)
            {
                Logger.LogTrack(Name, CurrentTrackMetaData);
            }
#pragma warning restore CS0162 // Unreachable code detected
        }

        public void Stop()
        {
            if (playbackState != StreamingPlaybackState.Stopped)
            {
                playbackState = StreamingPlaybackState.Stopped;

                try
                {
                    EnableTimers(false);

                    if (_waveOut != null)
                    {
                        _waveOut.Stop();
                        _waveOut.Dispose();
                        _waveOut = null;
                    }
                    
                    // n.b. streaming thread may not yet have exited
                    //Thread.Sleep(500);
                    //ShowBufferState(0);
                }
                catch (Exception e)
                {
                    Logger.Log("Failed to stop radio '{0}'. Error: ", Name, e);
                }
            }
        }

        public void Pause()
        {
            if (playbackState == StreamingPlaybackState.Playing || playbackState == StreamingPlaybackState.Buffering)
            {
                if (_waveOut != null)
                {
                    _waveOut.Pause();
                    Debug.WriteLine(String.Format("User requested Pause, waveOut.PlaybackState={0}", _waveOut.PlaybackState));
                }
                playbackState = StreamingPlaybackState.Paused;
            }
        }

        public void Suspend(bool disableRestartUntilNotifierTimeout)
        {
            Pause();

            // suspention can be cancelled in the next PlaybackTimerElapsed if _restartPlayingInSuspendedStateIfNotified is true, so we set it to false, and after the timer elapsed, we set it back to true 
            if (disableRestartUntilNotifierTimeout)
            {
                _restartPlayingInSuspendedStateIfNotified = false;

                Task.Run(() =>
                {
                    Thread.Sleep(TIME_WITHOUT_SCRIPT_NOTIFY + 250);
                    _restartPlayingInSuspendedStateIfNotified = true;
                });
            }

            playbackState = StreamingPlaybackState.Suspended;
        }

        protected abstract void StreamAudio();

        protected void StreamAudio(WaveStream stream)
        {
            var buffer = new byte[64 * 1024]; // needs to be big enough to hold a decompressed frame
            int? bufferDropLimit = null;
            try
            {
                int frameErrorCount = 0;
                do
                {
                    if (IsBufferNearlyFull)
                    {
                        if (AlwaysSleepWhenBufferIsFull || IsPlaying)
                        {
                            Debug.WriteLine("Buffer getting full, taking a break");
                            Thread.Sleep(1000);
                        }
                        else
                        {
                            Debug.WriteLine("Buffer getting full, droping half of it.");
                            Debug.WriteLine(stream.CurrentTime);
                            if (bufferDropLimit.HasValue)
                            {
                                _bufferedWaveProvider.Read(new byte[bufferDropLimit.Value], 0, bufferDropLimit.Value); // drop the elder half of the buffer content, so the new samples have space, and the stream gonna be consistent
                                bufferDropLimit = null;
                            }
                            else
                            {
                                _bufferedWaveProvider.ClearBuffer();
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            if (_bufferedWaveProvider == null)
                            {
                                _bufferedWaveProvider = new BufferedWaveProvider(stream.WaveFormat);
                                _bufferedWaveProvider.BufferDuration = TimeSpan.FromSeconds(BufferLengthInSeconds); // allow us to get well ahead of ourselves
                                //bufferedWaveProvider.DiscardOnBufferOverflow = true;
                            }

                            int availableBuffer = _bufferedWaveProvider.BufferLength - _bufferedWaveProvider.BufferedBytes;
                            int decompressed = stream.Read(buffer, 0, availableBuffer < buffer.Length ? availableBuffer : buffer.Length);
                            _bufferedWaveProvider.AddSamples(buffer, 0, decompressed);

                            if (_bufferedWaveProvider.BufferedBytes > _bufferedWaveProvider.BufferLength / 2) // if half of the buffer is full, mark that spot
                            {
                                if (!bufferDropLimit.HasValue)
                                {
                                    bufferDropLimit = _bufferedWaveProvider.BufferedBytes;
                                }
                            }
                            else
                            {
                                bufferDropLimit = null;
                            }

                            frameErrorCount = 0;
                        }
                        catch (MmException)
                        {
                            frameErrorCount++;
                            if (frameErrorCount == 5) // if 5 frames in a raw causes error, we just give up
                            {
                                Debug.WriteLine("MmException");
                                break;
                            }
                        }
                        catch (Exception ex) // most frequent exceptions: WebException, IOException, SocketException
                        {
                            // most probably we have closed the stream
                            Debug.WriteLine(ex);
                            break;
                        }
                    }

                } while (stream.CanRead && playbackState != StreamingPlaybackState.Stopped);
                Debug.WriteLine("Exiting");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
            finally
            {
                stream.Dispose();
                StreamingFinished();
            }
        }

        /// <summary>
        /// Streaming has stopped, or a fatal error occured.
        /// </summary>
        protected virtual void StreamingFinished() { }

        private bool IsBufferNearlyFull
        {
            get
            {
                return _bufferedWaveProvider != null &&
                       _bufferedWaveProvider.BufferLength - _bufferedWaveProvider.BufferedBytes
                       < _bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;
            }
        }

        private void PlaybackTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (PauseIfNotNofified)
            {
                if (_stopWatch.ElapsedMilliseconds > TIME_WITHOUT_SCRIPT_NOTIFY) // the script didn't check in for a while, it's time to suspend the playing
                {
                    Suspend(false);
                    return;
                }
                else if (playbackState == StreamingPlaybackState.Suspended && _restartPlayingInSuspendedStateIfNotified)
                {
                    Play();
                }
            }

            if (playbackState != StreamingPlaybackState.Stopped)
            {
                if (_waveOut == null && _bufferedWaveProvider != null)
                {
                    _waveOut = new WaveOut();
                    _waveOut.PlaybackStopped += OnPlaybackStopped;

                    volumeProvider = new VolumeWaveProvider16(_bufferedWaveProvider);
                    volumeProvider.Volume = _volume * _defaultVolumeMultiplier;

                    _waveOut.Init(volumeProvider);
                }
                else if (_bufferedWaveProvider != null)
                {
                    double bufferedSeconds = _bufferedWaveProvider.BufferedDuration.TotalSeconds;

                    Debug.WriteLine("Buffered: " + bufferedSeconds);

                    // make it stutter less if we buffer up a decent amount before playing
                    if (bufferedSeconds < 0.5 && playbackState == StreamingPlaybackState.Playing)
                    {
                        playbackState = StreamingPlaybackState.Buffering;
                        _waveOut.Pause();
                        Debug.WriteLine(String.Format("Paused to buffer, waveOut.PlaybackState={0}", _waveOut.PlaybackState));
                    }
                    else if (bufferedSeconds >= Math.Min(BufferLengthInSeconds, BUFFERED_SECONDS_BEFORE_PLAY) && playbackState == StreamingPlaybackState.Buffering)
                    {
                        _waveOut.Play();
                        Debug.WriteLine(String.Format("Started playing, waveOut.PlaybackState={0}", _waveOut.PlaybackState));
                        playbackState = StreamingPlaybackState.Playing;
                    }
                }
            }
        }

        public abstract void RefreshMetaInfo();
        
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            Debug.WriteLine("Playback Stopped");
            if (e.Exception != null)
            {
                Console.Error.WriteLine("Playback Error {0}", e.Exception.Message);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Sets the default volume multiplier with the one set in GTA V
        /// </summary>
        protected static void SetDefaultVolumeMultiplier()
        {
            string profilesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Rockstar Games\GTA V\Profiles");
            if (!Directory.Exists(profilesDir)) // doesn't exist? Leave volume untouched
            {
                return;
            }

            var dirs = Directory.GetDirectories(profilesDir);
            if (dirs.Length != 1) // more profiles, or actually none. Don't bother, leave volume untouched
            {
                return;
            }

            string settingsFilePath = Path.Combine(dirs[0], "pc_settings.bin");

            if (File.Exists(settingsFilePath))
            {
                using (var settingsFile = File.OpenRead(settingsFilePath))
                {
                    settingsFile.Seek(0x164, SeekOrigin.Begin); // position of the byte which stores the volume
                    int volGTAV = settingsFile.ReadByte(); // value between 0 and 10
                    if (volGTAV >= 0 && volGTAV <= 10)
                    {
                        _defaultVolumeMultiplier = volGTAV / 10f;
                    }
                }
            }
        }

        protected static bool HasAllowedAudioExtension(string path)
        {
            var allowedExtensions = new[]
            {
                ".mp3", ".ogg", ".wav", ".flac", ".lnk"
            };

            return allowedExtensions.Contains(Path.GetExtension(path).ToLower());
        }

        protected static bool HasAllowedPlaylistExtension(string path)
        {
            var allowedExtensions = new[]
            {
                ".m3u", ".m3u8", ".pls"
            };

            return allowedExtensions.Contains(Path.GetExtension(path).ToLower());
        }

        /// <summary>
        /// Enable 'useUnsafeHeaderParsing', because .NET throws protocol violation exception for many shoutcast streams.
        /// See http://o2platform.wordpress.com/2010/10/20/dealing-with-the-server-committed-a-protocol-violation-sectionresponsestatusline/
        /// </summary>
        private static bool EnableUnsafeHeaderParsing()
        {
            //Get the assembly that contains the internal class
            Assembly assembly = Assembly.GetAssembly(typeof(SettingsSection));
            if (assembly != null)
            {
                //Use the assembly in order to get the internal type for the internal class
                Type settingsSectionType = assembly.GetType("System.Net.Configuration.SettingsSectionInternal");
                if (settingsSectionType != null)
                {
                    //Use the internal static property to get an instance of the internal settings class.
                    //If the static instance isn't created already invoking the property will create it for us.
                    object anInstance = settingsSectionType.InvokeMember("Section",
                    BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.NonPublic, null, null, new object[] { });
                    if (anInstance != null)
                    {
                        //Locate the private bool field that tells the framework if unsafe header parsing is allowed
                        FieldInfo aUseUnsafeHeaderParsing = settingsSectionType.GetField("useUnsafeHeaderParsing", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (aUseUnsafeHeaderParsing != null)
                        {
                            aUseUnsafeHeaderParsing.SetValue(anInstance, true);
                            return true;
                        }

                    }
                }
            }
            return false;
        }
    }
}
