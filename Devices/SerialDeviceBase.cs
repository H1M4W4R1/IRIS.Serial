using IRIS.Devices;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;

namespace IRIS.Serial.Devices
{
    /// <summary>
    /// Base class representing a device connected via serial port (COM port).
    /// Provides common functionality for serial port communication and device management.
    /// 
    /// <para>Examples of serial port addresses:</para>
    /// <list type="bullet">
    /// <item>Windows: COM1, COM9</item>
    /// <item>Linux: /dev/ttyUSB0, /dev/ttyACM0</item>
    /// </list>
    /// 
    /// <typeparam name="CachedSerialPortInterface">The type of hardware interface used for communication</typeparam>
    /// <typeparam name="SerialPortDeviceAddress">The type representing the device's address</typeparam>
    /// </summary>
    public abstract class SerialDeviceBase : DeviceBase<CachedSerialPortInterface, SerialPortDeviceAddress>
    {
        /// <summary>
        /// Initializes a new instance of the serial device with specified address and communication settings
        /// </summary>
        /// <param name="deviceAddress">The address of the serial port device</param>
        /// <param name="settings">Configuration settings for the serial port communication including:
        /// <list type="bullet">
        /// <item>Baud rate</item>
        /// <item>Parity</item>
        /// <item>Data bits</item>
        /// <item>Stop bits</item>
        /// <item>DTR enable</item>
        /// <item>RTS enable</item>
        /// </list>
        /// </param>
        protected SerialDeviceBase(SerialPortDeviceAddress deviceAddress, SerialInterfaceSettings settings)
        {
            HardwareAccess =
                new CachedSerialPortInterface(deviceAddress.Address, settings.baudRate, settings.parity,
                    settings.dataBits, settings.stopBits,
                    settings.dtrEnable, settings.rtsEnable);
        }

        /// <summary>
        /// Changes the device's serial port address while maintaining connection state
        /// </summary>
        /// <param name="deviceAddress">New serial port address to use</param>
        /// <param name="cancellationToken">Optional cancellation token that can be used to abort the connection attempt if the port was previously open</param>
        /// <remarks>
        /// This method handles the port transition gracefully by:
        /// <list type="number">
        /// <item>Checking current connection state</item>
        /// <item>Disconnecting if currently connected</item>
        /// <item>Updating the port address</item>
        /// <item>Reconnecting if previously connected</item>
        /// </list>
        /// </remarks>
        public void SetAddress(
            SerialPortDeviceAddress deviceAddress,
            CancellationToken cancellationToken = default)
        {
            // Check if port is open
            bool wasPortOpen = HardwareAccess.IsOpen;
            if (wasPortOpen) HardwareAccess.Disconnect();

            HardwareAccess.PortName = deviceAddress.ToString();

            // If port was open then connect again
            if (wasPortOpen) HardwareAccess.Connect(cancellationToken);
        }
    }
}