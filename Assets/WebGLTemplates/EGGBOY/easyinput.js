(() => {
  const hid = navigator.hid;
  const filter = {vendorId: 0x303a, productId: 0x1006, usagePage: 0xff00, usage: 2};
  const prefix = "c0ffee00-0000-4000-8000-00000000000";
  const actions = ["s1","s2","s3","s4","s5","s6","s7","s8","dial-left","dial-right","dial-press"];
  const button = document.querySelector("#easyinput-connect");
  const status = document.querySelector("#easyinput-status");
  const decoder = new TextDecoder();
  let device, held = false, used = false;
  const reset = () => { held = used = false; };
  const paint = message => {
    button.textContent = device?.opened ? "断开 EasyInput" : "连接 EasyInput";
    status.textContent = message;
  };
  function receive({device: sender, reportId, data}) {
    if (sender !== device || document.hidden || !document.hasFocus()) { reset(); return; }
    if (reportId !== 0x11 || data.byteLength < 4 ||
        data.getUint8(1) !== 0 || data.getUint8(2) !== 1) return;
    const type = data.getUint8(0), length = data.getUint8(3);
    if (data.byteLength < length + 4) return;
    const bytes = new Uint8Array(data.buffer, data.byteOffset + 4, length);
    let action;
    if (type === 2 && (length === 3 || length === 9) &&
        /^(fn|function)$/i.test(decoder.decode(bytes.subarray(1)))) {
      if (bytes[0] === 1 && !held) { held = true; used = false; }
      if (bytes[0] !== 2) return;
      const tap = held && !used;
      reset();
      if (tap) action = "dial-press";
    } else if (type === 5 && length === 36) {
      const uuid = decoder.decode(bytes).toLowerCase();
      if (uuid.slice(0, -1) !== prefix || !/^[1-9ab]$/.test(uuid.slice(-1))) return;
      action = actions[parseInt(uuid.slice(-1), 16) - 1];
      if (held && /^s[1-8]$/.test(action)) { used = true; action = "combo-" + action; }
      if (held && /^dial-(left|right)$/.test(action)) used = true;
    }
    if (action) window.dispatchEvent(new CustomEvent("game-input", {detail: {action}}));
  }
  function detach() {
    device?.removeEventListener("inputreport", receive);
    device = undefined;
    reset();
    window.dispatchEvent(new Event("easyinput-reset"));
  }
  window.addEventListener("blur", reset);
  document.addEventListener("visibilitychange", reset);
  hid?.addEventListener("disconnect", e => {
    if (e.device === device) { detach(); paint("EasyInput 已拔出"); }
  });
  button.addEventListener("click", async () => {
    if (!hid) { paint("请在桌面 Chrome 或 Edge 的 HTTPS 或 localhost 页面连接"); return; }
    button.disabled = true;
    try {
      if (device?.opened) { await device.close(); detach(); paint("已断开"); return; }
      const [selected] = await hid.requestDevice({filters: [filter]});
      if (!selected) { paint("未选择设备"); return; }
      await selected.open();
      device = selected;
      device.addEventListener("inputreport", receive);
      paint("已连接");
      document.querySelector("#unity-canvas")?.focus();
    } catch (error) {
      paint(error.name === "NotAllowedError" ? "浏览器未授予设备权限" : "连接失败，请检查 USB 数据线");
    } finally {
      button.disabled = false;
    }
  });
  window.addEventListener("game-input", ({detail}) => { status.textContent = detail.action; });
})();
