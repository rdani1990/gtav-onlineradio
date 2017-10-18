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
using GTAV_OnlineRadio.AsiLibrary.RadioPlayers;
using System.Threading.Tasks;

namespace GTAV_OnlineRadio.AsiLibrary
{

    public class OnlineRadioScript : Script
    {
        private bool _isInVehicle;
        private bool _isPressingToggleRadioButton;
        private int _lastKnownVehicleHandle;
        private VehicleRadioManager _vehicleRadioManager; // the known turned on radios for vehicles

        public bool IsOnlineRadioPlayingAllowed
        {
            get
            {
                var player = Game.Player;
                return player.Character.IsInVehicle() && player.CanControlCharacter && !player.IsDead && !Function.Call<bool>(Hash.IS_PLAYER_BEING_ARRESTED, new InputArgument[] { player.Handle, true });
            }
        }

        public OnlineRadioScript()
        {
            RadioTuner.Instance.RadioLoadingCompleted += OnRadioLoadingCompleted;

            Task.Run((Action)RadioTuner.Instance.LoadRadios);
        }

        private void OnRadioLoadingCompleted(object sender, EventArgs e)
        {
            if (RadioTuner.Instance.HasRadios) // only do anything if there's any radio available...
            {
                Tick += OnTick;
                Interval = 100;

                KeyDown += OnKeyDown;
                KeyUp += OnKeyUp;

                _vehicleRadioManager = new VehicleRadioManager();

                Radio.PauseIfNotNofified = true;
            }
            else
            {
                Logger.Instance.Log("No radios were found. The script is shutting down...");
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            var player = Game.Player.Character;
            if (_isInVehicle != player.IsInVehicle()) // player state within car has changed against its last known value
            {
                _isInVehicle = player.IsInVehicle();

                if (_isInVehicle)
                {
                    _lastKnownVehicleHandle = player.CurrentVehicle.Handle;
                    var customRadio = _vehicleRadioManager.GetVehicleCustomRadio(_lastKnownVehicleHandle);
                    if (customRadio != null) // if the online radio was turned on for this vehicle previously, then start the radio
                    {
                        customRadio.Play();
                    }
                }
            }

            var radioTuner = RadioTuner.Instance;

            if (radioTuner.CurrentStation != null)
            {
                bool isOnlineRadioPlayingAllowed = IsOnlineRadioPlayingAllowed;
                radioTuner.CurrentStation.KeepAlive();

                Audio.SetAudioFlag(AudioFlag.WantedMusicDisabled, isOnlineRadioPlayingAllowed);
                Audio.SetAudioFlag(AudioFlag.DisableFlightMusic, isOnlineRadioPlayingAllowed);

                if (isOnlineRadioPlayingAllowed)
                {
                    radioTuner.CurrentStation.HasOngoingConversation = (Function.Call<bool>(Hash.IS_SCRIPTED_CONVERSATION_ONGOING) || Function.Call<bool>(Hash.IS_MOBILE_PHONE_CALL_ONGOING));
                    HandleRadioChangeKeyDowns(false);
                }
                else
                {
                    if (_isInVehicle)
                    {
                        radioTuner.PauseCurrent(); // probably player just hop out from the vehicle for a sec, we suspend it to start quickier if he hops in 
                    }
                    else
                    {
                        radioTuner.StopCurrent();
                    }
                }
            }
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
                RadioTuner.Instance.CurrentStation?.Suspend(true);
            }
            else if (e.KeyCode == Keys.Z && !_isPressingToggleRadioButton && IsOnlineRadioPlayingAllowed)
            {
                _isPressingToggleRadioButton = true;

                ToggleRadioStreamer();
            }
            else if (e.KeyCode == Keys.BrowserFavorites)
            {
                string currentTrack = RadioTuner.Instance.CurrentStation?.CurrentTrackMetaData?.ToString();

                if (!String.IsNullOrEmpty(currentTrack))
                {
                    RadioTuner.Instance.LogCurrentTrack();
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

                //Logger.Instance.Log("{0}; {1}", screenWidth, screenHeight);
                //Logger.Instance.Log("AR: {0}", Function.Call<float>(Hash._GET_SCREEN_ASPECT_RATIO, new InputArgument[] { false }));

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
            if (RadioTuner.Instance.IsRadioOn) // if online radio is on
            {
                var vehicle = Game.Player.Character.CurrentVehicle;
                vehicle.IsRadioEnabled = true;
                _vehicleRadioManager.RemoveVehicleCustomRadio(vehicle.Handle);
                RadioTuner.Instance.StopCurrent();
            }
            else
            {
                Game.Player.Character.CurrentVehicle.IsRadioEnabled = false;
                RadioTuner.Instance.Play(0);

                HandleRadioChangeKeyDowns(true);
            }
        }

        private void HandleRadioChangeKeyDowns(bool forced)
        {
            if (forced || (_vehicleRadioManager.IsVehicleCustomRadioOn(_lastKnownVehicleHandle) && (Game.IsControlPressed(2, Control.VehicleNextRadio) || Game.IsControlPressed(2, Control.VehiclePrevRadio))))
            {
                bool radioChangeHandled = false;
                var nextRadio = RadioTuner.Instance.CurrentStation;
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
                    bool isNextStationKeyDown = Game.IsControlPressed(2, Control.VehicleNextRadio);
                    bool isPreviousStationKeyDown = Game.IsControlPressed(2, Control.VehiclePrevRadio);

                    if (!radioChangeHandled && isPreviousStationKeyDown)
                    {
                        nextRadio = RadioTuner.Instance.MoveToPreviousStation();
                        handleRadioChange();
                    }
                    else if (!radioChangeHandled && isNextStationKeyDown)
                    {
                        nextRadio = RadioTuner.Instance.MoveToNextStation();
                        handleRadioChange();
                    }
                    else if (!isPreviousStationKeyDown && !isNextStationKeyDown)
                    {
                        radioChangeHandled = false;
                    }

                    RadioTuner.Instance.KeepStreamAlive(); // don't forget about keeping alive the current radio!

                    DrawRadioLogoWithInfo(nextRadio);
                    Wait(0);
                }

                Game.Player.Character.CurrentVehicle.IsRadioEnabled = false; // turn off the radio in the car
                RadioTuner.Instance.ActivateNextStation(); // change to the next station

                _vehicleRadioManager.SetVehicleCustomRadio(Game.Player.Character.CurrentVehicle.Handle, RadioTuner.Instance.CurrentStation);
            }
        }

        protected override void Dispose(bool wut)
        {
            RadioTuner.Instance.Dispose();
            Logger.Instance.Dispose();
        }

    }
}
