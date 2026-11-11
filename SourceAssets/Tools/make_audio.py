# Synthesizes every game sound of Red Zone (one-shots and the two music loops) with numpy.
# Run from the project root:  python -I SourceAssets/Tools/make_audio.py [sfx] [music]
# One-shots: WAV 48 kHz 16-bit mono, peak -1 dB, short fades. Music: OGG Vorbis stereo, seamless loops
# (the reverb/delay tail of the last bar is folded back onto the start).
import os
import sys

import numpy as np
import soundfile as sf

SR = 48000
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
SFX = os.path.join(ROOT, "Assets", "_Project", "Audio", "Resources", "Sfx")
MUSIC = os.path.join(ROOT, "Assets", "_Project", "Audio", "Resources", "Music")
rng = np.random.default_rng(7)


def t_axis(sec):
    return np.arange(int(sec * SR)) / SR


def env(n, a=0.004, r=0.08, sustain=None):
    """Attack then exponential release over n samples."""
    t = np.arange(n) / SR
    e = np.minimum(1.0, t / max(a, 1e-4))
    rel = np.exp(-t / max(r, 1e-4)) if sustain is None else np.where(t < sustain, 1.0, np.exp(-(t - sustain) / max(r, 1e-4)))
    return e * rel


def osc(freq, sec, kind="sine", phase=0.0):
    t = t_axis(sec)
    f = np.broadcast_to(freq, t.shape) if np.ndim(freq) else np.full(t.shape, float(freq))
    ph = 2 * np.pi * np.cumsum(f) / SR + phase
    if kind == "sine":
        return np.sin(ph)
    if kind == "square":
        return np.sign(np.sin(ph)) * 0.6
    if kind == "saw":
        return 2 * ((ph / (2 * np.pi)) % 1.0) - 1
    if kind == "tri":
        return 2 * np.abs(2 * ((ph / (2 * np.pi)) % 1.0) - 1) - 1
    raise ValueError(kind)


def lowpass(x, cutoff):
    """One-pole low-pass, run twice (12 dB/oct)."""
    a = np.exp(-2 * np.pi * cutoff / SR)
    for _ in range(2):
        y = np.empty_like(x)
        acc = 0.0
        for i in range(len(x)):
            acc = (1 - a) * x[i] + a * acc
            y[i] = acc
        x = y
    return x


def lowpass_fast(x, cutoff):
    """FFT brick-ish low-pass with a soft knee, for long buffers."""
    X = np.fft.rfft(x, axis=0)
    f = np.fft.rfftfreq(x.shape[0], 1 / SR)
    g = 1 / (1 + (f / cutoff) ** 4)
    return np.fft.irfft(X * (g[:, None] if x.ndim == 2 else g), n=x.shape[0], axis=0)


def highpass_fast(x, cutoff):
    X = np.fft.rfft(x, axis=0)
    f = np.fft.rfftfreq(x.shape[0], 1 / SR)
    g = 1 - 1 / (1 + (f / max(cutoff, 1)) ** 4)
    return np.fft.irfft(X * (g[:, None] if x.ndim == 2 else g), n=x.shape[0], axis=0)


def noise(sec):
    return rng.uniform(-1, 1, int(sec * SR))


def place(buf, x, at):
    i = int(at * SR)
    j = min(len(buf), i + len(x))
    if j > i:
        buf[i:j] += x[: j - i]


def finish(x, peak_db=-1.0, fade=0.004):
    x = np.asarray(x, dtype=np.float64)
    n = int(fade * SR)
    if n and len(x) > 2 * n:
        x[:n] *= np.linspace(0, 1, n)
        x[-n:] *= np.linspace(1, 0, n)
    p = np.max(np.abs(x)) or 1.0
    return x / p * 10 ** (peak_db / 20)


def write(name, x):
    os.makedirs(SFX, exist_ok=True)
    path = os.path.join(SFX, name + ".wav")
    sf.write(path, finish(x), SR, subtype="PCM_16")
    print("sfx", name, f"{len(x) / SR:.2f}s")


def blip(freq, sec, kind="square", glide=1.0, r=0.05, cutoff=6000):
    f = np.geomspace(freq, freq * glide, int(sec * SR))
    x = osc(f, sec, kind) * env(len(f), 0.002, r)
    return lowpass_fast(x, cutoff)


def chord_tones(freqs, sec, kind, r):
    out = np.zeros(int(sec * SR))
    for f in freqs:
        out += osc(f, sec, kind) * env(len(out), 0.003, r)
    return out


# ------------------------------------------------------------------ one-shots

def make_sfx():
    # move: a short soft tick, every arrow press (quiet, it repeats a lot)
    write("move", blip(880, 0.06, "tri", 0.85, 0.018, 5000) + 0.25 * noise(0.06) * env(int(0.06 * SR), 0.001, 0.006))
    # bump: nothing could move, a dull low thud
    write("bump", blip(140, 0.12, "sine", 0.6, 0.04, 900) + 0.3 * lowpass_fast(noise(0.12), 600) * env(int(0.12 * SR), 0.001, 0.02))
    # fork: a thread is born, a rising two-note chirp (bright, friendly)
    a = blip(660, 0.09, "square", 1.25, 0.05, 4000)
    b = blip(990, 0.12, "square", 1.1, 0.07, 4500)
    x = np.zeros(int(0.24 * SR)); place(x, a, 0); place(x, b, 0.07); write("fork", x)
    # forced: fork pushed into the critical region, an alarm-ish down chirp with grit
    x = np.zeros(int(0.42 * SR))
    for k in range(3):
        place(x, blip(1200, 0.12, "saw", 0.55, 0.06, 3000), k * 0.11)
    x += 0.15 * noise(0.42) * env(len(x), 0.002, 0.12)
    write("forced", x)
    # finish: exit(0), a soft settled "lock" (two descending sine notes)
    x = np.zeros(int(0.35 * SR)); place(x, blip(784, 0.2, "sine", 1.0, 0.09, 8000), 0); place(x, blip(523, 0.3, "sine", 1.0, 0.14, 8000), 0.06)
    write("finish", x)
    # finish_bad: stopped on a gate or in the critical region, a dissonant buzz
    x = chord_tones([233, 247], 0.4, "square", 0.14)
    write("finish_bad", lowpass_fast(x, 2200))
    # enter: P(S), a short rising pip with a metallic edge
    x = np.zeros(int(0.16 * SR)); place(x, blip(520, 0.14, "square", 1.5, 0.06, 3500), 0); place(x, 0.4 * blip(1560, 0.1, "sine", 1.2, 0.03), 0)
    write("enter", x)
    # leave: V(S), the same pip falling
    write("leave", blip(780, 0.14, "square", 0.66, 0.06, 3500))
    # signal: V() pad, a sparkling up arpeggio
    x = np.zeros(int(0.6 * SR))
    for i, f in enumerate([523, 659, 784, 1046, 1318]):
        place(x, blip(f, 0.25, "tri", 1.0, 0.12, 9000), i * 0.055)
    write("signal", x)
    # crash: race condition, a glitch burst (bit-crushed noise and a falling saw)
    n = int(0.9 * SR)
    nz = noise(0.9)
    crushed = np.repeat(nz[::40], 40)[:n] * env(n, 0.001, 0.25)
    fall = osc(np.geomspace(900, 60, n), 0.9, "saw") * env(n, 0.002, 0.35)
    write("crash", 0.6 * lowpass_fast(crushed, 7000) + 0.7 * lowpass_fast(fall, 3000))
    # panic: the blue screen arrives, a low descending minor chord
    x = np.zeros(int(1.6 * SR))
    for i, f in enumerate([392, 311, 262, 196]):
        place(x, lowpass_fast(chord_tones([f, f * 1.5], 0.7, "saw", 0.3), 1800), i * 0.22)
    write("panic", x)
    # win: process complete, a major up arpeggio ending on a chord
    x = np.zeros(int(1.4 * SR))
    for i, f in enumerate([523, 659, 784, 1046]):
        place(x, blip(f, 0.3, "square", 1.0, 0.12, 5000), i * 0.09)
    place(x, lowpass_fast(chord_tones([523, 659, 784, 1046], 1.0, "tri", 0.45), 7000), 0.36)
    write("win", x)
    # star: a bell ping
    t = t_axis(0.7)
    bell = (np.sin(2 * np.pi * 1568 * t) + 0.5 * np.sin(2 * np.pi * 1568 * 2.76 * t) + 0.3 * np.sin(2 * np.pi * 1568 * 5.4 * t)) * np.exp(-t / 0.18)
    write("star", bell)
    # callout: a tip appears, a gentle two-tone notification
    x = np.zeros(int(0.4 * SR)); place(x, blip(988, 0.18, "sine", 1.0, 0.08), 0); place(x, blip(1319, 0.22, "sine", 1.0, 0.1), 0.09)
    write("callout", x)


# ------------------------------------------------------------------ music

NOTE = {n: i for i, n in enumerate(["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"])}


def hz(name, octave):
    return 440.0 * 2 ** ((NOTE[name] + 12 * (octave + 1) - 69) / 12)


def chord(root, quality, octave):
    steps = {"m": [0, 3, 7], "M": [0, 4, 7], "m7": [0, 3, 7, 10], "M7": [0, 4, 7, 11], "sus2": [0, 2, 7]}[quality]
    base = NOTE[root] + 12 * (octave + 1)
    return [440.0 * 2 ** ((base + s - 69) / 12) for s in steps]


def pan(x, p):
    """Equal-power pan, p in -1..1, returns (n, 2)."""
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def delay(x, sec, fb, mix):
    d = int(sec * SR)
    y = x.copy()
    for k in range(1, 6):
        g = mix * fb ** (k - 1)
        if g < 0.01:
            break
        y[d * k:] += x[: len(x) - d * k] * g
    return y


def render_song(bpm, bars, progression, layers, seconds_tail=4.0):
    beat = 60 / bpm
    bar = 4 * beat
    total = bars * bar
    n = int((total + seconds_tail) * SR)
    out = np.zeros((n, 2))
    for layer in layers:
        out += layer(beat, bar, bars, progression, n)
    # fold the tail onto the start so the loop is seamless
    loop_n = int(total * SR)
    tail = out[loop_n:]
    out = out[:loop_n].copy()
    out[: len(tail)] += tail
    out = highpass_fast(out, 35)
    peak = np.max(np.abs(out)) or 1
    return out / peak * 10 ** (-3 / 20)


def pad_layer(octave=3, gain=0.22, cutoff=1400, kind="saw"):
    def f(beat, bar, bars, prog, n):
        buf = np.zeros((n, 2))
        for i in range(bars):
            root, q = prog[i % len(prog)]
            for k, fr in enumerate(chord(root, q, octave)):
                sec = bar + 0.6
                for det, p in ((0.997, -0.5), (1.003, 0.5)):
                    x = osc(fr * det, sec, kind) * env(int(sec * SR), 0.35, 0.5, sustain=bar - 0.1)
                    x = lowpass_fast(x, cutoff) * gain / 3
                    seg = pan(x, p)
                    s = int(i * bar * SR)
                    buf[s:s + len(seg)] += seg[: max(0, min(len(seg), n - s))]
        return buf
    return f


def arp_layer(octave=5, gain=0.16, steps_per_beat=2, pattern=(0, 1, 2, 1, 0, 2, 1, 2), cutoff=3200, kind="square", echo=0.36):
    def f(beat, bar, bars, prog, n):
        mono = np.zeros(n)
        step = beat / steps_per_beat
        per_bar = int(round(bar / step))
        for i in range(bars):
            root, q = prog[i % len(prog)]
            tones = chord(root, q, octave)
            for k in range(per_bar):
                fr = tones[pattern[k % len(pattern)] % len(tones)]
                x = osc(fr, step * 1.6, kind) * env(int(step * 1.6 * SR), 0.003, step * 0.45)
                place(mono, lowpass_fast(x, cutoff) * gain, i * bar + k * step)
        mono = delay(mono, beat * 0.75, 0.45, echo)
        return pan(mono, 0.25)
    return f


def bass_layer(octave=2, gain=0.3, rhythm=(0, 1, 2, 3), cutoff=500):
    def f(beat, bar, bars, prog, n):
        mono = np.zeros(n)
        for i in range(bars):
            root, q = prog[i % len(prog)]
            fr = hz(root, octave)
            for b in rhythm:
                x = osc(fr, beat * 0.9, "tri") * env(int(beat * 0.9 * SR), 0.004, beat * 0.5)
                x += 0.4 * osc(fr * 2, beat * 0.9, "square") * env(int(beat * 0.9 * SR), 0.004, beat * 0.25)
                place(mono, lowpass_fast(x, cutoff) * gain, i * bar + b * beat)
        return pan(mono, 0.0)
    return f


def hat_layer(gain=0.05, per_beat=2, skip_first_bars=0):
    def f(beat, bar, bars, prog, n):
        mono = np.zeros(n)
        hit = highpass_fast(noise(0.05), 7000) * env(int(0.05 * SR), 0.001, 0.012)
        for i in range(skip_first_bars, bars):
            for k in range(4 * per_beat):
                g = gain * (1.0 if k % per_beat else 0.6)
                place(mono, hit * g, i * bar + k * beat / per_beat)
        return pan(mono, -0.3)
    return f


def kick_layer(gain=0.35, beats=(0, 2), skip_first_bars=0):
    def f(beat, bar, bars, prog, n):
        mono = np.zeros(n)
        sec = 0.35
        kick = osc(np.geomspace(140, 45, int(sec * SR)), sec, "sine") * env(int(sec * SR), 0.001, 0.12)
        for i in range(skip_first_bars, bars):
            for b in beats:
                place(mono, kick * gain, i * bar + b * beat)
        return pan(mono, 0.0)
    return f


def write_music(name, x):
    os.makedirs(MUSIC, exist_ok=True)
    path = os.path.join(MUSIC, name + ".ogg")
    # one big sf.write of a long Vorbis file overflows libsndfile's stack on Windows (libsndfile issue #1093):
    # writing it in small blocks avoids the crash
    with sf.SoundFile(path, "w", SR, x.shape[1], format="OGG", subtype="VORBIS") as f:
        for i in range(0, len(x), 4096):
            f.write(np.ascontiguousarray(x[i:i + 4096]))
    print("music", name, f"{len(x) / SR:.1f}s")


def make_music():
    # menu: calm, 84 bpm, Am F C G with a slow glassy arpeggio, no drums
    menu_prog = [("A", "m7"), ("F", "M7"), ("C", "M7"), ("G", "sus2")]
    menu = render_song(84, 16, menu_prog, [
        pad_layer(3, 0.26, 1100),
        arp_layer(5, 0.12, 2, (0, 2, 1, 3, 2, 1, 0, 2), 2600, "tri", 0.4),
        bass_layer(2, 0.22, (0, 2.5), 400),
    ])
    write_music("menu", menu)
    # game: focused, 112 bpm, Dm Bb F C, pulsing bass, square arp, light kick and hats after the intro bars
    game_prog = [("D", "m"), ("A#", "M"), ("F", "M"), ("C", "M")]
    game = render_song(112, 16, game_prog, [
        pad_layer(3, 0.16, 1500),
        arp_layer(5, 0.13, 4, (0, 1, 2, 1, 2, 0, 1, 2), 3400, "square", 0.3),
        bass_layer(2, 0.3, (0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5), 520),
        kick_layer(0.4, (0, 1, 2, 3), 4),
        hat_layer(0.045, 2, 4),
    ])
    write_music("game", game)


if __name__ == "__main__":
    what = set(sys.argv[1:]) or {"sfx", "music"}
    if "sfx" in what:
        make_sfx()
    if "music" in what:
        make_music()
