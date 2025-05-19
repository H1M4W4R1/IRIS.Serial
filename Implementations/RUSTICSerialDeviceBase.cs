using IRIS.Protocols.IRIS;
using IRIS.Protocols.IRIS.Data;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;
using IRIS.Serial.Devices;

namespace IRIS.Serial.Implementations
{
    /// <summary>
    /// Abstract base class providing common functionality for RUSTIC protocol serial devices.
    /// Inherits from <see cref="SerialDeviceBase"/> and implements RUSTIC-specific property operations.
    /// </summary>
    /// <remarks>
    /// This class serves as the foundation for all RUSTIC protocol device implementations,
    /// providing standardized methods for property access via the RUSTIC protocol.
    /// </remarks>
    public abstract class RUSTICSerialDeviceBase(
        SerialPortDeviceAddress deviceAddress,
        SerialInterfaceSettings settings
    ) : SerialDeviceBase(deviceAddress, settings)
    {
        /// <summary>
        /// Asynchronously sets a property value on the RUSTIC device and returns the response.
        /// </summary>
        /// <typeparam name="TValueType">The type of the value being set. Must be non-nullable.</typeparam>
        /// <param name="message">The property name or message to send to the device.</param>
        /// <param name="value">The value to set the property to.</param>
        /// <returns>
        /// A <see cref="ValueTask{RUSTICDeviceProperty}"/> representing the asynchronous operation,
        /// containing the device's response property or null if unsuccessful.
        /// </returns>
        /// <remarks>
        /// The value is converted to a string representation before being sent to the device.
        /// Uses a default timeout of 100ms for the operation.
        /// </remarks>
        protected ValueTask<RUSTICDeviceProperty?> SetProperty<TValueType>(string message, TValueType value)
            where TValueType : notnull
            => RUSTIC<CachedSerialPortInterface>.SetProperty(message, value.ToString() ?? string.Empty,
                HardwareAccess, 100);

        /// <summary>
        /// Asynchronously retrieves a property value from the RUSTIC device.
        /// </summary>
        /// <param name="propertyName">The name of the property to retrieve.</param>
        /// <returns>
        /// A <see cref="ValueTask{RUSTICDeviceProperty}"/> representing the asynchronous operation,
        /// containing the requested property or null if unsuccessful.
        /// </returns>
        /// <remarks>
        /// This method uses the device's current hardware access interface to communicate
        /// with the physical device.
        /// </remarks>
        protected ValueTask<RUSTICDeviceProperty?> GetProperty(string propertyName)
            => RUSTIC<CachedSerialPortInterface>.GetProperty(propertyName, HardwareAccess);
    }
}