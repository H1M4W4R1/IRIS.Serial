﻿using System;
using System.Threading.Tasks;
using IRIS.Protocols.IRIS;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;
using IRIS.Serial.Devices;

namespace IRIS.Serial.Implementations
{
    /// <summary>
    /// Base class for RUSTIC devices
    /// </summary>
    public abstract class RUSTICSerialDeviceBase(
        SerialPortDeviceAddress deviceAddress,
        SerialInterfaceSettings settings) : SerialDeviceBase(deviceAddress, settings)
    {
        /// <summary>
        /// Sends SET message to device and returns the response <br/>
        /// E.g. PROPERTY to desired value
        /// </summary>
        /// <remarks>
        /// Uses ToString() method to convert <see cref="value"/> to string
        /// </remarks>
        protected async Task<bool> SetProperty<TValueType>(string message, TValueType value)
        {
            try
            {
                return await RUSTIC<CachedSerialPortInterface>.SetProperty(message, value?.ToString(), HardwareAccess, 100);
            }
            catch(TimeoutException)
            {
                // If the device does not respond in time, return false
                // value may be set, but we cannot confirm it, so assume it is not
                return false;
            }
        }

        /// <summary>
        /// Sends GET message to device and returns the response <br/>
        /// </summary>
        protected async Task<string> GetProperty(string propertyName)
        {
            (string _, string value) =
                await RUSTIC<CachedSerialPortInterface>.GetProperty(propertyName, HardwareAccess);

            return value;
        }
    }
}