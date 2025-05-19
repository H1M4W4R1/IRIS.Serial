using IRIS.Protocols.IRIS;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;
using IRIS.Serial.Devices;

namespace IRIS.Serial.Implementations
{
    /// <summary>
    /// Abstract base class for devices using the REAP (Register-based Embedded Access Protocol)
    /// over a serial connection. Inherits from SerialDeviceBase to provide common serial device functionality.
    /// </summary>
    /// <param name="deviceAddress">The serial port address of the device</param>
    /// <param name="settings">Interface settings for the serial communication</param>
    public abstract class REAPSerialDeviceBase(SerialPortDeviceAddress deviceAddress,
        SerialInterfaceSettings settings) : SerialDeviceBase(deviceAddress, settings)
    {
        /// <summary>
        /// Sets the value of a device register using the REAP protocol.
        /// </summary>
        /// <param name="register">The register number to write to</param>
        /// <param name="value">The value to write to the register</param>
        /// <returns>
        /// A ValueTask containing the actual value written (which may differ from requested value)
        /// or null if the operation failed. Uses REAP protocol via cached serial port interface.
        /// </returns>
        public ValueTask<uint?> SetRegister(uint register, uint value) =>
            REAP<CachedSerialPortInterface>.SetRegister(HardwareAccess, register, value);
        
        /// <summary>
        /// Reads the value of a device register using the REAP protocol.
        /// </summary>
        /// <param name="register">The register number to read from</param>
        /// <returns>
        /// A ValueTask containing the register value or null if the operation failed.
        /// Uses REAP protocol via cached serial port interface.
        /// </returns>
        public ValueTask<uint?> GetRegister(uint register) =>
            REAP<CachedSerialPortInterface>.GetRegister(HardwareAccess, register);
    }
}