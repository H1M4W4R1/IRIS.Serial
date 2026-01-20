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
using IRIS.Utility.Awaitable;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace IRIS.Serial.Communication
{
    /// <summary>
    ///     Provides a reliable serial port implementation that buffers incoming data.
    ///     This addresses the unreliability of the standard .NET SerialPort class by implementing
    ///     a buffered data reception system. The class implements IRawDataCommunicationInterface
    ///     for raw byte-level communication with serial devices.
    /// </summary>
    /// <remarks>
    ///     The buffered approach is necessary because unbuffered event-driven solutions
    ///     can cause performance issues and data loss at high data rates.
    /// </remarks>
    public sealed class CachedSerialPortInterface : SerialPort,
        IRawDataCommunicationInterface<SerialPortDeviceAddress>
    {
        /// <summary>
        ///     Internal buffer storing all received data until it's read by the application
        /// </summary>
        private readonly List<byte> _dataReceived = new List<byte>();

        /// <summary>
        ///     Buffer used for reading data from the serial port
        ///     Matches the default 1kB buffer size used by .NET's SerialPort
        /// </summary>
        private readonly byte[] _readBuffer = new byte[1024];

        /// <summary>
        ///     Source for cancellation tokens to control the background read operation
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        ///     Reference to the current cancellation token for the read operation
        /// </summary>
        private CancellationToken _tokenRef = CancellationToken.None;

        /// <summary>
        ///     Event raised when a device successfully connects
        /// </summary>
        public event Delegates.DeviceConnectedHandler<SerialPortDeviceAddress>? DeviceConnected;

        /// <summary>
        ///     Event raised when a device is intentionally disconnected
        /// </summary>
        public event Delegates.DeviceDisconnectedHandler<SerialPortDeviceAddress>? DeviceDisconnected;

        /// <summary>
        ///     Event raised when connection to a device is unexpectedly lost
        /// </summary>
        public event Delegates.DeviceConnectionLostHandler<SerialPortDeviceAddress>? DeviceConnectionLost;

        /// <summary>
        ///     Event raised when data is received from the serial port
        /// </summary>
        public event Delegates.DataReceivedHandler? SerialDataReceived;
        
        /// <summary>
        ///     Initializes a new instance of the CachedSerialPortInterface with specified parameters
        /// </summary>
        /// <param name="portName">The name of the serial port (e.g., "COM1")</param>
        /// <param name="baudRate">The baud rate for communication</param>
        /// <param name="parity">The parity checking protocol</param>
        /// <param name="dataBits">The number of data bits per byte</param>
        /// <param name="stopBits">The number of stop bits</param>
        /// <param name="dtrEnable">Whether to enable Data Terminal Ready (DTR) signal</param>
        /// <param name="rtsEnable">Whether to enable Request to Send (RTS) signal</param>
        public CachedSerialPortInterface(
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
            // Do not connect if already connected
            if (IsOpen)
            {
                Notify.Verbose(nameof(CachedSerialPortInterface), $"Device {PortName} is already connected.");
                return DeviceOperation.VResult<DeviceAlreadyConnectedResult>();
            }
            _tokenRef = _cancellationTokenSource.Token;

            // Open the port
            Open();
            if (!IsOpen)
            {
                Notify.Error(nameof(CachedSerialPortInterface), $"Cannot connect to device {PortName}");
                return DeviceOperation.VResult<DeviceConnectionFailedResult>();
            }

            // Begin continuous read
            BeginContinuousRead(_tokenRef);

            DeviceConnected?.Invoke(new SerialPortDeviceAddress(PortName));
            Notify.Success(nameof(CachedSerialPortInterface), $"Successfully connected to device {PortName}");

            return DeviceOperation.VResult<DeviceConnectedSuccessfullyResult>();
        }

        /// <summary>
        ///     Disconnects from the serial device
        /// </summary>
        public ValueTask<IDeviceOperationResult> Disconnect()
        {
            // Check if port is open
            if (!IsOpen)
            {
                Notify.Verbose(nameof(CachedSerialPortInterface), $"Device {PortName} is already disconnected.");
                return DeviceOperation.VResult<DeviceAlreadyDisconnectedResult>();
            }

            // Cancel reading
            _cancellationTokenSource.Cancel();

            // Close port
            Close();

            // Invoke event
            DeviceDisconnected?.Invoke(new SerialPortDeviceAddress(PortName));
            Notify.Success(nameof(CachedSerialPortInterface), $"Successfully disconnected from device {PortName}");
            
            return DeviceOperation.VResult<DeviceDisconnectedSuccessfullyResult>();
        }

        /// <summary>
        ///     Starts a continuous asynchronous read operation from the serial port
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        private async void BeginContinuousRead(CancellationToken cancellationToken)
        {
            // Check if cancellation is requested, if so, return
            if (cancellationToken.IsCancellationRequested) return;

            // Check if port is open
            if (!IsOpen)
            {
                Notify.Verbose(nameof(CachedSerialPortInterface), $"Device {PortName} is not connected.");
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return;
            }

            // Read data from port
            BaseStream.BeginRead(_readBuffer, 0, _readBuffer.Length, delegate(IAsyncResult ar)
            {
                try
                {
                    int count = BaseStream.EndRead(ar);
                    byte[] dst = new byte[count];
                    Buffer.BlockCopy(_readBuffer, 0, dst, 0, count);
                    OnDataReceived(dst);
                }
                catch
                {
                    // Do nothing
                }

                // Begin next read
                BeginContinuousRead(cancellationToken);
            }, null);
        }

        /// <summary>
        ///     Handles received data by adding it to the internal buffer
        /// </summary>
        /// <param name="data">The received data to process</param>
        private void OnDataReceived(IReadOnlyList<byte> data)
        {
            // Copy data to storage
            for (int index = 0; index < data.Count; index++)
            {
                byte dataByte = data[index];
                lock (_dataReceived) _dataReceived.Add(dataByte);
            }

            SerialDataReceived?.Invoke(data.Count);
        }

#region IRawDataCommunicationInterface

        /// <summary>
        ///     Sends raw data to the connected device
        /// </summary>
        /// <param name="data">The data to transmit</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        ValueTask<IDeviceOperationResult> IRawDataCommunicationInterface.TransmitRawData(byte[] data, CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                Notify.Verbose(nameof(CachedSerialPortInterface), $"Device {PortName} is not connected.");
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return DeviceOperation.VResult<DeviceNotConnectedResult>();
            }

            // Write data to device
            Write(data, 0, data.Length);

            // Return success
            return DeviceOperation.VResult<DeviceWriteSuccessfulResult>();
        }

        /// <summary>
        ///     Reads a specified amount of raw data from the device
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
                Notify.Verbose(nameof(CachedSerialPortInterface), $"Device {PortName} is not connected.");
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return DeviceOperation.Result<DeviceNotConnectedResult>();
            }

            // Wait until data is available
            // ReSharper disable once InconsistentlySynchronizedField
            // Synchronization is not required for this awaiter
            int totalLength = await new WaitUntilCollectionExceeds<byte>(_dataReceived, length, cancellationToken);


            // Check if cancellation is requested
            if (cancellationToken.IsCancellationRequested)
            {
                Notify.Error(nameof(CachedSerialPortInterface), $"Device {PortName} reading timed out.");
                return DeviceOperation.Result<DeviceTimeoutResult>();
            }

            // Check if data is available
            if (totalLength < length)
            {
                Notify.Error(nameof(CachedSerialPortInterface), $"Read data length is less than expected on device {PortName}.");
                return DeviceOperation.Result<DeviceReadFailedResult>();
            }

            // Create buffer for data
            byte[] data = new byte[length];

            // Copy data to buffer
            lock (_dataReceived)
            {
                _dataReceived.CopyTo(0, data, 0, length);
                _dataReceived.RemoveRange(0, length);
            }
            
            // Return data
            return new DeviceReadSuccessful<byte[]>(data);
        }

        /// <summary>
        ///     Reads data until a specific byte is encountered
        /// </summary>
        /// <param name="receivedByte">The byte to search for</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        [OperationReadType(typeof(byte[]))]
        async ValueTask<IDeviceOperationResult> IRawDataCommunicationInterface.ReadRawDataUntil(
            byte receivedByte,
            CancellationToken cancellationToken)
        {
            // Check if device is open
            if (!IsOpen)
            {
                Notify.Verbose(nameof(CachedSerialPortInterface), $"Device {PortName} is not connected.");
                DeviceConnectionLost?.Invoke(new SerialPortDeviceAddress(PortName));
                return DeviceOperation.Result<DeviceNotConnectedResult>();
            }

            // Wait until received data contains expected byte
            // ReSharper disable once InconsistentlySynchronizedField
            // This should not happen as List is Collection and thus uses safe access to the collection
            await new WaitUntilCollectionContains<byte>(_dataReceived, receivedByte, cancellationToken);

            // Check if cancellation is requested
            if (cancellationToken.IsCancellationRequested)
            {
                Notify.Error(nameof(CachedSerialPortInterface), $"Device {PortName} reading timed out.");
                return DeviceOperation.Result<DeviceTimeoutResult>();
            }
            
            // Declare buffer
            byte[] buffer;

            // Copy data to buffer
            lock (_dataReceived)
            {
                int dataIndex = _dataReceived.IndexOf(receivedByte);
                buffer = _dataReceived.GetRange(0, dataIndex + 1).ToArray();

                // Remove data from original buffer
                _dataReceived.RemoveRange(0, dataIndex + 1);
            }

            // Return data
            return new DeviceReadSuccessful<byte[]>(buffer);
        }

#endregion
    }
}