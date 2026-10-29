# O estúdio: a partitura de uma peça (as notas no tempo), a mixagem (cada nota no seu lado, um pouco mandada para o eco),
# o eco do espaço, a finalização (volume e limitador) e os arquivos (wav e mp3).
import os
import subprocess
import numpy as np
import scipy.signal as sg
import scipy.io.wavfile as wf
import instrumentos as I

SR = I.SR


class Peca:
    def __init__(self, nome, bpm, compasso=4):
        self.nome, self.bpm, self.compasso = nome, bpm, compasso
        self.notas = []

    def b(self, tempos):
        return tempos * 60.0 / self.bpm

    def T(self, comp, tempo=0.0):
        return self.b(comp * self.compasso + tempo)

    def add(self, inst, t, d, m, v, lado=0.0, eco=0.4, **ex):
        """Uma nota: instrumento, início (s), duração (s), nota MIDI (ou a frequência, com hz=True; ou a altura do
        coração), volume, lado (-1 esquerda, 1 direita), quanto vai para o eco."""
        if inst == 'piano':
            t += I._rng.normal(0, 0.008)   # a mão não é uma máquina
            v *= 1 + I._rng.uniform(-0.08, 0.08)
        self.notas.append((inst, max(t, 0.0), d, m, v, lado, eco, ex))

    def frase(self, inst, comp, tempo, notas, v, lado=0.0, eco=0.4, ligado=1.0, pedal=0.0, **ex):
        """Uma melodia: [(nota, tempos), ...]; None é pausa. 'pedal' (s) segura cada nota mais um pouco (o piano)."""
        pos = comp * self.compasso + tempo
        for m, dur in notas:
            if m is not None:
                self.add(inst, self.b(pos), self.b(dur) * ligado + pedal, m, v, lado, eco, **ex)
            pos += dur
        return pos

    def acorde(self, inst, t, d, notas, v, abertura=0.5, eco=0.5, **ex):
        k = len(notas)
        for i, m in enumerate(notas):
            lado = 0.0 if k == 1 else -abertura + 2 * abertura * i / (k - 1)
            self.add(inst, t, d, m, v, lado, eco, **ex)


_ecos = {}


def mixar(p, duracao, eco_cfg=(4.5, 2.8, 5.0, 0.04), molhado=0.5, cauda=4.0, seco=1.0):
    """As notas da peça somadas (esquerda, direita), com o eco, sem mexer no volume."""
    n = int((duracao + cauda) * SR)
    seco_e, seco_d = np.zeros(n, np.float32), np.zeros(n, np.float32)
    env_e, env_d = np.zeros(n, np.float32), np.zeros(n, np.float32)
    for inst, t, d, m, v, lado, eco, ex in p.notas:
        f = m if inst in ('coracao',) else (I.hz(m) if m is not None else 100.0)
        if ex.pop('hz', False):
            f = m
        y = I.INSTRUMENTOS[inst](f, d, v, ex)
        i0 = int(t * SR)
        if i0 >= n:
            continue
        lado = float(np.clip(lado, -1, 1))
        ge, gd = np.cos((lado + 1) * np.pi / 4) * 1.4142, np.sin((lado + 1) * np.pi / 4) * 1.4142
        if isinstance(y, tuple):
            e, d_ = y
            m_ = min(len(e), n - i0)
            seco_e[i0:i0 + m_] += e[:m_]
            seco_d[i0:i0 + m_] += d_[:m_]
            env_e[i0:i0 + m_] += e[:m_] * eco
            env_d[i0:i0 + m_] += d_[:m_] * eco
            continue
        m_ = min(len(y), n - i0)
        y = y[:m_].astype(np.float32)
        seco_e[i0:i0 + m_] += y * ge
        seco_d[i0:i0 + m_] += y * gd
        env_e[i0:i0 + m_] += y * ge * eco
        env_d[i0:i0 + m_] += y * gd * eco
    if eco_cfg not in _ecos:
        _ecos[eco_cfg] = I.eco(*eco_cfg)
    ir_e, ir_d = _ecos[eco_cfg]
    molhado_e = sg.oaconvolve(env_e, ir_e.astype(np.float32))[:n] + 0.25 * sg.oaconvolve(env_d, ir_e.astype(np.float32))[:n]
    molhado_d = sg.oaconvolve(env_d, ir_d.astype(np.float32))[:n] + 0.25 * sg.oaconvolve(env_e, ir_d.astype(np.float32))[:n]
    return seco_e * seco + molhado_e * molhado * 2.0, seco_d * seco + molhado_d * molhado * 2.0


def renderizar(p, duracao, eco_cfg=(4.5, 2.8, 5.0, 0.04), molhado=0.5, alvo_db=-20.0, cauda=4.0, saida='.', seco=1.0):
    e, d = mixar(p, duracao, eco_cfg, molhado, cauda, seco)
    return finalizar(p.nome, e, d, alvo_db, saida)


def finalizar(nome, e, d, alvo_db, saida):
    hp = sg.butter(2, 30, btype='high', fs=SR, output='sos')
    e, d = sg.sosfilt(hp, e), sg.sosfilt(hp, d)
    rms = np.sqrt(np.mean((e.astype(np.float64) ** 2 + d.astype(np.float64) ** 2) / 2)) + 1e-12
    g = 10 ** (alvo_db / 20) / rms
    e, d = e * g, d * g

    def limitar(x, joelho=0.55, teto=0.89):
        a = np.abs(x)
        acima = a > joelho
        y = x.copy()
        y[acima] = np.sign(x[acima]) * (joelho + (teto - joelho) * np.tanh((a[acima] - joelho) / (teto - joelho)))
        return y

    e, d = limitar(e), limitar(d)
    # o fim: some devagar (a cauda do eco já está no tempo)
    m = int(2.0 * SR)
    e[-m:] *= np.linspace(1, 0, m) ** 2
    d[-m:] *= np.linspace(1, 0, m) ** 2
    os.makedirs(saida, exist_ok=True)
    wav = os.path.join(saida, nome + '.wav')
    wf.write(wav, SR, (np.stack([e, d], axis=1) * 32767).astype(np.int16))
    mp3 = os.path.join(saida, nome + '.mp3')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', wav, '-b:a', '224k', mp3], check=True)
    pico = max(np.abs(e).max(), np.abs(d).max())
    print(f'{nome}: {len(e) / SR:.0f} s, pico {20 * np.log10(pico):.1f} dB, rms {20 * np.log10(np.sqrt(np.mean(e ** 2))):.1f} dB')
    return wav, mp3
