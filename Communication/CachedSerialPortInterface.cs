﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Communication;
using IRIS.Communication.Types;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace IRIS.Serial.Communication
{
    /// <summary>
    /// Reliable serial port, as regular one is really unreliable in data receiving.
    /// Buffers data as unbuffered event-driven solution would bend space-time continuum (quite literally).
    /// </summary>
    public sealed class CachedSerialPortInterface : SerialPort, IRawDataCommunicationInterface
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
        
        public event Delegates.DeviceConnectionLost? OnDeviceConnectionLost;

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
        /// <exception cref="CommunicationException">If port cannot be opened</exception>
        public async ValueTask<bool> Connect(CancellationToken cancellationToken)
        {
            // Do not connect if already connected
            if (IsOpen) return true;
            _tokenRef = _cancellationTokenSource.Token;
                
            // Open the port
            Open();
            if (!IsOpen) return false;
                
            // Begin continuous read
            BeginContinuousRead(_tokenRef);
            
            return true;
        }

        public async ValueTask<bool> Disconnect(CancellationToken cancellationToken)
        {
            // Check if port is open
            if (!IsOpen) return true;
            
            // Cancel reading
            await _cancellationTokenSource.CancelAsync();
            
            // Close port
            Close();
            
            return true;
        }
        
        private async void BeginContinuousRead(CancellationToken cancellationToken)
        {
            // Check if cancellation is requested, if so, return
            if (cancellationToken.IsCancellationRequested) return;
            
            // Check if port is open
            if (!IsOpen)
            {
                OnDeviceConnectionLost?.Invoke();
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
        ValueTask IRawDataCommunicationInterface.TransmitRawData(byte[] data)
        {
            if (!IsOpen)
            {
                OnDeviceConnectionLost?.Invoke();
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
        /// <exception cref="CommunicationException">If port is not open</exception>
        async ValueTask<byte[]> IRawDataCommunicationInterface.ReadRawData(int length, CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                OnDeviceConnectionLost?.Invoke();
                return [];
            }

            // Create buffer for data
            // TODO: Get rid of this allocation
            byte[] data = new byte[length];
            int bytesRead = 0;

            while (bytesRead < length)
            {
                // Check if cancellation is requested
                if (cancellationToken.IsCancellationRequested) return [];
                
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
            return data;
        }

        /// <summary>
        /// Reads data until specified byte is found
        /// </summary>
        /// <param name="receivedByte">Byte to find</param>
        /// <param name="cancellationToken">Used to cancel read operation</param>
        /// <returns>Array of data, if byte is not found, empty array is returned</returns>
        /// <exception cref="CommunicationException">If port is not open</exception>
        async ValueTask<byte[]> IRawDataCommunicationInterface.ReadRawDataUntil(byte receivedByte,
            CancellationToken cancellationToken)
        {
            // Check if device is open
            if (!IsOpen)
            {
                OnDeviceConnectionLost?.Invoke();
                return [];
            }

            // Read data until byte is found
            // TODO: Get rid of this allocation
            List<byte> data = new List<byte>();

            // Read data until byte is found
            while (true)
            {
                // Check if cancellation is requested
                if (cancellationToken.IsCancellationRequested) return [];

                // Local byte variable
                byte currentByte = 0;
                
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
            return data.ToArray();
        }

#endregion
    }
}