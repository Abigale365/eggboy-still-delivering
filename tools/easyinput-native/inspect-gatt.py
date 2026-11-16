import asyncio,json
from winrt.windows.devices.bluetooth import BluetoothLEDevice,BluetoothCacheMode
async def main():
 d=await BluetoothLEDevice.from_bluetooth_address_async(int('D4C19F3980C6',16))
 inventory={'name':d.name,'connection_status':int(d.connection_status),'services':[]}
 r=await d.get_gatt_services_async(BluetoothCacheMode.CACHED)
 for s in r.services:
  cr=await s.get_characteristics_async(BluetoothCacheMode.CACHED)
  inventory['services'].append({'uuid':str(s.uuid),'status':int(cr.status),'characteristics':[{'uuid':str(c.uuid),'handle':c.attribute_handle,'properties':int(c.characteristic_properties)} for c in cr.characteristics]})
  s.close()
 d.close()
 print(json.dumps(inventory,indent=2))
asyncio.run(main())
