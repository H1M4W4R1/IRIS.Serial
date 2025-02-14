using System.Threading.Tasks;
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
        SerialInterfaceSettings settings) : SerialDeviceBase(deviceAddress, settings)
    {
        /// <summary>
        /// Exchange message with device
        /// </summary>
        public async ValueTask<string> ExchangeMessages(string message) =>
            await LINE<CachedSerialPortInterface>.ExchangeMessages(HardwareAccess, message);

        /// <summary>
        /// Send message to device
        /// </summary>
        public async ValueTask SendMessage(string message) =>
            await LINE<CachedSerialPortInterface>.SendMessage(HardwareAccess, message);

        /// <summary>
        /// Read message from device
        /// </summary>
        public async ValueTask<string> ReadMessage() => await LINE<CachedSerialPortInterface>.ReadMessage(HardwareAccess);
    }
}