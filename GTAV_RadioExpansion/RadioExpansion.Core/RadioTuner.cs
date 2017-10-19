using RadioExpansion.Core.Logging;
using RadioExpansion.Core.RadioPlayers;
using System;
using System.Linq;

namespace RadioExpansion.Core
{
    /// <summary>
    /// Class to handle radio stations
    /// </summary>
    public class RadioTuner : IDisposable
    {
        private Radio[] _radios;
        private int? _activeStationIndex; // the currently selected station
        private int? _nextStationIndex; // the next station which gonna start, if activated

        public event EventHandler RadioLoadingCompleted;

        public bool IsRadioOn => _radios.Any(r => r.IsPlaying);

        public bool HasRadios => (_radios.Length > 0);

        public Radio CurrentStation
        {
            get
            {
                return (_activeStationIndex.HasValue ? _radios[_activeStationIndex.Value] : null);
            }
            set
            {
                int? newStationIndex = (value == null ? null : (int?)Array.IndexOf(_radios, value));
                if (newStationIndex != _activeStationIndex) // do anything only if station really changed
                {
                    //bool wasPreviousRadioStationPlaying = (CurrentStation?.Player.IsPlaying == true); // the previous radio station was on
                    CurrentStation?.Stop(); // completely stop the radio station (it can be in paused state)

                    _activeStationIndex = newStationIndex;

                    if (_activeStationIndex.HasValue)
                    {
                        CurrentStation.Play();
                    }
                }
            }
        }

        private static RadioTuner _instance;

        public static RadioTuner Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new RadioTuner();
                }
                return _instance;
            }
        }

        private RadioTuner()
        {
            _radios = new Radio[0];
        }

        public void Play(int? stationIndex = null)
        {
            if (_radios.Length > 0)
            {
                CurrentStation = _radios[stationIndex ?? _activeStationIndex ?? 0];
                CurrentStation.Play();
            }
        }

        public void StopCurrent()
        {
            CurrentStation?.Stop();
        }

        public void PauseCurrent()
        {
            CurrentStation?.Pause();
        }

        public void KeepStreamAlive()
        {
            CurrentStation?.KeepAlive();
        }
        
        public void Dispose()
        {
            foreach (var radio in _radios)
            {
                radio.Dispose();
            }

            _instance = null;
        }

        public Radio MoveToNextStation()
        {
            if (!_nextStationIndex.HasValue)
            {
                _nextStationIndex = _activeStationIndex;
            }
            
            if (_nextStationIndex == _radios.Length - 1)
            {
                _nextStationIndex = 0;
            }
            else
            {
                _nextStationIndex++;
            }

            return _radios[_nextStationIndex.Value];
        }

        public Radio MoveToPreviousStation()
        {
            if (!_nextStationIndex.HasValue)
            {
                _nextStationIndex = _activeStationIndex;
            }

            if (_nextStationIndex == 0)
            {
                _nextStationIndex = _radios.Length - 1;
            }
            else
            {
                _nextStationIndex--;
            }

            return _radios[_nextStationIndex.Value];
        }

        public void ActivateNextStation()
        {
            if (_nextStationIndex.HasValue)
            {
                CurrentStation = _radios[_nextStationIndex.Value];
                _nextStationIndex = null;
            }
        }

        public void LoadRadios()
        {
            _radios = new RadioConfigManager().LoadRadios();

            RadioLoadingCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void LogCurrentTrack()
        {
            if (CurrentStation != null)
            {
                Logger.LogTrack(CurrentStation.Name, CurrentStation.CurrentTrackMetaData);
            }
        }
    }
}
