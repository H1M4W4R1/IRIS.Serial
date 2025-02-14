# IRIS<sup>2</sup>: Intermediate Resource Integration System - Serial Package
[IRIS<sup>2</sup>](https://github.com/H1M4W4R1/IRIS) package for interfacing with serial devices.

## Devices
### SerialDeviceBase
SerialDevice is used to communicate with serial devices on
COM ports. It uses `CachedSerialPortInterface` to communicate
with hardware - data received from port is automatically
cached to prevent issues with lost data which may occur
when reading from port directly.

It takes two parameters in constructor:
* `SerialPortDeviceAddress` - address of device
* `SerialInterfaceSettings` - settings for serial port
  (baud rate, parity, etc.)

Example:
```cs
public sealed class MySerialDevice(
        SerialPortDeviceAddress deviceAddress,
        SerialInterfaceSettings settings) :
        SerialDeviceBase(deviceAddress, settings)
{
    // Code     
}
```

## Interfaces
### SerialPortInterface
Simple SerialPort interface that uses `System.IO.Ports.
SerialPort`. Definitely not recommended to be used in
production as it doesn't provide any caching mechanism
and may cause issues with lost data.

### CachedSerialPortInterface
Better version of `SerialPortInterface` that caches data
received from port. Used to communicate with devices on
COM ports.
