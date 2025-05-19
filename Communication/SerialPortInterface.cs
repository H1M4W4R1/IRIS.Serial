using System.IO.Ports;
using IRIS.Communication;
using IRIS.Communication.Types;
using IRIS.Serial.Addressing;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace IRIS.Serial.Communication
{
    /// <summary>
    /// Provides a serial port interface implementation for device communication.
    /// This sealed class inherits from System.IO.Ports.SerialPort and implements
    /// the IRawDataCommunicationInterface for SerialPortDeviceAddress.
    /// </summary>
    public sealed class SerialPortInterface : SerialPort, IRawDataCommunicationInterface<SerialPortDeviceAddress>
    {
        /// <summary>
        /// Single-byte buffer used for character-by-character reading to optimize memory usage
        /// </summary>
        private readonly byte[] _singleCharReadBuffer = new byte[1];
       
        /// <summary>
        /// Event triggered when a device successfully connects
        /// </summary>
        public event Delegates.DeviceConnectedHandler<SerialPortDeviceAddress>? DeviceConnected;
        
        /// <summary>
        /// Event triggered when a device is properly disconnected
        /// </summary>
        public event Delegates.DeviceDisconnectedHandler<SerialPortDeviceAddress>? DeviceDisconnected;
        
        /// <summary>
        /// Event triggered when connection to a device is unexpectedly lost
        /// </summary>
        public event Delegates.DeviceConnectionLostHandler<SerialPortDeviceAddress>? DeviceConnectionLost;
        
        /// <summary>
        /// Initializes a new instance of the SerialPortInterface with specified parameters
        /// </summary>
        /// <param name="portName">The name of the serial port (e.g. "COM1")</param>
        /// <param name="baudRate">The baud rate for communication</param>
        /// <param name="parity">The parity checking protocol</param>
        /// <param name="dataBits">The number of data bits per byte</param>
        /// <param name="stopBits">The number of stop bits</param>
        /// <param name="dtrEnable">Whether to enable Data Terminal Ready (DTR) signal</param>
        /// <param name="rtsEnable">Whether to enable Request to Send (RTS) signal</param>
        public SerialPortInterface(string portName,
            int baudRate,
            Parity parity,
            int dataBits,
            StopBits stopBits,
            bool dtrEnable,
            bool rtsEnable)
        {
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Parity = parity;
            StopBits = stopBits;
            Handshake = Handshake.None;
            DtrEnable = dtrEnable;
            RtsEnable = rtsEnable;
            NewLine = Environment.NewLine;
            ReceivedBytesThreshold = 1024;
        }

        /// <summary>
        /// Establishes connection with the serial device
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        /// <returns>ValueTask containing true if connection succeeded, false otherwise</returns>
        public ValueTask<bool> Connect(CancellationToken cancellationToken)
        {
            // If port is already open, return
            if(IsOpen) return ValueTask.FromResult(true);
            
            // Open the port
            Open();
            
            if(!IsOpen) return ValueTask.FromResult(false);
            
            // Invoke connected event
            DeviceConnected?.Invoke(new SerialPortDeviceAddress(PortName));
            return ValueTask.FromResult(true);
        }

        /// <summary>
        /// Terminates connection with the serial device
        /// </summary>
        /// <returns>ValueTask containing true if disconnection succeeded, false otherwise</returns>
        public ValueTask<bool> Disconnect()
        {
            // If port is not open, return
            if (!IsOpen) return ValueTask.FromResult(true);
            Close();
            
            // Invoke disconnected event
            DeviceDisconnected?.Invoke(new SerialPortDeviceAddress(PortName));
            return ValueTask.FromResult(true);
        }

#region IRawDataCommunicationInterface

        /// <summary>
        /// Sends raw data to the connected device
        /// </summary>
        /// <param name="data">Byte array containing data to transmit</param>
        /// <returns>ValueTask containing true if transmission succeeded, false otherwise</returns>
        ValueTask<bool> IRawDataCommunicationInterface.TransmitRawData(byte[] data)
        {
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return ValueTask.FromResult(false);
            }

            // Write data to device
            Write(data, 0, data.Length);

            return ValueTask.FromResult(true);
        }

        /// <summary>
        /// Reads a specified amount of raw data from the connected device
        /// </summary>
        /// <param name="length">Number of bytes to read</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        /// <returns>ValueTask containing the read data or empty array if failed</returns>
        ValueTask<byte[]> IRawDataCommunicationInterface.ReadRawData(
            int length,
            CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return ValueTask.FromResult(Array.Empty<byte>());
            }

            // Create buffer for data
            byte[] data = new byte[length];
            int bytesRead = 0;

            // Read data until all data is read
            while (bytesRead < length)
            {
                try
                {
                    // Create task to read data
                    Task<int> readTask = BaseStream.ReadAsync(data, bytesRead, length - bytesRead, cancellationToken);

                    // Wait for task to complete
                    while (!readTask.IsCompleted)
                    {
                        if (cancellationToken.IsCancellationRequested) return ValueTask.FromResult(Array.Empty<byte>());
                    }

                    bytesRead += readTask.Result;
                    if (cancellationToken.IsCancellationRequested) return ValueTask.FromResult(Array.Empty<byte>());
                }
                catch (TaskCanceledException)
                {
                    return ValueTask.FromResult(Array.Empty<byte>());
                }
            }

            // Return data
            return ValueTask.FromResult(data);
        }

        /// <summary>
        /// Reads data from the device until a specified byte is encountered
        /// </summary>
        /// <param name="receivedByte">The termination byte to search for</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        /// <returns>ValueTask containing all read data up to and including the termination byte</returns>
        ValueTask<byte[]> IRawDataCommunicationInterface.ReadRawDataUntil(
            byte receivedByte,
            CancellationToken cancellationToken)
        {
            // Check if device is open
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return ValueTask.FromResult(Array.Empty<byte>());
            }

            // Read data until byte is found
            List<byte> data = new List<byte>();

            // Read data until byte is found
            while (true)
            {
                Task<int> readTask = BaseStream.ReadAsync(_singleCharReadBuffer, 0, 1, cancellationToken);
                
                // Wait for task to complete
                while (!readTask.IsCompleted)
                {
                    if (cancellationToken.IsCancellationRequested) return ValueTask.FromResult(Array.Empty<byte>());
                }
                
                // Get bytes read
                int bytesRead = readTask.Result;
                
                // Check if cancellation is requested
                if (cancellationToken.IsCancellationRequested) break;
                
                // Check if data is read
                if (bytesRead == 0) continue;

                // If data is read, add it to list
                data.Add(_singleCharReadBuffer[0]);

                // Check if byte is found
                if (_singleCharReadBuffer[0] == receivedByte) break;
            }

            // Return data
            return ValueTask.FromResult(data.ToArray());
        }

#endregion
    }
}