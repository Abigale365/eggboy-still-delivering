"""EasyInput read-only BLE/USB listener and localhost relay. Python 3.11+."""
import argparse
import asyncio
import ctypes
import json
import os
import socket
import sys
import time
from pathlib import Path

SERVICE = '7d2f4d10-6f6b-4a2d-8b01-6d4653320001'
NOTIFY = '7d2f4d10-6f6b-4a2d-8b01-6d4653320003'
PREFIX = 'c0ffee00-0000-4000-8000-00000000000'
ACTIONS = ['s1','s2','s3','s4','s5','s6','s7','s8','dial-left','dial-right','dial-press']

class Decoder:
    def __init__(self):
        self.held = self.used = False
    def decode(self, data):
        if len(data) < 5 or data[0] != 17 or data[2:4] != b'\x00\x01':
            return None
        size = data[4]
        if size > len(data) - 5:
            return None
        payload = data[5:5+size]
        if data[1] == 2 and size in (3,9) and payload[1:].lower() in (b'fn',b'function'):
            if payload[0] == 1 and not self.held:
                self.held, self.used = True, False
            if payload[0] == 2:
                tap = self.held and not self.used
                self.held = self.used = False
                return 'dial-press' if tap else None
        if data[1] != 5 or size != 36:
            return None
        uuid = payload.decode('ascii',errors='replace').lower()
        if not uuid.startswith(PREFIX) or uuid[-1] not in '123456789ab':
            return None
        index = '123456789ab'.index(uuid[-1])
        if self.held and index < 10:
            self.used = True
        return ('combo-' if self.held and index < 8 else '') + ACTIONS[index]

def normalize(data):
    data = bytes(data)
    # USB includes report ID. BLE may carry only the report payload.
    if len(data) >= 4 and data[0] in (2,5) and data[1:3] == b'\x00\x01':
        return b'\x11' + data
    return data

class Listener:
    def __init__(self, args):
        self.args = args
        self.decoder = Decoder()
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.token = bytes.fromhex(args.token)
        self.connected = False
        self.deadline = time.monotonic() + args.seconds if args.seconds else float('inf')
        self.parent = None
        self.stop_event = None
        if args.parent or args.stop_event:
            kernel = ctypes.WinDLL('kernel32',use_last_error=True)
            kernel.OpenProcess.restype = ctypes.c_void_p
            if args.parent:
                self.parent = kernel.OpenProcess(0x100000,False,args.parent)
            if args.stop_event:
                kernel.OpenEventW.restype = ctypes.c_void_p
                kernel.OpenEventW.argtypes = [ctypes.c_uint32,ctypes.c_int,ctypes.c_wchar_p]
                self.stop_event = kernel.OpenEventW(0x100000,False,args.stop_event)
            self.kernel = kernel
            kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p,ctypes.c_uint32]
            kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    def alive(self):
        if time.monotonic() >= self.deadline:
            return False
        if self.args.stop_event and (not self.stop_event or self.kernel.WaitForSingleObject(self.stop_event,0) != 258):
            return False
        if self.args.parent:
            return bool(self.parent) and self.kernel.WaitForSingleObject(self.parent,0) == 258
        return True
    def send(self, data):
        if self.args.port:
            self.socket.sendto(self.token + data,('127.0.0.1',self.args.port))
    def state(self, connected):
        if connected != self.connected:
            self.decoder = Decoder()
            print('CONNECTED' if connected else 'DISCONNECTED / RESET',flush=True)
        self.connected = connected
        self.send(bytes([1 if connected else 0]))
    def report(self, source, raw):
        data = normalize(raw)
        action = self.decoder.decode(data)
        print(f'{time.strftime("%H:%M:%S")} {source} RAW={bytes(raw).hex(" ")} ACTION={action or "-"}',flush=True)
        if data and data[0] == 17:
            self.send(b'\x02' + data)
    def close(self):
        self.state(False)
        self.socket.close()
        if self.parent:
            self.kernel.CloseHandle(self.parent)
        if self.stop_event:
            self.kernel.CloseHandle(self.stop_event)

async def ble_session(listener):
    from winrt.windows.devices.bluetooth import BluetoothLEDevice, BluetoothCacheMode
    from winrt.windows.devices.bluetooth.genericattributeprofile import GattClientCharacteristicConfigurationDescriptorValue as CCCD
    from winrt.windows.storage.streams import DataReader
    device = await BluetoothLEDevice.from_bluetooth_address_async(int(listener.args.address.replace(':',''),16))
    if device is None:
        raise RuntimeError('Windows has no paired EasyInput device at this address')
    services = []
    characteristic = None
    registration = None
    loop = asyncio.get_running_loop()
    try:
        result = await device.get_gatt_services_async(BluetoothCacheMode.CACHED)
        if int(result.status) != 0:
            raise RuntimeError(f'GATT services status={result.status}')
        services = list(result.services)
        for service in services:
            if str(service.uuid).lower() != SERVICE:
                continue
            result = await service.get_characteristics_async(BluetoothCacheMode.CACHED)
            for candidate in result.characteristics:
                if str(candidate.uuid).lower() == NOTIFY:
                    characteristic = candidate
        if characteristic is None:
            raise RuntimeError('Host notification characteristic absent')
        def received(sender, args):
            reader = DataReader.from_buffer(args.characteristic_value)
            raw = bytearray(reader.unconsumed_buffer_length)
            reader.read_bytes(raw)
            loop.call_soon_threadsafe(listener.report,'BLE',bytes(raw))
        registration = characteristic.add_value_changed(received)
        status = await characteristic.write_client_characteristic_configuration_descriptor_async(CCCD.NOTIFY)
        if int(status) != 0:
            raise RuntimeError(f'BLE subscribe status={status} (1=Unreachable)')
        listener.state(True)
        while listener.alive() and int(device.connection_status) == 1:
            listener.state(True)
            await asyncio.sleep(.25)
    finally:
        if characteristic is not None and registration is not None:
            characteristic.remove_value_changed(registration)
        for service in services:
            service.close()
        device.close()
        listener.state(False)

def usb_device():
    import hid
    for info in hid.enumerate(0x303a,0x1006):
        if info['usage_page'] == 0xff00 and info['usage'] == 2:
            device = hid.device()
            device.open_path(info['path'])
            device.set_nonblocking(True)
            print('USB OPEN',info,flush=True)
            return device
    return None

async def run(args):
    listener = Listener(args)
    try:
        while listener.alive():
            device = None
            try:
                if args.transport in ('auto','usb'):
                    device = usb_device()
                if device:
                    listener.state(True)
                    while listener.alive():
                        raw = device.read(1024)
                        if raw:
                            listener.report('USB',raw)
                        listener.state(True)
                        await asyncio.sleep(.02)
                elif args.transport in ('auto','ble'):
                    # Bound each connection attempt, including unreachable Windows GATT calls.
                    task = asyncio.create_task(ble_session(listener))
                    started = time.monotonic()
                    while not task.done() and listener.alive():
                        if not listener.connected and time.monotonic()-started > 15:
                            break
                        await asyncio.sleep(.25)
                    if not task.done():
                        task.cancel()
                    try:
                        await task
                    except asyncio.CancelledError:
                        pass
                else:
                    print('USB device absent',flush=True)
            except Exception as error:
                print(f'WAIT {type(error).__name__}: {error}',flush=True)
            finally:
                if device:
                    device.close()
                listener.state(False)
            for _ in range(8):
                if not listener.alive():
                    break
                await asyncio.sleep(.25)
    finally:
        listener.close()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--transport',choices=['auto','ble','usb'],default='auto')
    parser.add_argument('--address',default='D4:C1:9F:39:80:C6')
    parser.add_argument('--seconds',type=float,default=0,help='0 listens until Ctrl+C')
    parser.add_argument('--port',type=int,default=0,help='game-owned localhost UDP port')
    parser.add_argument('--token',default='')
    parser.add_argument('--parent',type=int,default=0)
    parser.add_argument('--stop-event',default='')
    parser.add_argument('--log',default='')
    parser.add_argument('--inventory',action='store_true')
    args = parser.parse_args()
    if args.log:
        sys.stdout = open(args.log,"a",encoding="utf-8",buffering=1)
        sys.stderr = sys.stdout
    if args.inventory:
        import hid
        print(json.dumps(hid.enumerate(),ensure_ascii=False,indent=2,default=str))
        return
    try:
        asyncio.run(run(args))
    except KeyboardInterrupt:
        pass

if __name__ == '__main__':
    main()
