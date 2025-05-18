﻿using System.IO.Ports;
using IRIS.Communication;
using IRIS.Communication.Types;
using IRIS.Serial.Addressing;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace IRIS.Serial.Communication
{
    /// <summary>
    /// Reliable serial port, as regular one is really unreliable in data receiving.
    /// Buffers data as unbuffered event-driven solution would bend space-time continuum (quite literally).
    /// </summary>
    public sealed class CachedSerialPortInterface : SerialPort, IRawDataCommunicationInterface<SerialPortDeviceAddress>
    {
        /// <summary>
        /// Storage of all data received
        /// </summary>
        private readonly List<byte> _dataReceived = new List<byte>();
        
        /// <summary>
        /// C# serial port uses 1kB buffer by default, so we will use the same size
        /// </summary>
        private readonly byte[] _readBuffer = new byte[1024];
        
        /// <summary>
        /// Local cancellation token source
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// "Reference" to cancellation token
        /// </summary>
        private CancellationToken _tokenRef = CancellationToken.None;
        
        public event Delegates.DeviceConnectedHandler<SerialPortDeviceAddress>? DeviceConnected;
        public event Delegates.DeviceDisconnectedHandler<SerialPortDeviceAddress>? DeviceDisconnected;
        public event Delegates.DeviceConnectionLostHandler<SerialPortDeviceAddress>? DeviceConnectionLost;

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
        /// Connect to device - open port and start reading data
        /// </summary>
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
        /// Callback invoked when data is received
        /// </summary>
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
        /// <returns></returns>
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
        /// Reads data until specified byte is found
        /// </summary>
        /// <param name="receivedByte">Byte to find</param>
        /// <param name="cancellationToken">Used to cancel read operation</param>
        /// <returns>Array of data, if byte is not found, empty array is returned</returns>
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