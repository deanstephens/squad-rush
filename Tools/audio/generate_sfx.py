#!/usr/bin/env python3
"""
Procedural sound effects and music loops for Squad Rush (standard library only).

A small sfxr-style synthesiser: oscillators (square/saw/tri/sine), pitched noise, pitch slides,
vibrato, arpeggios, one-pole filters with sweeps and soft-clip drive. Everything is seeded, so
re-running produces identical files.

    python3 Tools/audio/generate_sfx.py            -> Assets/Audio/Sfx/*.wav, Assets/Audio/Music/*.wav
"""
import math, os, random, struct, wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SFX_DIR = os.path.join(ROOT, "Assets", "Audio", "Sfx")
MUSIC_DIR = os.path.join(ROOT, "Assets", "Audio", "Music")
SR = 44100


# ------------------------------------------------------------------ core synth

def osc(kind, p, duty):
    p %= 1.0
    if kind == "square":
        return 1.0 if p < duty else -1.0
    if kind == "saw":
        return 2.0 * p - 1.0
    if kind == "tri":
        return 4.0 * abs(p - 0.5) - 1.0
    return math.sin(2.0 * math.pi * p)


def synth(dur, f0=440.0, f1=None, wave="square", duty=0.5, attack=0.002, curve=1.0, vib=(0.0, 0.0),
          noise=0.0, noise_hold=1, lp=None, lp_end=None, hp=None, drive=0.0, arp=None, sr=SR, seed=1):
    """One voice. `curve` shapes the decay (0 = flat, 1 = linear, >1 = snappier). `arp` = [(time_frac, pitch_mult)]."""
    rnd = random.Random(seed)
    n = max(1, int(dur * sr))
    f1 = f0 if f1 is None else f1
    out = [0.0] * n
    phase = 0.0
    lp_y = hp_y = 0.0
    held, hold = 0.0, 0
    for i in range(n):
        u = i / (n - 1) if n > 1 else 0.0
        t = i / sr
        f = f0 * (f1 / f0) ** u if f0 > 0 and f1 > 0 else f0 + (f1 - f0) * u
        if arp:
            m = 1.0
            for tf, mult in arp:
                if u >= tf:
                    m = mult
            f *= m
        if vib[1]:
            f *= 1.0 + vib[1] * math.sin(2.0 * math.pi * vib[0] * t)
        phase += f / sr
        s = osc(wave, phase, duty) if wave else 0.0
        if noise:
            if hold <= 0:
                held, hold = rnd.uniform(-1.0, 1.0), noise_hold
            hold -= 1
            s = s * (1.0 - noise) + held * noise
        env = min(1.0, t / attack) if attack > 0 else 1.0
        if curve:
            env *= (1.0 - u) ** curve
        s *= env
        if lp:
            fc = lp * (lp_end / lp) ** u if lp_end else lp
            a = 1.0 - math.exp(-2.0 * math.pi * min(fc, sr * 0.45) / sr)
            lp_y += a * (s - lp_y)
            s = lp_y
        if hp:
            a = 1.0 - math.exp(-2.0 * math.pi * hp / sr)
            hp_y += a * (s - hp_y)
            s -= hp_y
        if drive:
            s = math.tanh(s * (1.0 + drive)) / math.tanh(1.0 + drive)
        out[i] = s
    return out


def mix(layers, sr=SR, length=None):
    """layers: (samples, offset_seconds, gain). Returns the summed buffer."""
    end = max(int(off * sr) + len(s) for s, off, g in layers)
    buf = [0.0] * (length or end)
    for s, off, g in layers:
        o = int(off * sr)
        for i, v in enumerate(s):
            j = o + i
            if j < len(buf):
                buf[j] += v * g
    return buf


def finish(buf, peak=0.89, sr=SR, loop=False):
    # Remove DC (quarter-duty squares are lopsided): mean subtraction keeps loops seamless,
    # a one-pole blocker keeps one-shots starting at zero.
    if loop:
        mean = sum(buf) / len(buf)
        buf = [v - mean for v in buf]
    else:
        y = px = 0.0
        blocked = []
        for v in buf:
            y = v - px + 0.995 * y
            px = v
            blocked.append(y)
        buf = blocked
    m = max(1e-9, max(abs(v) for v in buf))
    k = peak / m
    out = [v * k for v in buf]
    if not loop:
        fade = min(len(buf), int(0.004 * sr))
        for i in range(fade):   # de-click the tail
            out[-1 - i] *= i / fade
    return out


def write(path, buf, sr=SR):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, v)) * 32767)) for v in buf))


def note(n):
    return 440.0 * 2.0 ** ((n - 69) / 12.0)


def seq(notes, step, **kw):
    """Consecutive notes (midi, or None for a rest), each `step` seconds."""
    layers = []
    for i, n in enumerate(notes):
        if n is not None:
            layers.append((synth(step * 1.6, note(n), seed=i + 7, **kw), i * step, 1.0))
    return mix(layers)


# ------------------------------------------------------------------ effects

def noise_burst(dur, lp=None, lp_end=None, hp=None, hold=1, curve=2.0, drive=0.0, seed=3):
    return synth(dur, 100, wave=None, noise=1.0, noise_hold=hold, lp=lp, lp_end=lp_end, hp=hp, curve=curve, drive=drive, seed=seed)


SFX = {
    # --- shared / UI
    "ui_click": lambda: mix([(synth(0.045, 1900, 1300, duty=0.5, curve=2.5), 0, 1.0)]),
    "purchase": lambda: mix([(synth(0.07, note(83), duty=0.5, curve=0.3), 0, 0.8),
                             (synth(0.26, note(88), duty=0.5, curve=1.4), 0.07, 0.8)]),
    "perk_pick": lambda: seq([72, 76, 79, 84, 88, 91], 0.035, wave="square", duty=0.25, curve=1.5, lp=6000),
    "game_over": lambda: mix([(seq([67, 63, 60], 0.2, wave="square", duty=0.5, curve=1.0, lp=2500), 0, 0.8),
                              (synth(0.8, note(48), wave="tri", curve=1.2), 0.6, 1.0)]),

    # --- treadmill
    "squad_shot": lambda: mix([(synth(0.09, 950, 320, duty=0.3, curve=2.0, lp=5000), 0, 0.8),
                               (noise_burst(0.04, hp=2500, curve=3), 0, 0.35)]),
    "enemy_hit": lambda: mix([(noise_burst(0.05, hp=1800, hold=2, curve=3), 0, 0.8),
                              (synth(0.025, 2600, 1900, duty=0.5, curve=2), 0, 0.4)]),
    "enemy_die": lambda: mix([(noise_burst(0.035, lp=4500, hold=3, curve=2.5, seed=s), off, g)
                              for s, off, g in ((11, 0, 1.0), (12, 0.035, 0.8), (13, 0.07, 0.65), (14, 0.11, 0.45))]
                             + [(synth(0.13, 170, 55, wave="sine", curve=1.5), 0, 0.9)]),
    "gate_good": lambda: mix([(seq([72, 76, 79, 84], 0.055, wave="square", duty=0.25, curve=1.2, lp=7000), 0, 0.9),
                              (synth(0.3, note(96), wave="sine", curve=2.0), 0.2, 0.25)]),
    "gate_bad": lambda: mix([(synth(0.32, 320, 110, wave="saw", vib=(18, 0.04), curve=1.0, lp=1400, drive=1.0), 0, 1.0)]),
    "unit_lost": lambda: mix([(synth(0.18, 230, 85, wave="tri", curve=1.3), 0, 1.0),
                              (noise_burst(0.1, lp=500, curve=2), 0, 0.6)]),
    "boss_spawn": lambda: mix([(synth(1.3, 115, 68, wave="saw", vib=(6, 0.03), curve=0.8, lp=900, drive=1.5), 0, 1.0),
                               (synth(1.3, 172, 102, wave="saw", vib=(6, 0.03), curve=0.8, lp=700, drive=1.0), 0, 0.5),
                               (noise_burst(1.2, lp=260, curve=0.9), 0, 0.7)]),
    "boss_bite": lambda: mix([(noise_burst(0.2, lp=1600, hold=4, curve=1.6, drive=2.0), 0, 0.9),
                              (synth(0.16, 190, 60, duty=0.5, curve=1.4, drive=1.5), 0, 0.8)]),
    "level_clear": lambda: mix([(seq([72, 76, 79], 0.12, wave="square", duty=0.25, curve=0.9, lp=6000), 0, 0.8)]
                               + [(synth(0.7, note(n), wave="square", duty=0.5, curve=1.1, lp=4500), 0.36, 0.35) for n in (84, 88, 91)]),

    # --- arena guns
    "gun_pistol": lambda: mix([(synth(0.12, 1450, 360, duty=0.5, curve=1.6, lp=6000), 0, 0.85),
                               (noise_burst(0.03, hp=3000, curve=3), 0, 0.35)]),
    "gun_smg": lambda: mix([(synth(0.06, 1850, 720, duty=0.3, curve=1.8, lp=6000), 0, 0.8),
                            (noise_burst(0.03, hp=2600, curve=3), 0, 0.35)]),
    "gun_shotgun": lambda: mix([(noise_burst(0.32, lp=3200, lp_end=380, curve=1.5, drive=2.0), 0, 1.0),
                                (synth(0.16, 125, 48, wave="sine", curve=1.3), 0, 0.9)]),
    "gun_rifle": lambda: mix([(noise_burst(0.05, hp=1500, curve=2.5), 0, 0.9),
                              (synth(0.26, 950, 190, wave="saw", curve=1.4, lp=2600, lp_end=500), 0, 0.7),
                              (noise_burst(0.42, lp=800, curve=1.3), 0.02, 0.35)]),
    "gun_minigun": lambda: mix([(synth(0.045, 1150, 620, duty=0.25, curve=1.8, lp=5000), 0, 0.75),
                                (noise_burst(0.03, hp=3200, curve=3), 0, 0.3)]),
    "gun_rocket": lambda: mix([(noise_burst(0.5, lp=500, lp_end=2600, hold=2, curve=0.7), 0, 0.9),
                               (synth(0.32, 210, 420, wave="saw", curve=1.2, lp=1200), 0, 0.35)]),
    "explosion": lambda: mix([(noise_burst(0.85, lp=2200, lp_end=140, curve=1.2, drive=3.0), 0, 1.0),
                              (synth(0.42, 92, 34, wave="sine", curve=1.2), 0, 1.0)]),

    # --- arena events
    "xp_pickup": lambda: mix([(synth(0.06, 1250, 2100, duty=0.5, curve=2.0, lp=7000), 0, 1.0)]),
    "level_up": lambda: mix([(seq([72, 76, 79, 84, 88], 0.05, wave="square", duty=0.25, curve=1.2, lp=7000), 0, 0.9),
                             (synth(0.45, note(96), wave="sine", vib=(9, 0.01), curve=1.6), 0.25, 0.4)]),
    "player_hurt": lambda: mix([(synth(0.16, 420, 150, duty=0.5, curve=1.3, drive=1.2, lp=3000), 0, 0.9),
                                (noise_burst(0.08, lp=1200, curve=2), 0, 0.5)]),
    "wave_start": lambda: mix([(synth(0.2, note(57), wave="saw", vib=(5, 0.01), curve=0.6, lp=1500), 0, 0.9),
                               (synth(0.4, note(64), wave="saw", vib=(5, 0.01), curve=1.0, lp=1500), 0.18, 0.9)]),
    "wave_clear": lambda: mix([(synth(0.12, note(76), wave="sine", curve=1.0), 0, 0.8),
                               (synth(0.35, note(83), wave="tri", curve=1.4), 0.1, 0.8)]),
    "boss_horn": lambda: mix([(synth(1.4, note(45), wave="saw", vib=(4.5, 0.012), attack=0.08, curve=0.7, lp=1000, drive=0.8), 0, 1.0),
                              (synth(1.4, note(52), wave="saw", vib=(4.5, 0.012), attack=0.08, curve=0.7, lp=900), 0, 0.6),
                              (noise_burst(1.2, lp=220, curve=1.0), 0, 0.5)]),
    "enrage": lambda: mix([(synth(0.95, 155, 88, wave="saw", vib=(13, 0.08), curve=0.9, lp=1300, drive=3.0), 0, 1.0),
                           (noise_burst(0.9, lp=700, curve=1.0, drive=2.0), 0, 0.6)]),
}


# ------------------------------------------------------------------ music

def render_loop(bpm, chords, bars_per_chord, style, sr=22050, seed=5):
    """Seamless loop: notes spilling past the end wrap to the start."""
    step = 60.0 / bpm / 4.0                       # 16th notes
    steps = len(chords) * bars_per_chord * 16
    length = int(steps * step * sr)
    buf = [0.0] * length

    def add(samples, t, gain):
        o = int(t * sr)
        for i, v in enumerate(samples):
            buf[(o + i) % length] += v * gain

    # Pre-render drum hits once.
    kick = synth(0.18, 125, 42, wave="sine", curve=1.4, sr=sr)
    snare = mix([(synth(0.16, 100, wave=None, noise=1.0, hp=1400, curve=1.8, sr=sr, seed=9), 0, 0.8),
                 (synth(0.08, 190, 150, wave="tri", curve=1.5, sr=sr), 0, 0.5)], sr=sr)
    hat = synth(0.04, 100, wave=None, noise=1.0, hp=6000, curve=2.5, sr=sr, seed=4)
    ohat = synth(0.12, 100, wave=None, noise=1.0, hp=5000, curve=1.6, sr=sr, seed=6)

    rnd = random.Random(seed)
    for s in range(steps):
        t = s * step
        bar_step = s % 16
        chord = chords[(s // 16) // bars_per_chord]
        root = chord[0]
        four_floor = style == "arena"
        if bar_step in ((0, 4, 8, 12) if four_floor else (0, 8, 10)):
            add(kick, t, 0.9)
        if bar_step in (4, 12):
            add(snare, t, 0.55)
        if bar_step % 2 == 0:
            add(hat, t, 0.16)
        if bar_step % 4 == 2:
            add(ohat, t, 0.12)
        if bar_step % 2 == 0:                      # bass on 8ths, octave bounce
            n = root - 24 + (12 if bar_step % 4 == 2 else 0)
            add(synth(step * 1.8, note(n), wave="saw" if four_floor else "tri", curve=1.2,
                      lp=700 if four_floor else 1200, sr=sr), t, 0.42)
        tones = [chord[0], chord[1], chord[2], chord[0] + 12]   # arpeggio on 16ths
        n = tones[bar_step % 4] + (0 if style == "arena" else 12)
        if not (style == "arena" and bar_step in (7, 15)):
            add(synth(step * 1.4, note(n), wave="square", duty=0.25, curve=1.6, lp=3200, sr=sr), t, 0.13)
        if bar_step == 0 and rnd.random() < 0.9:               # soft pad note per bar
            add(synth(step * 15, note(chord[1] + 12), wave="tri", attack=0.05, curve=0.8, lp=1800, sr=sr), t, 0.10)
    return finish(buf, peak=0.8, sr=sr, loop=True)


MUSIC = {
    # C major, I - V - vi - IV
    "music_run": lambda: render_loop(126, [(60, 64, 67), (55, 59, 62), (57, 60, 64), (53, 57, 60)], 2, "run"),
    # A minor, i - VI - III - VII
    "music_arena": lambda: render_loop(138, [(57, 60, 64), (53, 57, 60), (48, 52, 55), (55, 59, 62)], 2, "arena"),
}


if __name__ == "__main__":
    for name, fn in SFX.items():
        buf = finish(fn())
        write(os.path.join(SFX_DIR, name + ".wav"), buf)
        print(f"sfx   {name:12s} {len(buf) / SR:5.2f}s")
    for name, fn in MUSIC.items():
        buf = fn()
        write(os.path.join(MUSIC_DIR, name + ".wav"), buf, sr=22050)
        print(f"music {name:12s} {len(buf) / 22050:5.2f}s")
