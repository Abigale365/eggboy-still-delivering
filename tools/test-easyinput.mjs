import {strict as assert} from 'node:assert';
import {readFileSync} from 'node:fs';
import {runInNewContext} from 'node:vm';

class DetailEvent extends Event {
  constructor(type, options) { super(type); this.detail = options.detail; }
}
const device = new EventTarget();
device.open = async () => { device.opened = true; };
device.close = async () => { device.opened = false; };
const hid = new EventTarget();
hid.requestDevice = async ({filters}) => {
  assert.equal(filters[0].vendorId, 0x303a);
  assert.equal(filters[0].productId, 0x1006);
  return [device];
};
const button = new EventTarget(), status = {}, window = new EventTarget();
const document = Object.assign(new EventTarget(), {
  hidden: false,
  hasFocus: () => true,
  querySelector: id => ({'#easyinput-connect': button, '#easyinput-status': status}[id])
});
const observed = [];
window.addEventListener('game-input', event => observed.push(event.detail.action));
const script = new URL('../Assets/WebGLTemplates/EGGBOY/easyinput.js', import.meta.url);
runInNewContext(readFileSync(script, 'utf8'), {
  navigator: {hid}, window, document, TextDecoder, Uint8Array, Event, CustomEvent: DetailEvent
});
button.dispatchEvent(new Event('click'));
await new Promise(resolve => setImmediate(resolve));
function report(type, payload, reportId = 17) {
  const bytes = Uint8Array.from([type, 0, 1, payload.length, ...payload]);
  device.dispatchEvent(Object.assign(new Event('inputreport'), {device, reportId, data: new DataView(bytes.buffer)}));
}
const key = suffix => report(5, Buffer.from('c0ffee00-0000-4000-8000-00000000000' + suffix));
const fn = state => report(2, [state, 70, 110]);
for (const suffix of '123456789ab') key(suffix);
fn(1); key('1'); fn(2);
fn(1); fn(2);
fn(1); window.dispatchEvent(new Event('blur')); fn(2);
document.hidden = true; key('2'); document.hidden = false;
report(5, Buffer.from('c0ffee00-0000-4000-8000-000000000001'), 99);
hid.dispatchEvent(Object.assign(new Event('disconnect'), {device}));
key('1');
assert.deepEqual(observed, ['s1','s2','s3','s4','s5','s6','s7','s8',
  'dial-left','dial-right','dial-press','combo-s1','dial-press']);
console.log('EasyInput simulated reports passed');
