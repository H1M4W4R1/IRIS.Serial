using System.Threading.Tasks;
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
        public async Task<uint> SetRegister(uint register, uint value) =>
            await REAP<CachedSerialPortInterface>.SetRegister(HardwareAccess, register, value);
        
        public async Task<uint> GetRegister(uint register) =>
            await REAP<CachedSerialPortInterface>.GetRegister(HardwareAccess, register);
        
    }
}