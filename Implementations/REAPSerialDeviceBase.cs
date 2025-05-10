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
        public ValueTask<uint?> SetRegisterAsync(uint register, uint value) =>
            REAP<CachedSerialPortInterface>.SetRegisterAsync(HardwareAccess, register, value);
        
        public ValueTask<uint?> GetRegisterAsync(uint register) =>
            REAP<CachedSerialPortInterface>.GetRegisterAsync(HardwareAccess, register);
    }
}