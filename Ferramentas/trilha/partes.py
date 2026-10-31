import sys, numpy as np
import instrumentos as I, estudio, compor
nome = sys.argv[1]
capt = {}
def falso(p, duracao, **kw):
    n = int((duracao + 4) * I.SR)
    soma = {}
    for inst, t, d, m, v, lado, eco, ex in p.notas:
        f = m if inst == 'coracao' else (I.hz(m) if m is not None else 100.0)
        y = I.INSTRUMENTOS[inst](f, d, v, dict(ex))
        if isinstance(y, tuple): y = (y[0] + y[1]) / 2
        i0 = int(t * I.SR); mm = min(len(y), n - i0)
        if mm <= 0: continue
        soma.setdefault(inst, np.zeros(n, np.float32))[i0:i0 + mm] += y[:mm]
    tudo = sum(soma.values())
    ref = np.sqrt(np.mean(tudo ** 2))
    for k, s in sorted(soma.items(), key=lambda a: -np.sqrt(np.mean(a[1] ** 2))):
        ativo = np.abs(s) > 1e-4
        rms_ativo = np.sqrt(np.mean(s[ativo] ** 2)) if ativo.any() else 0
        print(f'{k:10s} rms total {20*np.log10(np.sqrt(np.mean(s**2))/ref+1e-12):6.1f} dB   quando toca {20*np.log10(rms_ativo/ref+1e-12):6.1f} dB')
    return None, None
compor.renderizar = falso
compor.PECAS[nome]()
