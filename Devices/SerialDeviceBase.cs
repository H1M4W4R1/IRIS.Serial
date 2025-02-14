﻿using IRIS.Devices;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;

namespace IRIS.Serial.Devices
{
    /// <summary>
    /// Represents device connected to computer via serial port
    /// For example COM9 or /dev/ttyUSB0
    /// </summary>
    public abstract class SerialDeviceBase : DeviceBase<CachedSerialPortInterface, SerialPortDeviceAddress>
    {
        /// <summary>
        /// Create serial device with specific address and settings
        /// </summary>
        protected SerialDeviceBase(SerialPortDeviceAddress deviceAddress, SerialInterfaceSettings settings)
        {
            HardwareAccess =
                new CachedSerialPortInterface(deviceAddress.Address, settings.baudRate, settings.parity,
                    settings.dataBits, settings.stopBits,
                    settings.dtrEnable, settings.rtsEnable);
        }

        /// <summary>
        /// Change device address to new one
        /// </summary>
        public async ValueTask SetAddress(
            SerialPortDeviceAddress deviceAddress,
            CancellationToken cancellationToken = default)
        {
            // Check if port is open
            bool wasPortOpen = HardwareAccess.IsOpen;
            if (wasPortOpen) await HardwareAccess.Disconnect(cancellationToken);

            HardwareAccess.PortName = deviceAddress.ToString();

            // If port was open then connect again
            if (wasPortOpen) await HardwareAccess.Connect(cancellationToken);
        }
    }
}