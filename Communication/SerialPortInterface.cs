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
        public async ValueTask<bool> Connect(CancellationToken cancellationToken)
        {
            // If port is already open, return
            if(IsOpen) return true;
            
            // Open the port
            Open();
            
            if(!IsOpen) return false;
            
            // Invoke connected event
            DeviceConnected?.Invoke(new SerialPortDeviceAddress(PortName));
            return true;
        }

        public async ValueTask<bool> Disconnect(CancellationToken cancellationToken)
        {
            // If port is not open, return
            if (!IsOpen) return true;
            Close();
            
            // Invoke disconnected event
            DeviceDisconnected?.Invoke(new SerialPortDeviceAddress(PortName));
            return true;
        }

#region IRawDataCommunicationInterface

        /// <summary>
        /// Transmit data to device over serial port
        /// </summary>
        ValueTask IRawDataCommunicationInterface.TransmitRawData(byte[] data)
        {
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return ValueTask.CompletedTask;
            }

            // Write data to device
            Write(data, 0, data.Length);
            
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Read data from device over serial port
        /// </summary>
        /// <param name="length">Amount of data to read</param>
        /// <param name="cancellationToken">Used to cancel read operation</param>
        /// <returns></returns>
        async ValueTask<byte[]?> IRawDataCommunicationInterface.ReadRawData(int length, CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return [];
            }

            // Create buffer for data
            // TODO: Get rid of this allocation
            byte[] data = new byte[length];
            int bytesRead = 0;

            // Read data until all data is read
            while (bytesRead < length)
            {
                bytesRead += await BaseStream.ReadAsync(data, bytesRead, length - bytesRead, cancellationToken);
                if (cancellationToken.IsCancellationRequested) break;
            }

            // Return data
            return data;
        }

        /// <summary>
        /// Reads data until specified byte is found
        /// </summary>
        /// <param name="receivedByte">Byte to find</param>
        /// <param name="cancellationToken">Used to cancel read operation</param>
        /// <returns>Array of data, if byte is not found, empty array is returned</returns>
        async ValueTask<byte[]?> IRawDataCommunicationInterface.ReadRawDataUntil(byte receivedByte,
            CancellationToken cancellationToken)
        {
            // Check if device is open
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return [];
            }

            // Read data until byte is found
            // TODO: Get rid of this allocation
            List<byte> data = new List<byte>();

            // Read data until byte is found
            while (true)
            {
                int bytesRead = await BaseStream.ReadAsync(_singleCharReadBuffer, 0, 1, cancellationToken);
                if (cancellationToken.IsCancellationRequested) break;
                
                // Check if data is read
                if (bytesRead == 0) continue;

                // If data is read, add it to list
                data.Add(_singleCharReadBuffer[0]);

                // Check if byte is found
                if (_singleCharReadBuffer[0] == receivedByte) break;
            }

            // Return data
            return data.ToArray();
        }

#endregion
    }
}