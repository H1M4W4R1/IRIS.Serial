using System.IO.Ports;
using IRIS.Communication;
using IRIS.Communication.Types;
using IRIS.Exceptions;
using IRIS.Serial.Addressing;
using IRIS.Utility;

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

        public SerialPortInterface(
            string portName,
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
            if (IsOpen) return true;

            // Open the port
            Open();

            if (!IsOpen) return false;

            // Invoke connected event
            DeviceConnected?.Invoke(new SerialPortDeviceAddress(PortName));
            return true;
        }

        public async ValueTask<bool> Disconnect()
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
        ValueTask<bool> IRawDataCommunicationInterface.TransmitRawData(byte[] data)
        {
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return new DeviceNotConnectedException().ToValueTask<bool>();
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
        async ValueTask<byte[]> IRawDataCommunicationInterface.ReadRawData(
            int length,
            CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                throw new DeviceNotConnectedException();
            }

            // Create buffer for data
            byte[] data = new byte[length];
            int bytesRead = 0;
            
            

            // Read data until all data is read
            while (bytesRead < length)
            {
                // Read data to buffer
                Memory<byte> memory = data.AsMemory(bytesRead, length - bytesRead);
                int count = await BaseStream.ReadAsync(memory, cancellationToken);
                bytesRead += count;
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Return data
            return data;
        }

        /// <summary>
        /// Reads data until specified byte is found
        /// </summary>
        /// <param name="receivedByte">Byte to find</param>
        /// <param name="cancellationToken">Used to cancel read operation</param>
        async ValueTask<byte[]> IRawDataCommunicationInterface.ReadRawDataUntil(
            byte receivedByte,
            CancellationToken cancellationToken)
        {
            // Check if device is open
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                throw new DeviceNotConnectedException(); 
            }

            // Read data until byte is found
            List<byte> data = new List<byte>();
            Memory<byte> memory = _singleCharReadBuffer.AsMemory(0, 1);

            // Read data until byte is found
            while (true)
            {
                int bytesRead = await BaseStream.ReadAsync(memory, cancellationToken);

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
            return data.ToArray();
        }

#endregion
    }
}