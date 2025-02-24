﻿using IRIS.Data;
using IRIS.Protocols.IRIS;
using IRIS.Serial.Addressing;
using IRIS.Serial.Communication;
using IRIS.Serial.Communication.Settings;
using IRIS.Serial.Devices;

namespace IRIS.Serial.Implementations
{
    public abstract class REAPSerialDeviceBase(SerialPortDeviceAddress deviceAddress,
        SerialInterfaceSettings settings) : SerialDeviceBase(deviceAddress, settings)
    {
        public DataPromise<uint> SetRegister(uint register, uint value) =>
            REAP<CachedSerialPortInterface>.SetRegister(HardwareAccess, register, value);
        
        public DataPromise<uint> GetRegister(uint register) =>
            REAP<CachedSerialPortInterface>.GetRegister(HardwareAccess, register);
        
    }
}