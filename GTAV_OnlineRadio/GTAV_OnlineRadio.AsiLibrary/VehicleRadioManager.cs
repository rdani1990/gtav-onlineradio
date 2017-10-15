using GTAV_OnlineRadio.AsiLibrary.RadioPlayers;
using System.Collections.Generic;

namespace GTAV_OnlineRadio.AsiLibrary
{
    /// <summary>
    /// Maintains the list of vehicles which has custom radio on.
    /// </summary>
    public class VehicleRadioManager
    {
        private Dictionary<int, Radio> _vehicleRadios;

        public VehicleRadioManager()
        {
            _vehicleRadios = new Dictionary<int, Radio>();
        }

        public void SetVehicleCustomRadio(int vehiclePtr, Radio radio)
        {
            _vehicleRadios[vehiclePtr] = radio;
        }

        public Radio GetVehicleCustomRadio(int vehiclePtr)
        {
            return (_vehicleRadios.ContainsKey(vehiclePtr) ? _vehicleRadios[vehiclePtr] : null);
        }

        public void RemoveVehicleCustomRadio(int vehiclePtr)
        {
            _vehicleRadios.Remove(vehiclePtr);
        }

        public bool IsVehicleCustomRadioOn(int vehiclePtr)
        {
            return _vehicleRadios.ContainsKey(vehiclePtr);
        }
    }
}
