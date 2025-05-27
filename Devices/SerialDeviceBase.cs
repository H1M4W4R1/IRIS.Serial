using System.Text;
using IRIS.Communication.Types;
using IRIS.Devices;
using IRIS.Operations;
using IRIS.Operations.Abstract;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;

namespace IRIS.Serial.Devices
{
    /// <summary>
    ///     Base class representing a device connected via serial port (COM port).
    ///     Provides common functionality for serial port communication and device management.
    ///     <para>Examples of serial port addresses:</para>
    ///     <list type="bullet">
    ///         <item>Windows: COM1, COM9</item>
    ///         <item>Linux: /dev/ttyUSB0, /dev/ttyACM0</item>
    ///     </list>
    /// </summary>
    public abstract class SerialDeviceBase : DeviceBase<CachedSerialPortInterface, SerialPortDeviceAddress>
    {
        /// <summary>
        ///     Initializes a new instance of the serial device with specified address and communication settings
        /// </summary>
        /// <param name="deviceAddress">The address of the serial port device</param>
        /// <param name="settings">
        ///     Configuration settings for the serial port communication including:
        ///     <list type="bullet">
        ///         <item>Baud rate</item>
        ///         <item>Parity</item>
        ///         <item>Data bits</item>
        ///         <item>Stop bits</item>
        ///         <item>DTR enable</item>
        ///         <item>RTS enable</item>
        ///     </list>
        /// </param>
        protected SerialDeviceBase(SerialPortDeviceAddress deviceAddress, SerialInterfaceSettings settings)
        {
            HardwareAccess =
                new CachedSerialPortInterface(deviceAddress.Address, settings.baudRate, settings.parity,
                    settings.dataBits, settings.stopBits,
                    settings.dtrEnable, settings.rtsEnable);
        }

        /// <summary>
        ///     Changes the device's serial port address while maintaining connection state
        /// </summary>
        /// <param name="deviceAddress">New serial port address to use</param>
        /// <param name="cancellationToken">
        ///     Optional cancellation token that can be used to abort the connection attempt if the
        ///     port was previously open
        /// </param>
        /// <remarks>
        ///     This method handles the port transition gracefully by:
        ///     <list type="number">
        ///         <item>Checking current connection state</item>
        ///         <item>Disconnecting if currently connected</item>
        ///         <item>Updating the port address</item>
        ///         <item>Reconnecting if previously connected</item>
        ///     </list>
        /// </remarks>
        public async ValueTask SetAddress(
            SerialPortDeviceAddress deviceAddress,
            CancellationToken cancellationToken = default)
        {
            // Check if port is open
            bool wasPortOpen = HardwareAccess.IsOpen;
            if (wasPortOpen) await HardwareAccess.Disconnect();

            HardwareAccess.PortName = deviceAddress.ToString();

            // If port was open then connect again
            if (wasPortOpen) await HardwareAccess.Connect(cancellationToken);
        }

        /// <summary>
        ///     Sends data over the serial port
        /// </summary>
        /// <param name="data">Data to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if data was sent successfully</returns>
        public async ValueTask<bool> WriteBytes(byte[] data, CancellationToken cancellationToken = default)
            => DeviceOperation.IsSuccess(
                await ((IRawDataCommunicationInterface) HardwareAccess).TransmitRawData(data, cancellationToken));

        /// <summary>
        ///     Reads specified amount of data from the serial port
        /// </summary>
        /// <param name="count">Amount of data to read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Data read from the serial port</returns>
        /// <exception cref="IOException">Thrown if data could not be read from the serial port</exception>
        public async ValueTask<byte[]> ReadBytes(int count, CancellationToken cancellationToken = default)
        {
            IDeviceOperationResult result =
                await ((IRawDataCommunicationInterface) HardwareAccess).ReadRawData(count, cancellationToken);
            
            // Check if result is success
            if (DeviceOperation.IsFailure(result)) throw new IOException("Failed to read data from device");
            
            // Check if result is of proper type
            if (result is not IDeviceOperationResult<byte[]> operationWithData)
                throw new IOException("Failed to read data from device");
            
            return operationWithData.Data;
        }
        
        /// <summary>
        ///     Reads data from the serial port until the specified byte is encountered
        /// </summary>
        /// <param name="receivedByte">Byte to wait for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Data read from the serial port</returns>
        /// <exception cref="IOException">Thrown if data could not be read from the serial port</exception>
        public async ValueTask<byte[]> ReadBytesUntil(byte receivedByte, CancellationToken cancellationToken = default)
        {
            IDeviceOperationResult result =
                await ((IRawDataCommunicationInterface) HardwareAccess).ReadRawDataUntil(receivedByte, cancellationToken);
            
            // Check if result is success
            if (DeviceOperation.IsFailure(result)) throw new IOException("Failed to read data from device");
            
            // Check if result is of proper type
            if (result is not IDeviceOperationResult<byte[]> operationWithData)
                throw new IOException("Failed to read data from device");
            
            return operationWithData.Data;
        }

        /// <summary>
        ///     Writes a string to the serial port
        /// </summary>
        /// <param name="data">String to write</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="encoding">Encoding to use, ASCII if null</param>
        /// <returns>True if data was sent successfully</returns>
        /// <exception cref="IOException">Thrown if data could not be sent to the serial port</exception>
        /// <remarks>
        ///     Uses <see cref="WriteBytes"/> implementation.
        /// </remarks>
        public async ValueTask<bool> WriteString(
            string data,
            CancellationToken cancellationToken = default,
            Encoding? encoding = null)
        {
            encoding ??= Encoding.ASCII;
            return await WriteBytes(encoding.GetBytes(data), cancellationToken);
        }
        
        /// <summary>
        ///     Reads a string from the serial port, string is terminated by 0xA (Line Feed)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="encoding">Encoding to use, ASCII if null</param>
        /// <returns>String read from the serial port</returns>
        /// <exception cref="IOException">Thrown if data could not be read from the serial port</exception>
        /// <remarks>
        ///     Uses <see cref="ReadBytesUntil"/> implementation.
        /// </remarks>
        public async ValueTask<string> ReadString(
            CancellationToken cancellationToken = default,
            Encoding? encoding = null)
        {
            encoding ??= Encoding.ASCII;
            byte[] data = await ReadBytesUntil((byte) 0xA, cancellationToken);
            return encoding.GetString(data);
        }
    }
}