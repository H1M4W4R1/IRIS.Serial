using System.IO.Ports;
using IRIS.Communication;
using IRIS.Communication.Types;
using IRIS.Serial.Addressing;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace IRIS.Serial.Communication
{
    /// <summary>
    /// Provides a reliable serial port implementation that buffers incoming data.
    /// This addresses the unreliability of the standard .NET SerialPort class by implementing
    /// a buffered data reception system. The class implements IRawDataCommunicationInterface
    /// for raw byte-level communication with serial devices.
    /// </summary>
    /// <remarks>
    /// The buffered approach is necessary because unbuffered event-driven solutions
    /// can cause performance issues and data loss at high data rates.
    /// </remarks>
    public sealed class CachedSerialPortInterface : SerialPort, IRawDataCommunicationInterface<SerialPortDeviceAddress>
    {
        /// <summary>
        /// Internal buffer storing all received data until it's read by the application
        /// </summary>
        private readonly List<byte> _dataReceived = new List<byte>();
        
        /// <summary>
        /// Buffer used for reading data from the serial port
        /// Matches the default 1kB buffer size used by .NET's SerialPort
        /// </summary>
        private readonly byte[] _readBuffer = new byte[1024];
        
        /// <summary>
        /// Source for cancellation tokens to control the background read operation
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// Reference to the current cancellation token for the read operation
        /// </summary>
        private CancellationToken _tokenRef = CancellationToken.None;
        
        /// <summary>
        /// Event raised when a device successfully connects
        /// </summary>
        public event Delegates.DeviceConnectedHandler<SerialPortDeviceAddress>? DeviceConnected;
        
        /// <summary>
        /// Event raised when a device is intentionally disconnected
        /// </summary>
        public event Delegates.DeviceDisconnectedHandler<SerialPortDeviceAddress>? DeviceDisconnected;
        
        /// <summary>
        /// Event raised when connection to a device is unexpectedly lost
        /// </summary>
        public event Delegates.DeviceConnectionLostHandler<SerialPortDeviceAddress>? DeviceConnectionLost;

        /// <summary>
        /// Initializes a new instance of the CachedSerialPortInterface with specified parameters
        /// </summary>
        /// <param name="portName">The name of the serial port (e.g., "COM1")</param>
        /// <param name="baudRate">The baud rate for communication</param>
        /// <param name="parity">The parity checking protocol</param>
        /// <param name="dataBits">The number of data bits per byte</param>
        /// <param name="stopBits">The number of stop bits</param>
        /// <param name="dtrEnable">Whether to enable Data Terminal Ready (DTR) signal</param>
        /// <param name="rtsEnable">Whether to enable Request to Send (RTS) signal</param>
        public CachedSerialPortInterface(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits,
            bool dtrEnable, bool rtsEnable)
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
        /// <returns>ValueTask indicating whether connection was successful</returns>
        public ValueTask<bool> Connect(CancellationToken cancellationToken)
        {
            // Do not connect if already connected
            if (IsOpen) return ValueTask.FromResult(true);
            _tokenRef = _cancellationTokenSource.Token;
                
            // Open the port
            Open();
            if (!IsOpen) return ValueTask.FromResult(false);
                
            // Begin continuous read
            BeginContinuousRead(_tokenRef);
            
            DeviceConnected?.Invoke(new SerialPortDeviceAddress(PortName));
            
            return ValueTask.FromResult(true);
        }

        /// <summary>
        /// Disconnects from the serial device
        /// </summary>
        /// <returns>ValueTask indicating whether disconnection was successful</returns>
        public ValueTask<bool> Disconnect()
        {
            // Check if port is open
            if (!IsOpen) return ValueTask.FromResult(true);
            
            // Cancel reading
            _cancellationTokenSource.Cancel();
            
            // Close port
            Close();
            
            // Invoke event
            DeviceDisconnected?.Invoke(new SerialPortDeviceAddress(PortName));
            return ValueTask.FromResult(true);
        }
        
        /// <summary>
        /// Starts a continuous asynchronous read operation from the serial port
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        private async void BeginContinuousRead(CancellationToken cancellationToken)
        {
            // Check if cancellation is requested, if so, return
            if (cancellationToken.IsCancellationRequested) return;
            
            // Check if port is open
            if (!IsOpen)
            {
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
        /// Handles received data by adding it to the internal buffer
        /// </summary>
        /// <param name="data">The received data to process</param>
        private void OnDataReceived(IReadOnlyList<byte> data)
        {
            // Copy data to storage
            for (int index = 0; index < data.Count; index++)
            {
                byte dataByte = data[index];
                lock(_dataReceived)
                    _dataReceived.Add(dataByte);
            }
        }

#region IRawDataCommunicationInterface

        /// <summary>
        /// Sends raw data to the connected device
        /// </summary>
        /// <param name="data">The data to transmit</param>
        /// <returns>ValueTask indicating whether transmission was successful</returns>
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
        /// Reads a specified amount of raw data from the device
        /// </summary>
        /// <param name="length">Number of bytes to read</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        /// <returns>ValueTask containing the read data or empty array if cancelled/error</returns>
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

            while (bytesRead < length)
            {
                // Check if cancellation is requested
                if (cancellationToken.IsCancellationRequested) 
                    return ValueTask.FromResult(Array.Empty<byte>());
                
                // Check if data is available
                lock (_dataReceived)
                {
                    if (_dataReceived.Count == 0) continue;

                    // Read data
                    data[bytesRead] = _dataReceived[0];
                    _dataReceived.RemoveAt(0);
                }

                // Increment bytes read
                bytesRead++;
            }

            // Return data
            return ValueTask.FromResult(data);
        }

        /// <summary>
        /// Reads data until a specific byte is encountered
        /// </summary>
        /// <param name="receivedByte">The byte to search for</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
        /// <returns>ValueTask containing all data up to and including the search byte</returns>
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
                // Check if cancellation is requested
                if (cancellationToken.IsCancellationRequested)
                    return ValueTask.FromResult(Array.Empty<byte>());

                // Local byte variable
                byte currentByte;
                
                lock (_dataReceived)
                {
                    // Check if data is available
                    if (_dataReceived.Count == 0) continue;

                    // Read data
                    currentByte = _dataReceived[0];
                    _dataReceived.RemoveAt(0);
                }

                // Add data to list
                data.Add(currentByte);
                
                // Check if byte is found
                if (currentByte == receivedByte) break;
            }

            // Return data
            return ValueTask.FromResult(data.ToArray());
        }

#endregion

        
    }
}