using IRIS.Protocols.IRIS;
using IRIS.Protocols.IRIS.Data;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;
using IRIS.Serial.Devices;

namespace IRIS.Serial.Implementations
{
    /// <summary>
    /// Base class for RUSTIC devices
    /// </summary>
    public abstract class RUSTICSerialDeviceBase(
        SerialPortDeviceAddress deviceAddress,
        SerialInterfaceSettings settings
    ) : SerialDeviceBase(deviceAddress, settings)
    {
        /// <summary>
        /// Sends SET message to device and returns the response <br/>
        /// E.g. PROPERTY to desired value
        /// </summary>
        protected RUSTICDeviceProperty? SetProperty<TValueType>(string message, TValueType value)
            where TValueType : notnull
            => RUSTIC<CachedSerialPortInterface>.SetProperty(message, value.ToString() ?? string.Empty,
                HardwareAccess, 100);

        /// <summary>
        /// Sends GET message to device and returns the response <br/>
        /// </summary>
        protected RUSTICDeviceProperty? GetProperty(string propertyName)
            => RUSTIC<CachedSerialPortInterface>.GetProperty(propertyName, HardwareAccess);
    }
}