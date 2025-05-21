using System.Runtime.InteropServices;
using IRIS.Addressing.Abstract;

namespace IRIS.Serial.Addressing
{
    /// <summary>
    ///     Represents a serial port device address for cross-platform serial communication.
    ///     This immutable struct handles platform-specific serial port naming conventions
    ///     for Windows (COM ports), Linux (tty devices), and macOS.
    /// </summary>
    /// <remarks>
    ///     <para>Windows examples: COM1, COM3</para>
    ///     <para>Linux examples: /dev/ttyUSB0, /dev/ttyACM0</para>
    ///     <para>macOS examples: /dev/cu.usbmodem1421</para>
    /// </remarks>
    public readonly struct SerialPortDeviceAddress : IDeviceAddress<string>, IEquatable<SerialPortDeviceAddress>
    {
        /// <summary>
        ///     Gets the platform-specific serial port address string
        /// </summary>
        /// <value>
        ///     The full serial port path/name (e.g. "COM3" on Windows or "/dev/ttyUSB0" on Linux)
        /// </value>
        public string Address { get; }

        /// <summary>
        ///     Creates a Linux USB serial port address (/dev/ttyUSB{portIndex})
        /// </summary>
        /// <param name="portIndex">The USB port index (0-based)</param>
        /// <returns>A new SerialPortDeviceAddress for the specified Linux USB port</returns>
        public static SerialPortDeviceAddress LinuxUSB(int portIndex) => 
            new SerialPortDeviceAddress($"/dev/ttyUSB{portIndex}");

        /// <summary>
        ///     Creates a Linux ACM (Abstract Control Model) serial port address (/dev/ttyACM{portIndex})
        /// </summary>
        /// <param name="portIndex">The ACM port index (0-based)</param>
        /// <returns>A new SerialPortDeviceAddress for the specified Linux ACM port</returns>
        public static SerialPortDeviceAddress LinuxACM(int portIndex) =>
            new SerialPortDeviceAddress($"/dev/ttyACM{portIndex}");

        /// <summary>
        ///     Creates a Windows COM port address (COM{portIndex})
        /// </summary>
        /// <param name="portIndex">The COM port number (1-based)</param>
        /// <returns>A new SerialPortDeviceAddress for the specified Windows COM port</returns>
        public static SerialPortDeviceAddress Windows(int portIndex) =>
            new SerialPortDeviceAddress($"COM{portIndex}");

        /// <summary>
        ///     Creates a SerialPortDeviceAddress from a numeric port index using platform-specific conventions
        /// </summary>
        /// <param name="deviceAddress">The port index/number</param>
        /// <exception cref="PlatformNotSupportedException">
        ///     Thrown when the platform is not supported (macOS/BSD) or when no valid port is found on Linux
        /// </exception>
        /// <remarks>
        ///     <para>On Windows: Creates COM{deviceAddress} (e.g. COM3 for deviceAddress=3)</para>
        ///     <para>On Linux: Attempts multiple common tty device patterns (USB, ACM, AMA, SAC, S)</para>
        ///     <para>macOS/BSD: Not supported due to non-standardized port naming</para>
        /// </remarks>
        public SerialPortDeviceAddress(int deviceAddress)
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Address = $"COM{deviceAddress}";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try to connect via USB
                Address = $"/dev/ttyUSB{deviceAddress}";
                
                // Check if Linux USB port exists, if not try to connect via ACM
                if (!File.Exists(Address))
                    Address = $"/dev/ttyACM{deviceAddress}";
                
                // Check if Linux ACM port exists, if not try to connect via AMA
                if (!File.Exists(Address))
                    Address = $"/dev/ttyAMA{deviceAddress}";
                
                // Check if Linux ACM port exists, if not try to connect via SAC
                if (!File.Exists(Address))
                    Address = $"/dev/ttySAC{deviceAddress}";
                
                // Check if Linux ACM port exists, if not try regular COM/RS232 port
                if (!File.Exists(Address))
                    Address = $"/dev/ttyS{deviceAddress}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new PlatformNotSupportedException("OSX port number uses weird naming for USB and thus is not supported via numeric index");
            }
            else
                throw new PlatformNotSupportedException();
        }

        /// <summary>
        ///     Creates a SerialPortDeviceAddress from a string address with platform-specific normalization
        /// </summary>
        /// <param name="deviceAddress">The serial port address string</param>
        /// <remarks>
        ///     <para>On Linux: Normalizes device paths to /dev/tty* format</para>
        ///     <para>On Windows: Normalizes COM port names (case-insensitive, adds COM prefix if needed)</para>
        ///     <para>On macOS/other: Uses the address as-is</para>
        /// </remarks>
        public SerialPortDeviceAddress(string deviceAddress) 
        {
            // Check if platform is Linux
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Check if file name starts with /dev
                if (deviceAddress.StartsWith("/dev", StringComparison.InvariantCulture))
                    Address = deviceAddress;
                else if(deviceAddress.StartsWith("dev", StringComparison.InvariantCulture))
                    Address = $"/{deviceAddress}";
                else
                    Address = $"/dev/{deviceAddress}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Check if file name starts with COM
                if (deviceAddress.StartsWith("COM", StringComparison.InvariantCulture))
                    Address = deviceAddress;
                // Attempt to fix COM port name if case is wrong
                else if(deviceAddress.StartsWith("com", StringComparison.InvariantCulture))
                    Address = $"COM{deviceAddress.Replace("com", "")}";
                // Attempt to connect to COM port if only number is provided
                else
                    Address = $"COM{deviceAddress}";
            }
            else
            {
                // OSX and other platforms require full path
                Address = deviceAddress;
            }
        }

        /// <summary>
        ///     Equality operator comparing two SerialPortDeviceAddress instances
        /// </summary>
        public static bool operator ==(SerialPortDeviceAddress left, SerialPortDeviceAddress right) => left.Address == right.Address;

        /// <summary>
        ///     Inequality operator comparing two SerialPortDeviceAddress instances
        /// </summary>
        public static bool operator !=(SerialPortDeviceAddress left, SerialPortDeviceAddress right) => !(left == right);

        /// <summary>
        ///     Determines whether the specified SerialPortDeviceAddress is equal to the current instance
        /// </summary>
        public bool Equals(SerialPortDeviceAddress other) => Address == other.Address;

        /// <summary>
        ///     Determines whether the specified object is equal to the current instance
        /// </summary>
        public override bool Equals(object? obj) => obj is SerialPortDeviceAddress other && Equals(other);

        /// <summary>
        ///     Gets a hash code for the current instance
        /// </summary>
        public override int GetHashCode() => Address.GetHashCode();
    }
}
