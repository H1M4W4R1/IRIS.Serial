using IRIS.Protocols.IRIS;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;
using IRIS.Serial.Devices;

namespace IRIS.Serial.Implementations
{
    /// <summary>
    /// Base class for LINE devices
    /// Uses simple UART communication, mostly logging purposes.
    /// </summary>
    public abstract class LINESerialDeviceBase(
        SerialPortDeviceAddress deviceAddress,
        SerialInterfaceSettings settings
    ) : SerialDeviceBase(deviceAddress, settings)
    {
        /// <summary>
        ///     Exchange message with device
        /// </summary>
        public string ExchangeMessages(string message) =>
            LINE<CachedSerialPortInterface>.ExchangeMessages(HardwareAccess, message);
        
        /// <summary>
        ///     Exchange message with device
        /// </summary>
        public ValueTask<string> ExchangeMessagesAsync(string message) =>
            LINE<CachedSerialPortInterface>.ExchangeMessagesAsync(HardwareAccess, message);

        /// <summary>
        ///    Send message to device
        /// </summary>
        public bool SendMessage(string message) =>
            LINE<CachedSerialPortInterface>.SendMessage(HardwareAccess, message);
        
        /// <summary>
        ///     Send message to device
        /// </summary>
        public ValueTask<bool> SendMessageAsync(string message) =>
            LINE<CachedSerialPortInterface>.SendMessageAsync(HardwareAccess, message);

        /// <summary>
        ///    Read message from device
        /// </summary>
        public string ReadMessage()
            => LINE<CachedSerialPortInterface>.ReadMessage(HardwareAccess);
        
        /// <summary>
        ///     Read message from device
        /// </summary>
        public ValueTask<string> ReadMessageAsync()
            => LINE<CachedSerialPortInterface>.ReadMessageAsync(HardwareAccess);
    }
}