// Browser-to-browser voice calling — the only place actual WebRTC APIs are
// touched, since Blazor has no native binding for RTCPeerConnection/getUserMedia.
// SignalR (voiceCallHub) only ever carries the SDP offer/answer and ICE
// candidates exchanged here; the audio itself flows peer-to-peer, never
// through the server.
window.voiceCallInterop = {
    _pc: null,
    _localStream: null,
    _dotNetRef: null,
    _audioCtx: null,
    _ringbackEl: null,
    _incomingRingEl: null,
    _incomingRingTimer: null,

    // CC0 (public domain) recordings, sourced from bigsoundbank.com and kept
    // under wwwroot/sounds — no attribution required, free for this or any
    // other use. Only the short connect chime is synthesized (Web Audio),
    // since it's a tiny UI blip rather than a "phone" sound.
    _soundUrl: function (file) {
        return '/sounds/' + file;
    },

    _ensureAudioCtx: function () {
        if (!this._audioCtx) this._audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        if (this._audioCtx.state === 'suspended') this._audioCtx.resume();
        return this._audioCtx;
    },

    _playTone: function (freq, durationMs, volume) {
        try {
            const ctx = this._ensureAudioCtx();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.frequency.value = freq;
            osc.type = 'sine';
            gain.gain.value = volume ?? 0.12;
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + durationMs / 1000);
        } catch { /* audio is a nice-to-have, never block the call over it */ }
    },

    // Caller-side "it's ringing on the other end" tone — a real 40s
    // ringback-tone recording with its own cadence baked in, just looped.
    startRingback: function () {
        this.stopRingback();
        try {
            const el = new Audio(this._soundUrl('ringback-tone.mp3'));
            el.loop = true;
            el.volume = 0.35;
            el.play().catch(() => { /* blocked until a user gesture unlocks audio — non-fatal */ });
            this._ringbackEl = el;
        } catch { /* audio is a nice-to-have, never block the call over it */ }
    },
    stopRingback: function () {
        if (this._ringbackEl) { this._ringbackEl.pause(); this._ringbackEl.currentTime = 0; this._ringbackEl = null; }
    },

    // Callee-side incoming-call ringtone — a short ring burst replayed with
    // a brief gap between rings (native <audio loop> would replay it with
    // zero silence, which reads as one continuous buzz rather than ringing).
    startIncomingRing: function () {
        this.stopIncomingRing();
        try {
            const el = new Audio(this._soundUrl('incoming-ring.mp3'));
            el.volume = 0.5;
            el.onended = () => {
                this._incomingRingTimer = setTimeout(() => el.play().catch(() => { }), 900);
            };
            el.play().catch(() => { /* blocked until a user gesture unlocks audio — non-fatal */ });
            this._incomingRingEl = el;
        } catch { /* audio is a nice-to-have, never block the call over it */ }
    },
    stopIncomingRing: function () {
        if (this._incomingRingTimer) { clearTimeout(this._incomingRingTimer); this._incomingRingTimer = null; }
        if (this._incomingRingEl) {
            this._incomingRingEl.onended = null;
            this._incomingRingEl.pause();
            this._incomingRingEl.currentTime = 0;
            this._incomingRingEl = null;
        }
    },

    playConnectSound: function () {
        this._playTone(660, 140, 0.15);
        setTimeout(() => this._playTone(880, 160, 0.15), 140);
    },

    // Same "three beeps" recording for a normal hang-up, a declined call, or
    // a missed/not-answered call — all three are "the call didn't continue."
    playEndSound: function () {
        try {
            const el = new Audio(this._soundUrl('call-end.mp3'));
            el.volume = 0.4;
            el.play().catch(() => { });
        } catch { /* audio is a nice-to-have, never block the call over it */ }
    },

    startLocalAudio: async function () {
        this._localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
        return true;
    },

    // Live mic-level meter — purely cosmetic feedback so the user can SEE
    // their own voice is actually being picked up (rather than wondering
    // silently whether their mic works). Reads the local stream directly via
    // an AnalyserNode, so it works even before the peer connection exists and
    // keeps working regardless of network/ICE state. Animates a handful of
    // bar elements straight via the DOM (not through Blazor) since this ticks
    // on every animation frame — routing that through .NET would be wasteful.
    _meterAnalyser: null,
    _meterSource: null,
    _meterRafId: null,
    startVoiceMeter: function () {
        this.stopVoiceMeter();
        if (!this._localStream) return;
        try {
            const ctx = this._ensureAudioCtx();
            const source = ctx.createMediaStreamSource(this._localStream);
            const analyser = ctx.createAnalyser();
            analyser.fftSize = 256;
            analyser.smoothingTimeConstant = 0.65;
            source.connect(analyser);
            this._meterAnalyser = analyser;
            this._meterSource = source;

            const data = new Uint8Array(analyser.frequencyBinCount);

            const tick = () => {
                if (!this._meterAnalyser) return; // stopped

                // Queried every frame, not cached — this meter is started
                // BEFORE BeginCall() flips the UI into the "InCall" state, so
                // #voiceWaveMeter doesn't exist in the DOM yet at startup. A
                // one-time querySelectorAll here would capture an empty
                // NodeList forever (bars.forEach silently doing nothing), so
                // re-query each tick until Blazor renders the bars in.
                const bars = document.querySelectorAll('#voiceWaveMeter .voice-wave-bar');

                const track = this._localStream && this._localStream.getAudioTracks()[0];
                const muted = !track || !track.enabled;

                analyser.getByteTimeDomainData(data);
                let sumSquares = 0;
                for (let i = 0; i < data.length; i++) {
                    const v = (data[i] - 128) / 128;
                    sumSquares += v * v;
                }
                const rms = Math.sqrt(sumSquares / data.length);
                const level = muted ? 0 : Math.min(1, rms * 4.5); // amplified — raw mic RMS reads very quiet

                bars.forEach((bar, i) => {
                    // Slight per-bar phase offset so it reads as a "wave" rather
                    // than every bar moving in lockstep.
                    const wobble = 0.55 + 0.45 * Math.sin((Date.now() / 140) + i * 0.9);
                    const scale = level <= 0.02 ? 0.12 : Math.max(0.12, Math.min(1, level * wobble + 0.1));
                    bar.style.transform = `scaleY(${scale})`;
                });

                this._meterRafId = requestAnimationFrame(tick);
            };
            tick();
        } catch { /* meter is cosmetic only — never let it block the call */ }
    },
    stopVoiceMeter: function () {
        if (this._meterRafId) { cancelAnimationFrame(this._meterRafId); this._meterRafId = null; }
        if (this._meterSource) { try { this._meterSource.disconnect(); } catch { } this._meterSource = null; }
        this._meterAnalyser = null;
    },

    createPeerConnection: function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        this._pc = new RTCPeerConnection({
            iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
        });

        if (this._localStream) {
            this._localStream.getTracks().forEach(track => this._pc.addTrack(track, this._localStream));
        }

        this._pc.onicecandidate = (event) => {
            if (event.candidate) {
                this._dotNetRef.invokeMethodAsync('OnLocalIceCandidate', JSON.stringify(event.candidate));
            }
        };

        this._pc.ontrack = (event) => {
            const audioEl = document.getElementById('remoteCallAudio');
            if (!audioEl) return;
            if (audioEl.srcObject !== event.streams[0]) {
                audioEl.srcObject = event.streams[0];
            }
            // The `autoplay` attribute only reliably kicks in the first time
            // this element ever plays. On later calls it's already been
            // played-and-paused once (hangUp() sets srcObject back to null),
            // and re-attaching a new stream to an element in that state does
            // NOT reliably resume playback in Chrome/Edge — hence "first call
            // has audio, every call after is silent." Explicitly (re)starting
            // playback here fixes that regardless of the element's history.
            const playPromise = audioEl.play();
            if (playPromise && typeof playPromise.catch === 'function') {
                playPromise.catch(() => { /* will retry on the next ontrack firing */ });
            }
        };
    },

    createOffer: async function () {
        const offer = await this._pc.createOffer();
        await this._pc.setLocalDescription(offer);
        return JSON.stringify(offer);
    },

    createAnswer: async function (offerJson) {
        await this._pc.setRemoteDescription(JSON.parse(offerJson));
        const answer = await this._pc.createAnswer();
        await this._pc.setLocalDescription(answer);
        return JSON.stringify(answer);
    },

    setRemoteAnswer: async function (answerJson) {
        await this._pc.setRemoteDescription(JSON.parse(answerJson));
    },

    addIceCandidate: async function (candidateJson) {
        if (!this._pc) return;
        try { await this._pc.addIceCandidate(JSON.parse(candidateJson)); }
        catch { /* candidates that arrive before the remote description is set are harmless to drop */ }
    },

    setMuted: function (muted) {
        if (!this._localStream) return;
        this._localStream.getAudioTracks().forEach(track => track.enabled = !muted);
    },

    hangUp: function () {
        this.stopRingback();
        this.stopIncomingRing();
        this.stopVoiceMeter();
        if (this._localStream) {
            this._localStream.getTracks().forEach(track => track.stop());
            this._localStream = null;
        }
        if (this._pc) {
            this._pc.close();
            this._pc = null;
        }
        const audioEl = document.getElementById('remoteCallAudio');
        if (audioEl) { audioEl.pause(); audioEl.srcObject = null; }
        this._dotNetRef = null;
    }
};
