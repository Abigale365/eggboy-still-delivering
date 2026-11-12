mergeInto(LibraryManager.library, {
  $TideTownMic: {
    generation: 0,
    context: null,
    stream: null,
    source: null,
    analyser: null,
    samples: null,

    stop: function () {
      this.generation++;
      if (this.source) this.source.disconnect();
      if (this.stream) this.stream.getTracks().forEach(function (track) { track.stop(); });
      if (this.context) this.context.close().catch(function () {});
      this.context = null;
      this.stream = null;
      this.source = null;
      this.analyser = null;
      this.samples = null;
    }
  },

  TideTown_StartMic__deps: ['$TideTownMic'],
  TideTown_StartMic: function () {
    var mic = TideTownMic;
    var Context = window.AudioContext || window.webkitAudioContext;
    if (mic.context || !Context || !navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return;

    var generation = ++mic.generation;
    function onFailure() {
      if (mic.generation === generation) mic.stop();
    }

    try {
      var context = new Context();
      mic.context = context;
      // 从点击事件唤醒音频上下文，等待授权时不连接任何输出。
      context.resume().catch(onFailure);
      navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false },
        video: false
      }).then(function (stream) {
        if (mic.generation !== generation) {
          stream.getTracks().forEach(function (track) { track.stop(); });
          return;
        }
        mic.stream = stream;
        mic.source = context.createMediaStreamSource(stream);
        mic.analyser = context.createAnalyser();
        mic.analyser.fftSize = 1024;
        mic.samples = new Float32Array(mic.analyser.fftSize);
        mic.source.connect(mic.analyser);
      }).catch(onFailure);
    } catch (error) {
      onFailure();
    }
  },

  TideTown_ReadMicRms__deps: ['$TideTownMic'],
  TideTown_ReadMicRms: function () {
    var mic = TideTownMic;
    if (!mic.analyser || mic.context.state !== 'running' || !mic.stream.active) return -1;
    mic.analyser.getFloatTimeDomainData(mic.samples);
    var sum = 0;
    for (var i = 0; i < mic.samples.length; i++) sum += mic.samples[i] * mic.samples[i];
    return Math.sqrt(sum / mic.samples.length);
  },

  TideTown_StopMic__deps: ['$TideTownMic'],
  TideTown_StopMic: function () {
    TideTownMic.stop();
  }
});
