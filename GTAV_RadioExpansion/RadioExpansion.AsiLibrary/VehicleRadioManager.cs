using RadioExpansion.Core.RadioPlayers;
using System.Collections.Generic;

namespace RadioExpansion.AsiLibrary
{
    /// <summary>
    /// Pairs vehicles with custom radio info.
    /// </summary>
    public class VehicleRadioManager
    {
        private Dictionary<int, Radio> _vehicleRadios;

        public VehicleRadioManager()
        {
            _vehicleRadios = new Dictionary<int, Radio>();
        }

        public void RegisterVehicleWithRadio(int vehiclePtr, Radio radio)
        {
            _vehicleRadios[vehiclePtr] = radio;
        }

        public Radio GetVehicleCustomRadio(int vehiclePtr)
        {
            return (_vehicleRadios.ContainsKey(vehiclePtr) ? _vehicleRadios[vehiclePtr] : null);
        }

        public void RemoveVehicleCustomRadio(int vehiclePtr)
        {
            _vehicleRadios[vehiclePtr] = null;
        }

        public bool HasVehicleRadioInfo(int vehiclePtr)
        {
            return _vehicleRadios.ContainsKey(vehiclePtr);
        }
    }
}
