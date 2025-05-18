using System.IO.Ports;
using IRIS.Communication;
using IRIS.Communication.Types;
using IRIS.Serial.Addressing;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace IRIS.Serial.Communication
{
    /// <summary>
    /// Serial port interface for communication with devices.
    /// </summary>
    public sealed class SerialPortInterface : SerialPort, IRawDataCommunicationInterface<SerialPortDeviceAddress>
    {
        /// <summary>
        /// Used when reading data stream by single character to prevent unnecessary allocations
        /// </summary>
        private readonly byte[] _singleCharReadBuffer = new byte[1];
       
        public event Delegates.DeviceConnectedHandler<SerialPortDeviceAddress>? DeviceConnected;
        public event Delegates.DeviceDisconnectedHandler<SerialPortDeviceAddress>? DeviceDisconnected;
        public event Delegates.DeviceConnectionLostHandler<SerialPortDeviceAddress>? DeviceConnectionLost;
        
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
        /// Connect to device - open port and start reading data
        /// </summary>
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
        /// Transmit data to device over serial port
        /// </summary>
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
        /// Read data from device over serial port
        /// </summary>
        /// <param name="length">Amount of data to read</param>
        /// <param name="cancellationToken">Used to cancel read operation</param>
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
        /// Reads data until specified byte is found
        /// </summary>
        /// <param name="receivedByte">Byte to find</param>
        /// <param name="cancellationToken">Used to cancel read operation</param>
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