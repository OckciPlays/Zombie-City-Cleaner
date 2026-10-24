using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Grava os sons gerados por código em WAV, para ouvir fora do jogo, como se ouve nele: os passos de cada chão
    /// (6 seguidos, andando e correndo), comer da lata, beber da garrafa, largar e o soco.
    /// Vão para `Sons/Previas/` da pasta de arte. Menu: Zombie City Cleaner > Gravar prévias dos sons.
    /// </summary>
    public static class PreviasDosSons
    {
        const string Pasta = @"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Sons\Previas";
        const int Taxa = 44100;

        [MenuItem("Zombie City Cleaner/Gravar prévias dos sons")]
        public static void Gravar()
        {
            Directory.CreateDirectory(Pasta);
            foreach (Superficie s in System.Enum.GetValues(typeof(Superficie)))
                foreach (bool correndo in new[] { false, true })
                {
                    float passo = correndo ? 0.3f : 0.5f;
                    var y = new float[Mathf.RoundToInt((passo * 6 + 0.4f) * Taxa)];
                    for (int k = 0; k < 6; k++)
                    {
                        var p = SomDePasso.Passo(s, correndo, 100 * (int)s + 10 * (correndo ? 1 : 0) + k % 4);
                        Somar(y, p, k * passo, (correndo ? 1f : 0.7f) * (k % 2 == 0 ? 1f : 0.85f));
                    }
                    Wav($"Passos_{s}_{(correndo ? "correndo" : "andando")}.wav", y);
                }
            // nos tempos do clipe Usar (as mordidas e os goles nos quadros 14, 20 e 26)
            Wav("Comer_lata.wav", Juntar(1.6f, (SomDosItens.AbrirLata(21), 0f), (SomDosItens.Mastigar(30), 14 / 24f),
                                         (SomDosItens.Mastigar(31), 20 / 24f), (SomDosItens.Mastigar(32), 26 / 24f)));
            Wav("Beber_garrafa.wav", Juntar(1.6f, (SomDosItens.AbrirTampa(22), 0f), (SomDosItens.Gole(40), 14 / 24f),
                                            (SomDosItens.Gole(41), 20 / 24f), (SomDosItens.Gole(42), 26 / 24f)));
            Wav("Largar.wav", SomDosItens.Largar(23));
            // o soco: no ar, e acertando um zumbi (o vento e, no quadro do golpe, a pancada)
            Wav("Soco_no_ar.wav", Juntar(0.5f, (SomDoSoco.Vento(51), 0f)));
            Wav("Soco_acertando.wav", Juntar(0.6f, (SomDoSoco.Vento(52), 0f), (SomDoSoco.NaCarne(53), SomDoSoco.Impacto)));
            Debug.Log($"Prévias dos sons gravadas em {Pasta}.");
        }

        static void Somar(float[] y, float[] p, float em, float volume)
        {
            int i0 = Mathf.RoundToInt(em * Taxa);
            for (int i = 0; i < p.Length && i0 + i < y.Length; i++) y[i0 + i] += p[i] * volume;
        }

        static float[] Juntar(float duracao, params (float[] dados, float em)[] partes)
        {
            var y = new float[Mathf.RoundToInt(duracao * Taxa)];
            foreach (var (dados, em) in partes) Somar(y, dados, em, 1f);
            return y;
        }

        static void Wav(string nome, float[] y)
        {
            float max = 1e-6f;
            foreach (var v in y) max = Mathf.Max(max, Mathf.Abs(v));
            float k = max > 0.95f ? 0.95f / max : 1f;
            using (var f = new BinaryWriter(File.Create(Path.Combine(Pasta, nome))))
            {
                f.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); f.Write(36 + y.Length * 2);
                f.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); f.Write(16); f.Write((short)1); f.Write((short)1);
                f.Write(Taxa); f.Write(Taxa * 2); f.Write((short)2); f.Write((short)16);
                f.Write(System.Text.Encoding.ASCII.GetBytes("data")); f.Write(y.Length * 2);
                foreach (var v in y) f.Write((short)Mathf.RoundToInt(Mathf.Clamp(v * k, -1f, 1f) * 32767f));
            }
        }
    }
}
