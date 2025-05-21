using System.Management;
using System.Runtime.InteropServices;
using IRIS.Addressing.Ports;
using IRIS.Recognition;
using IRIS.Serial.Addressing;
using Microsoft.Win32;

namespace IRIS.Serial.Recognition
{
    /// <summary>
    ///     Windows-specific implementation of a USB serial port device watcher.
    ///     Scans for available USB serial port devices by querying Windows Management Instrumentation (WMI)
    ///     and the Windows Registry. Validates devices by checking Vendor ID (VID) and Product ID (PID).
    ///     When constructed with a specific USBDeviceAddress, only devices matching those identifiers will be returned.
    ///     When constructed without a specific address (null), all CDC (Communications Device Class) devices will be returned.
    /// </summary>
    public sealed class WindowsUSBSerialPortDeviceWatcher(USBDeviceAddress? hwAddress = null)
        : DeviceWatcherBase<WindowsUSBSerialPortDeviceWatcher, USBDeviceAddress, SerialPortDeviceAddress>
    {
        /// <summary>
        ///     Gets the hardware address filter for this watcher.
        ///     When null, the watcher will return all detected CDC devices.
        ///     When set, the watcher will only return devices matching the specified VID/PID combination.
        /// </summary>
        private USBDeviceAddress? HardwareAddress { get; } = hwAddress;

        /// <summary>
        ///     Scans the system for available USB serial port devices.
        ///     This method:
        ///     1. Verifies the platform is Windows (throws PlatformNotSupportedException otherwise)
        ///     2. Queries WMI for all PnP devices
        ///     3. Filters for devices in the "PORTS" class (GUID: 4D36E978-E325-11CE-BFC1-08002BE10318)
        ///     4. Extracts port information from the Windows Registry
        ///     5. Parses device IDs to get VID/PID information
        ///     6. Applies hardware address filtering if specified
        ///     Returns a tuple containing:
        ///     - List of hardware addresses (VID/PID combinations)
        ///     - List of corresponding serial port addresses (COM port names)
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        /// <returns>Tuple of hardware and software device addresses</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows</exception>
        protected override ValueTask<(List<USBDeviceAddress>, List<SerialPortDeviceAddress>)>
            ScanForDevicesAsync(CancellationToken cancellationToken)
        {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("This class is only supported on Windows platform.");
            
            // Create lists for hardware and software devices
            List<USBDeviceAddress> hardwareDevices = [];
            List<SerialPortDeviceAddress> softwareDevices = [];
            
            // Create entity
            using ManagementClass registerAccessEntity = new("Win32_PnPEntity");
       
            // Registry path
            const string localMachineSystemCurrentControlSet = "HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\";

            // Loop through all instances
            foreach (ManagementBaseObject? o in registerAccessEntity.GetInstances())
            {
                ManagementObject? registryObject = (ManagementObject) o;
                object? registryGUID = registryObject.GetPropertyValue("ClassGuid");
                if (registryGUID == null || registryGUID.ToString()?.ToUpper() != "{4D36E978-E325-11CE-BFC1-08002BE10318}")
                    continue; // Skip all devices except device class "PORTS"

                string? deviceID = registryObject.GetPropertyValue("PnpDeviceID").ToString();
                string registryDeviceEnumerationDataPath = localMachineSystemCurrentControlSet + "Enum\\" + deviceID + "\\Device Parameters";
                string? portName = Registry.GetValue(registryDeviceEnumerationDataPath, "PortName", "")?.ToString();

                // Check if ID exists
                if (deviceID == null) continue;

                // Skip if not a port
                if (portName == null) continue;

                // Parse Device ID
                string[] parsedId = deviceID.Split("\\", StringSplitOptions.RemoveEmptyEntries);
                if (parsedId.Length <= 0) continue;

                // Check if is usb
                if (parsedId[0] != "USB") continue;

                // Split VID and PID
                string[] vendorAndProductIdentifiers = parsedId[1].Split("&");
                if (vendorAndProductIdentifiers.Length < 2) // Some devices have shitty names with more parameters
                    continue;

                // Remove trash data
                string vid1 = vendorAndProductIdentifiers[0].Replace("VID_", "");
                string pid1 = vendorAndProductIdentifiers[1].Replace("PID_", "");

                // Check if hardware address is set, if not return all CDC devices connected
                if (HardwareAddress == null)
                {
                    // Create serial port address
                    SerialPortDeviceAddress deviceAddress = new(portName);
                    USBDeviceAddress hardwareAddress = new(vid1, pid1);
                    
                    // Add device to list
                    hardwareDevices.Add(hardwareAddress);
                    softwareDevices.Add(deviceAddress);
                }
                else
                {
                    // Check if device is meeting VID and PID criteria
                    if (vid1 != HardwareAddress.Value.VID || pid1 != HardwareAddress.Value.PID) continue;
                    
                    // Create serial port address
                    SerialPortDeviceAddress deviceAddress = new(portName);
                    
                    // Add device to list
                    hardwareDevices.Add(HardwareAddress.Value);
                    softwareDevices.Add(deviceAddress);
                }
            }
            
            // Return devices
            return ValueTask.FromResult((hardwareDevices, softwareDevices));
        }
    }
}