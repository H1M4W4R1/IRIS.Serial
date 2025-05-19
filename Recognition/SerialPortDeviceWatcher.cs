using System.IO.Ports;
using IRIS.Recognition;
using IRIS.Serial.Addressing;

namespace IRIS.Serial.Recognition
{
    /// <summary>
    /// Provides basic serial port device detection by enumerating all available serial ports on the system.
    /// This implementation uses the standard <see cref="SerialPort.GetPortNames"/> method to discover ports.
    /// 
    /// <para><strong>Important Limitations:</strong></para>
    /// <list type="bullet">
    /// <item>Does not perform any device-specific identification (e.g., USB VID/PID checking)</item>
    /// <item>Will detect all available serial ports regardless of device compatibility</item>
    /// <item>May include virtual COM ports that aren't connected to physical devices</item>
    /// </list>
    /// 
    /// <para><strong>Recommended Usage:</strong></para>
    /// <para>This watcher is primarily provided for backward compatibility and basic scenarios. For most production use cases,
    /// <see cref="WindowsUSBSerialPortDeviceWatcher"/> provides more reliable device detection by filtering for
    /// specific USB hardware identifiers.</para>
    /// </summary>
    /// <remarks>
    /// The watcher returns identical lists for both hardware and software devices since it cannot distinguish
    /// between physical devices and virtual ports at this basic detection level.
    /// </remarks>
    public sealed class SerialPortDeviceWatcher : DeviceWatcherBase<SerialPortDeviceWatcher, SerialPortDeviceAddress>
    {
        /// <summary>
        /// Scans the system for all available serial ports and returns them as device addresses.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests. Note: This operation is synchronous and cancellation has no effect.</param>
        /// <returns>
        /// A ValueTask containing a tuple of two lists:
        /// <list type="number">
        /// <item>First list contains hardware device addresses (all detected ports in this implementation)</item>
        /// <item>Second list contains software device addresses (identical to hardware list in this implementation)</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// This implementation performs a synchronous scan operation but returns a ValueTask for API consistency.
        /// The operation is not actually cancellable due to the nature of SerialPort.GetPortNames().
        /// </remarks>
        protected override ValueTask<(List<SerialPortDeviceAddress>, List<SerialPortDeviceAddress>)>
            ScanForDevicesAsync(CancellationToken cancellationToken)
        {
            // Get all available serial ports
            string[] ports = SerialPort.GetPortNames();
            
            // Create lists for hardware and software devices
            List<SerialPortDeviceAddress> hardwareDevices = new();
            List<SerialPortDeviceAddress> softwareDevices = new();
            
            // Loop through all ports
            for (int serialPortIndex = 0; serialPortIndex < ports.Length; serialPortIndex++)
            {
                string port = ports[serialPortIndex];
                // Create device address
                SerialPortDeviceAddress deviceAddress = new(port);

                // Add device to list
                hardwareDevices.Add(deviceAddress);
                softwareDevices.Add(deviceAddress);
            }
            
            // Return devices
            return ValueTask.FromResult((hardwareDevices, softwareDevices));
        }
    }
}