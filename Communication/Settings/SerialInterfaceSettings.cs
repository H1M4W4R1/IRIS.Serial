using System.IO.Ports;

namespace IRIS.Serial.Communication.Settings
{
    /// <summary>
    /// Represents the configuration settings for a serial communication interface.
    /// This immutable struct encapsulates all necessary parameters for establishing
    /// a serial port connection, including baud rate, parity, data bits, stop bits,
    /// and flow control settings.
    /// </summary>
    /// <remarks>
    /// <para>This struct provides sensible defaults for common serial communication scenarios.</para>
    /// <para>Typical usage involves creating an instance with desired parameters or using the Default static instance.</para>
    /// </remarks>
    public readonly struct SerialInterfaceSettings(int baudRate = 115200, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One,
        bool useDtr = false, bool useRts = false)
    {
        /// <summary>
        /// Provides a default configuration instance with 115200 baud rate and other common settings.
        /// This static instance serves as a convenient starting point for most serial communication needs.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public static readonly SerialInterfaceSettings Default = new(115200);
        
        /// <summary>
        /// Gets the baud rate (bits per second) for serial communication.
        /// This determines the speed at which data is transmitted over the serial connection.
        /// Common values include 9600, 19200, 38400, 57600, and 115200.
        /// Default: 115200
        /// </summary>
        public readonly int baudRate = baudRate;
        
        /// <summary>
        /// Gets the parity checking protocol used for error detection.
        /// Parity helps detect transmission errors by ensuring an even or odd number of bits.
        /// Default: None (no parity checking)
        /// </summary>
        public readonly Parity parity = parity;
        
        /// <summary>
        /// Gets the number of data bits in each byte transmitted.
        /// This determines the size of the basic data unit in the communication.
        /// Common values are 7 or 8 bits per byte.
        /// Default: 8
        /// </summary>
        public readonly int dataBits = dataBits;
        
        /// <summary>
        /// Gets the number of stop bits that indicate the end of a data packet.
        /// Stop bits provide timing synchronization between devices.
        /// Default: One (1 stop bit)
        /// </summary>
        public readonly StopBits stopBits = stopBits;
        
        /// <summary>
        /// Gets a value indicating whether the Data Terminal Ready (DTR) signal is enabled.
        /// DTR is a flow control signal that indicates the device is ready to communicate.
        /// Default: false (DTR disabled)
        /// </summary>
        public readonly bool dtrEnable = useDtr;

        /// <summary>
        /// Gets a value indicating whether the Request to Send (RTS) signal is enabled.
        /// RTS is a flow control signal used to request permission to send data.
        /// Default: false (RTS disabled)
        /// </summary>
        public readonly bool rtsEnable = useRts;
    }
}