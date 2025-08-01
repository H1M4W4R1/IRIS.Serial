using System.IO.Ports;
using IRIS.Communication;
using IRIS.Communication.Types;
using IRIS.Operations;
using IRIS.Operations.Abstract;
using IRIS.Operations.Attributes;
using IRIS.Operations.Connection;
using IRIS.Operations.Data;
using IRIS.Operations.Generic;
using IRIS.Serial.Addressing;
using IRIS.Utility;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace IRIS.Serial.Communication
{
    /// <summary>
    ///     Provides a serial port interface implementation for device communication.
    ///     This sealed class inherits from System.IO.Ports.SerialPort and implements
    ///     the IRawDataCommunicationInterface for SerialPortDeviceAddress.
    /// </summary>
    public sealed class SerialPortInterface : SerialPort, IRawDataCommunicationInterface<SerialPortDeviceAddress>
    {
        /// <summary>
        ///     Single-byte buffer used for character-by-character reading to optimize memory usage
        /// </summary>
        private readonly byte[] _singleCharReadBuffer = new byte[1];

        /// <summary>
        ///     Event triggered when a device successfully connects
        /// </summary>
        public event Delegates.DeviceConnectedHandler<SerialPortDeviceAddress>? DeviceConnected;

        /// <summary>
        ///     Event triggered when a device is properly disconnected
        /// </summary>
        public event Delegates.DeviceDisconnectedHandler<SerialPortDeviceAddress>? DeviceDisconnected;

        /// <summary>
        ///     Event triggered when connection to a device is unexpectedly lost
        /// </summary>
        public event Delegates.DeviceConnectionLostHandler<SerialPortDeviceAddress>? DeviceConnectionLost;

        /// <summary>
        ///     Initializes a new instance of the SerialPortInterface with specified parameters
        /// </summary>
        /// <param name="portName">The name of the serial port (e.g. "COM1")</param>
        /// <param name="baudRate">The baud rate for communication</param>
        /// <param name="parity">The parity checking protocol</param>
        /// <param name="dataBits">The number of data bits per byte</param>
        /// <param name="stopBits">The number of stop bits</param>
        /// <param name="dtrEnable">Whether to enable Data Terminal Ready (DTR) signal</param>
        /// <param name="rtsEnable">Whether to enable Request to Send (RTS) signal</param>
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
        ///     Establishes connection with the serial device
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        public ValueTask<IDeviceOperationResult> Connect(CancellationToken cancellationToken)
        {
            // If port is already open, return
            if (IsOpen)
            {
                Notify.Verbose(nameof(SerialPortInterface), $"Device is already connected {PortName}");
                return DeviceOperation.VResult<DeviceAlreadyConnectedResult>();
            }

            // Open the port
            Open();

            if (!IsOpen)
            {
                Notify.Error(nameof(SerialPortInterface), $"Cannot connect to device {PortName}");
                return DeviceOperation.VResult<DeviceConnectionFailedResult>();
            }

            // Invoke connected event
            DeviceConnected?.Invoke(new SerialPortDeviceAddress(PortName));
            Notify.Success(nameof(SerialPortInterface), $"Successfully connected to device {PortName}");
            return DeviceOperation.VResult<DeviceConnectedSuccessfullyResult>();
        }

        /// <summary>
        ///     Terminates connection with the serial device
        /// </summary>
        public ValueTask<IDeviceOperationResult> Disconnect()
        {
            // If port is not open, return
            if (!IsOpen)
            {
                Notify.Verbose(nameof(SerialPortInterface), $"Device {PortName} is not connected");
                return DeviceOperation.VResult<DeviceConnectionFailedResult>();
            }
            Close();

            // Invoke disconnected event
            DeviceDisconnected?.Invoke(new SerialPortDeviceAddress(PortName));
            Notify.Success(nameof(SerialPortInterface), $"Disconnected from device {PortName}");
            return DeviceOperation.VResult<DeviceDisconnectedSuccessfullyResult>();
        }

#region IRawDataCommunicationInterface

        /// <summary>
        ///     Sends raw data to the connected device
        /// </summary>
        /// <param name="data">Byte array containing data to transmit</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        ValueTask<IDeviceOperationResult> IRawDataCommunicationInterface.TransmitRawData(byte[] data, CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                Notify.Error(nameof(SerialPortInterface), $"Device {PortName} is not connected");
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return DeviceOperation.VResult<DeviceNotConnectedResult>();
            }

            // Write data to device
            Write(data, 0, data.Length);

            return DeviceOperation.VResult<DeviceWriteSuccessfulResult>();
        }

        /// <summary>
        ///     Reads a specified amount of raw data from the connected device
        /// </summary>
        /// <param name="length">Number of bytes to read</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        [OperationReadType(typeof(byte[]))]
        async ValueTask<IDeviceOperationResult> IRawDataCommunicationInterface.ReadRawData(
            int length,
            CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                Notify.Error(nameof(SerialPortInterface), $"Device {PortName} is not connected");
                return DeviceOperation.Result<DeviceNotConnectedResult>();
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
                    int readBytesCount = await
                        BaseStream.ReadAsync(data, bytesRead, length - bytesRead, cancellationToken);

                    bytesRead += readBytesCount;
                }
                catch (TaskCanceledException)
                {
                    Notify.Error(nameof(SerialPortInterface), $"Data reading on device {PortName} was cancelled.");
                    return DeviceOperation.Result<DeviceTimeoutResult>();
                }
            }

            // Return data
            return new DeviceReadSuccessful<byte[]>(data);
        }

        /// <summary>
        ///     Reads data from the device until a specified byte is encountered
        /// </summary>
        /// <param name="receivedByte">The termination byte to search for</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        [OperationReadType(typeof(byte[]))]
        async ValueTask<IDeviceOperationResult> IRawDataCommunicationInterface.ReadRawDataUntil(
            byte receivedByte,
            CancellationToken cancellationToken)
        {
            // Check if device is open
            if (!IsOpen)
            {
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                Notify.Error(nameof(SerialPortInterface), $"Device {PortName} is not connected");
                return DeviceOperation.Result<DeviceNotConnectedResult>();
            }

            // Read data until byte is found
            List<byte> data = new List<byte>();

            // Read data until byte is found
            while (true)
            {
                try
                {
                    int readBytesCount =
                        await BaseStream.ReadAsync(_singleCharReadBuffer, 0, 1, cancellationToken);

                    // Check if cancellation is requested
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Notify.Error(nameof(SerialPortInterface), $"Data reading on device {PortName} was cancelled.");
                        return DeviceOperation.Result<DeviceTimeoutResult>();
                    }

                    // Check if data is read
                    if (readBytesCount == 0) continue;

                    // If data is read, add it to list
                    data.Add(_singleCharReadBuffer[0]);

                    // Check if byte is found
                    if (_singleCharReadBuffer[0] == receivedByte) break;
                }
                catch (TaskCanceledException)
                {
                    Notify.Error(nameof(SerialPortInterface), $"Data reading on device {PortName} was cancelled.");
                    return DeviceOperation.Result<DeviceTimeoutResult>();
                }
            }

            // Return data
            return new DeviceReadSuccessful<byte[]>(data.ToArray());
        }

#endregion
    }
}