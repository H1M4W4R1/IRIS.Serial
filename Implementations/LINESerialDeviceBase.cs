using IRIS.Protocols.IRIS;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;
using IRIS.Serial.Devices;

namespace IRIS.Serial.Implementations
{
    /// <summary>
    /// Abstract base class for devices using the LINE protocol over serial communication.
    /// Provides fundamental LINE protocol operations for simple UART-based communication,
    /// primarily used for logging and basic command/response interactions.
    /// </summary>
    /// <remarks>
    /// <para>This class inherits from SerialDeviceBase and implements LINE protocol operations.</para>
    /// <para>Typical use cases include:</para>
    /// <list type="bullet">
    /// <item>Device logging and monitoring</item>
    /// <item>Simple command/response interactions</item>
    /// <item>Basic device configuration</item>
    /// </list>
    /// </remarks>
    public abstract class LINESerialDeviceBase(
        SerialPortDeviceAddress deviceAddress,
        SerialInterfaceSettings settings) : SerialDeviceBase(deviceAddress, settings)
    {
        /// <summary>
        /// Performs a complete message exchange with the device using the LINE protocol.
        /// Sends a message and waits for a response in a single operation.
        /// </summary>
        /// <param name="message">The message to send to the device</param>
        /// <returns>
        /// ValueTask containing the response string from the device, or null if no response was received.
        /// The task completes when the full exchange is finished.
        /// </returns>
        public ValueTask<string?> ExchangeMessages(string message) =>
            LINE<CachedSerialPortInterface>.ExchangeMessages(HardwareAccess, message);

        /// <summary>
        /// Sends a message to the device using the LINE protocol.
        /// </summary>
        /// <param name="message">The message to transmit</param>
        /// <returns>
        /// ValueTask containing a boolean indicating whether the message was successfully sent.
        /// The task completes when the transmission is finished.
        /// </returns>
        public ValueTask<bool> SendMessage(string message) =>
            LINE<CachedSerialPortInterface>.SendMessage(HardwareAccess, message);

        /// <summary>
        /// Reads a message from the device using the LINE protocol.
        /// </summary>
        /// <returns>
        /// ValueTask containing the received message string, or null if no message was available.
        /// The task completes when a complete message is received or timeout occurs.
        /// </returns>
        public ValueTask<string?> ReadMessage() => LINE<CachedSerialPortInterface>.ReadMessage(HardwareAccess);
    }
}