import sys, numpy as np, scipy.io.wavfile as wf, scipy.signal as sg
from PIL import Image, ImageDraw
arq = sys.argv[1]
sr, x = wf.read(arq); x = x.astype(np.float32) / 32768; m = x.mean(axis=1)
w = sr * 4
print('volume a cada 4 s (dB):', ' '.join(f'{20*np.log10(np.sqrt(np.mean(m[i:i+w]**2))+1e-9):.0f}' for i in range(0, len(m), w)))
f, t, S = sg.spectrogram(m, sr, nperseg=4096, noverlap=3072)
k = f < 5000
S = 10 * np.log10(S[k] + 1e-12); S = np.clip((S - S.max() + 75) / 75, 0, 1)
img = Image.fromarray((255 * (1 - S[::-1])).astype(np.uint8)).resize((1200, 300))
ImageDraw.Draw(img).text((5, 5), arq + ' (0-5 kHz)', fill=0)
img.save(arq.replace('.wav', '_espectro.png'))
