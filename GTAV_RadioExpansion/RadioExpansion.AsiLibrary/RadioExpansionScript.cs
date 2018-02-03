using System;
using GTA;
using GTA.Native;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using Control = GTA.Control;
using Font = GTA.Font;
using System.Diagnostics;
using System.Collections.Generic;
using RadioExpansion.Core.RadioPlayers;
using System.Threading.Tasks;
using RadioExpansion.Core;
using RadioExpansion.Core.Logging;

namespace RadioExpansion.AsiLibrary
{
    public class RadioExpansionScript : Script
    {
        private bool _isInVehicle;
        private bool _isPressingToggleRadioButton;
        private int _lastKnownVehicleHandle;
        private VehicleRadioManager _vehicleRadioManager; // the known turned on radios for vehicles
        private RadioTuner _radioTuner;
        private Random _random;

        private const int IngameRadioCount = 20;

        public bool IsCustomRadioPlayingAllowed
        {
            get
            {
                var player = Game.Player;
                return player.Character.IsInVehicle() && player.CanControlCharacter && !player.IsDead && !Function.Call<bool>(Hash.IS_PLAYER_BEING_ARRESTED, new InputArgument[] { player.Handle, true }) && Function.Call<bool>(Hash.IS_GAME_IN_CONTROL_OF_MUSIC);
            }
        }

        public RadioExpansionScript()
        {
            Logger.SetLogger(new FileLogger());

            _random = new Random();

            Task.Run(() =>
            {
                Initialize(RadioConfigManager.LoadConfig()?.Radios);
            });
        }

        private void Initialize(Radio[] radios)
        {
            if (radios?.Length > 0) // only do anything if there's any radio available...
            {
                RadioLogoManager.CreateTempLogos(radios);

                _radioTuner = new RadioTuner(radios);
                _vehicleRadioManager = new VehicleRadioManager();

                Tick += OnTick;
                Interval = 100;

                KeyDown += OnKeyDown;
                KeyUp += OnKeyUp;

                Radio.PauseIfNotNofified = true;
            }
            else
            {
                Logger.Log("No radios were found. The script is shutting down...");
            }
        }

        /// <summary>
        /// Give a chance to turn on the custom radio if player is about to enter in a new vehicle.
        /// </summary>
        private void HandleIfPlayerIsTryingToEnterInVehicle(Ped player)
        {
            var newVehicle = player.GetVehicleIsTryingToEnter();

            if (newVehicle != null && // player is about to enter in a vehicle
                !_vehicleRadioManager.HasVehicleRadioInfo(newVehicle.Handle) &&
                !Function.Call<bool>(Hash._IS_VEHICLE_RADIO_LOUD, newVehicle) && // that'd be weird...
                !Function.Call<bool>(Hash.AUDIO_IS_SCRIPTED_MUSIC_PLAYING))
            {
                var allRadio = _radioTuner.Radios;
                int newStationIndex = _random.Next(allRadio.Length + IngameRadioCount);
                if (newStationIndex < allRadio.Length)
                {
                    newVehicle.IsRadioEnabled = false;
                    _vehicleRadioManager.RegisterVehicleWithRadio(newVehicle.Handle, allRadio[newStationIndex]);
                    _radioTuner.CurrentStation = allRadio[newStationIndex];
                }
                else
                {
                    _vehicleRadioManager.RegisterVehicleWithRadio(newVehicle.Handle, null); // register with turned off radio, so it won't try to generate a radio for it every time the user tries to enter
                }
            }
        }

        /// <summary>
        /// Handle case if player entered in a vehicle, or exited from one.
        /// </summary>
        private void HandleIfPlayerVehicleStateChanged(Ped player)
        {
            if (_isInVehicle != player.IsInVehicle()) // player state within car has changed against its last known value
            {
                _isInVehicle = !_isInVehicle; // flip the state

                if (_isInVehicle)
                {
                    _lastKnownVehicleHandle = player.CurrentVehicle.Handle;
                    var customRadio = _vehicleRadioManager.GetVehicleCustomRadio(_lastKnownVehicleHandle);
                    if (customRadio != null) // if the custom radio was turned on for this vehicle previously, then start the radio
                    {
                        customRadio.Play();
                    }
                    else
                    {
                        _vehicleRadioManager.RegisterVehicleWithRadio(_lastKnownVehicleHandle, null); // still register it with an empty radio, so next time if player is approaching this vehicle, it won't generate a random custom radio for it
                    }
                }
            }
        }

        /// <summary>
        /// Does all the necessary actions if there is a station on.
        /// </summary>
        private void HandleIfStationIsOn()
        {
            if (_radioTuner.CurrentStation != null)
            {
                bool isCustomRadioPlayingAllowed = IsCustomRadioPlayingAllowed;

                Audio.SetAudioFlag(AudioFlag.WantedMusicDisabled, isCustomRadioPlayingAllowed);
                Audio.SetAudioFlag(AudioFlag.DisableFlightMusic, isCustomRadioPlayingAllowed);

                if (isCustomRadioPlayingAllowed)
                {
                    if (Function.Call<bool>(Hash.AUDIO_IS_SCRIPTED_MUSIC_PLAYING))
                    {
                        Function.Call(Hash.TRIGGER_MUSIC_EVENT, "OJDA_STOP"); // stop script music: arms traffic (air)
                        Function.Call(Hash.TRIGGER_MUSIC_EVENT, "OJDG_STOP"); // stop script music: arms traffic (ground)
                        // TODO: all the other...
                    }

                    _radioTuner.CurrentStation.KeepAlive();
                    _radioTuner.CurrentStation.HasOngoingConversation = (Function.Call<bool>(Hash.IS_SCRIPTED_CONVERSATION_ONGOING) || Function.Call<bool>(Hash.IS_MOBILE_PHONE_CALL_ONGOING));
                    HandleRadioChangeKeyDowns(false);
                }
                else
                {
                    if (_isInVehicle)
                    {
                        _radioTuner.PauseCurrent(); // probably player just hop out from the vehicle for a sec, we suspend it to start quickier if he hops in 
                    }
                    else
                    {
                        _radioTuner.StopCurrent();
                    }
                }
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            var player = Game.Player.Character;

            HandleIfPlayerIsTryingToEnterInVehicle(player);
            HandleIfPlayerVehicleStateChanged(player);
            HandleIfStationIsOn();
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Z)
            {
                _isPressingToggleRadioButton = false;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _radioTuner.CurrentStation?.Suspend(true);
            }
            else if (e.KeyCode == Keys.Z && !_isPressingToggleRadioButton && IsCustomRadioPlayingAllowed)
            {
                _isPressingToggleRadioButton = true;

                ToggleRadioStreamer();
            }
            else if (e.KeyCode == Keys.BrowserFavorites)
            {
                string currentTrack = _radioTuner.CurrentStation?.CurrentTrackMetaData?.ToString();

                if (!String.IsNullOrEmpty(currentTrack))
                {
                    Logger.LogTrack(_radioTuner.CurrentStation.Name, _radioTuner.CurrentStation.CurrentTrackMetaData);
                    UI.Notify($"Track '{currentTrack}' logged.");
                }
                else
                {
                    UI.Notify("No track to log.");
                }
            }
        }

        private void DrawRadioLogoWithInfo(Radio radio)
        {
            if (radio != null)
            {
                string text = radio.Name;
                var metaData = radio.CurrentTrackMetaData;
                if (metaData != null)
                {
                    if (String.IsNullOrEmpty(metaData.Artist) || String.IsNullOrEmpty(metaData.Track))
                    {
                        text += $"\n{metaData.ToString()}";
                    }
                    else
                    {
                        text += $"\n{metaData.Artist.ToUpper()}\n{metaData.Track}";
                    }
                }

                int screenWidth = 0, screenHeight = 0;
                unsafe
                {
                    Function.Call(Hash.GET_SCREEN_RESOLUTION, new InputArgument[] { &screenWidth, &screenHeight });
                }

                //Logger.Log("{0}; {1}", screenWidth, screenHeight);
                //Logger.Log("AR: {0}", Function.Call<float>(Hash._GET_SCREEN_ASPECT_RATIO, new InputArgument[] { false }));

                var caption = new UIText(text, new Point(screenWidth / 2, 203), 0.5f, Color.White, Font.ChaletComprimeCologne, true, false, true);
                UI.DrawTexture(GetRadioLogoTempPath(radio.Name), 0, -9999, 50, new Point(screenWidth / 2, 131), new PointF(0.5f, 0), new Size(108 / 4 * 3, 108 / 3 * 2), 0, Color.White, 1);
                caption.Draw();
            }
        }

        private string GetRadioLogoTempPath(string radioName)
        {
            uint playerHash = (uint)Game.Player.Character.Model.Hash;
            switch (playerHash)
            {
                case (uint)PedHash.Franklin: return RadioLogoManager.GetTempLogoPathForFranklin(radioName);
                case (uint)PedHash.Trevor: return RadioLogoManager.GetTempLogoPathForTrevor(radioName);
                default: return RadioLogoManager.GetTempLogoPathForMichael(radioName);
            }
        }

        private void ToggleRadioStreamer()
        {
            if (_radioTuner.IsRadioOn) // if online radio is on
            {
                var vehicle = Game.Player.Character.CurrentVehicle;
                vehicle.IsRadioEnabled = true;
                _vehicleRadioManager.RemoveVehicleCustomRadio(vehicle.Handle);
                _radioTuner.StopCurrent();
            }
            else
            {
                Game.Player.Character.CurrentVehicle.IsRadioEnabled = false;
                _radioTuner.Play(0);

                HandleRadioChangeKeyDowns(true);
            }
        }

        private void HandleRadioChangeKeyDowns(bool forced)
        {
            if (forced || (_vehicleRadioManager.GetVehicleCustomRadio(_lastKnownVehicleHandle) != null && (Game.IsEnabledControlPressed(2, Control.VehicleNextRadio) || Game.IsEnabledControlPressed(2, Control.VehiclePrevRadio))))
            {
                bool radioChangeHandled = false;
                var nextRadio = _radioTuner.CurrentStation;
                var stopWatch = new Stopwatch();
                stopWatch.Start();

                // action to handle the things coming with radio change
                Action handleRadioChange = () =>
                {
                    radioChangeHandled = true;
                    Audio.PlaySoundFrontend("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                    stopWatch.Restart();
                };

                // within 1 sec, player can change the current online radio station, and until that, infos are shown
                while (stopWatch.ElapsedMilliseconds < 1000)
                {
                    bool isNextStationKeyDown = Game.IsEnabledControlPressed(2, Control.VehicleNextRadio);
                    bool isPreviousStationKeyDown = Game.IsEnabledControlPressed(2, Control.VehiclePrevRadio);

                    if (!radioChangeHandled && isPreviousStationKeyDown)
                    {
                        nextRadio = _radioTuner.MoveToPreviousStation();
                        handleRadioChange();
                    }
                    else if (!radioChangeHandled && isNextStationKeyDown)
                    {
                        nextRadio = _radioTuner.MoveToNextStation();
                        handleRadioChange();
                    }
                    else if (!isPreviousStationKeyDown && !isNextStationKeyDown)
                    {
                        radioChangeHandled = false;
                    }

                    _radioTuner.KeepStreamAlive(); // don't forget about keeping alive the current radio!

                    DrawRadioLogoWithInfo(nextRadio);
                    Wait(0);
                }

                Game.Player.Character.CurrentVehicle.IsRadioEnabled = false; // turn off the radio in the car
                _radioTuner.ActivateNextStation(); // change to the next station

                _vehicleRadioManager.RegisterVehicleWithRadio(Game.Player.Character.CurrentVehicle.Handle, _radioTuner.CurrentStation);
            }
        }

        protected override void Dispose(bool wut)
        {
            _radioTuner.Dispose();
            Logger.Close();
        }

    }
}
