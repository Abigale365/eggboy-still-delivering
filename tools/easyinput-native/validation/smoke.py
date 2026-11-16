import sys, socket, subprocess,time,os
sys.path.insert(0,'tools/easyinput-native')
from listener import Listener,normalize
from types import SimpleNamespace
from winrt.windows.storage.streams import DataReader,DataWriter
writer=DataWriter(); writer.write_bytes(b'\x01\x02\x03')
reader=DataReader.from_buffer(writer.detach_buffer()); data=bytearray(reader.unconsumed_buffer_length); reader.read_bytes(data)
assert data==b'\x01\x02\x03'
sock=socket.socket(socket.AF_INET,socket.SOCK_DGRAM);sock.bind(('127.0.0.1',0));sock.settimeout(2)
args=SimpleNamespace(token='0102',seconds=0,parent=0,stop_event='',port=sock.getsockname()[1])
l=Listener(args)
payload=b'\x05\x00\x01\x24c0ffee00-0000-4000-8000-000000000001'
l.state(True); assert sock.recv(1024)==b'\x01\x02\x01'
l.report('SIMULATED',payload);assert sock.recv(1024)==b'\x01\x02\x02\x11'+payload
l.close();assert sock.recv(1024)==b'\x01\x02\x00';sock.close()
parent=subprocess.Popen([sys.executable,'-c','import time; time.sleep(1)'])
started=time.monotonic()
p=subprocess.Popen(['Assets/StreamingAssets/EasyInputNative/EasyInputRelay.exe','--transport','usb','--parent',str(parent.pid)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
output=p.communicate(timeout=8)[0]
assert p.returncode==0,(p.returncode,output)
assert time.monotonic()-started<8
print('PASS WinRT buffer, BLE report normalization, authenticated UDP packets, packaged helper parent-exit cleanup')
