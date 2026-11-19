using UnityEngine;
using Faixa = ZombieCityCleaner.Combat.SomDePasso.Faixa;
using PassaBaixa = ZombieCityCleaner.Combat.SomDePasso.PassaBaixa;
using Ressonador = ZombieCityCleaner.Combat.SomDePasso.Ressonador;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons da madeira, gerados por código (18/11/2026, o corte com os pinheiros novos: antes o machado na árvore tocava
    /// a batida na parede):
    /// <list type="bullet">
    /// <item>o golpe do machado no tronco: o estalo da lâmina entrando e o "toc" seco da madeira;</item>
    /// <item>os estalos da árvore rachando (gravados: os Madeira_01 a 03 do ambiente, copiados em Resources/Arvore);</item>
    /// <item>o tombo: o baque curto do tronco no chão, os galhos quebrando e as folhas.</item>
    /// </list>
    /// (18/11/2026, a pedido: "o barulho está parecendo uns barulho de peido": o rangido gerado era um zumbido grave em
    /// pulsos, e o tombo tinha um ronco de 48 Hz; agora o rangido é o estalo gravado, e o golpe e o tombo ficaram secos, sem
    /// o grave que zumbe.)
    /// </summary>
    public static class SomDaMadeira
    {
        const int Taxa = 44100;

        static AudioClip[] _golpes, _estalos;
        static AudioClip _tombo;

        /// <summary>Os golpes do machado na madeira (três variações).</summary>
        public static AudioClip[] Golpes
        {
            get
            {
                if (_golpes == null)
                {
                    _golpes = new AudioClip[3];
                    for (int i = 0; i < 3; i++) _golpes[i] = SomGerado.Clip($"Machado na madeira {i + 1}", Golpe(81 + i, 1f + 0.07f * i));
                }
                return _golpes;
            }
        }

        /// <summary>Os estalos da madeira rachando (a árvore começando a cair, os galhos no tombo): os gravados.</summary>
        public static AudioClip[] Estalos
        {
            get
            {
                if (_estalos == null || _estalos.Length == 0)
                {
                    var l = new System.Collections.Generic.List<AudioClip>();
                    for (int i = 1; i <= 3; i++)
                    {
                        var c = Resources.Load<AudioClip>($"Arvore/Estalo_0{i}");
                        if (c != null) l.Add(c);
                    }
                    _estalos = l.ToArray();
                }
                return _estalos;
            }
        }

        /// <summary>A árvore batendo no chão.</summary>
        public static AudioClip Tombo => _tombo != null ? _tombo : _tombo = SomGerado.Clip("Árvore tombando", Queda(95));

        // o machado entrando no tronco: o estalo curto da lâmina, o corpo oco da madeira (três modos graves) e as lascas
        static float[] Golpe(int semente, float tom)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.4f * Taxa)];
            var estalo = new Faixa(3200f * tom, 1.0f);
            var lascas = new Faixa(5200f * tom, 0.8f);
            var oco = new[]
            {
                new Ressonador(420f * tom, 0.035f, 1f),
                new Ressonador(910f * tom, 0.025f, 0.8f),
                new Ressonador(1650f * tom, 0.018f, 0.6f),
            };
            var surdo = new PassaBaixa(2400f, 1);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float golpe = Mathf.Exp(-t / 0.003f);
                float x = Ruido(r) * golpe;
                float corpo = 0f;
                foreach (var m in oco) corpo += m.Passar(x);
                float lasca = lascas.Passar(Ruido(r)) * Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t / 0.05f);
                y[i] = estalo.Passar(x) * 1.3f + surdo.Passar(corpo) * 5f + lasca * 0.4f;
            }
            return Acabar(y, 0.85f);
        }

        // o tombo: um baque curto e seco (sem o ronco grave que zumbia), os galhos quebrando e as folhas chacoalhando
        static float[] Queda(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(1.6f * Taxa)];
            var baque = new PassaBaixa(420f, 2);
            var tronco = new[] { new Ressonador(160f, 0.07f, 1f), new Ressonador(330f, 0.05f, 0.7f) };
            var galhos = new Faixa(2600f, 0.9f);
            var folhas = new Faixa(6000f, 0.5f);
            float estalo = 0f;
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float x = Ruido(r) * Mathf.Exp(-t / 0.012f);
                float corpo = 0f;
                foreach (var m in tronco) corpo += m.Passar(x);
                float grave = baque.Passar(x) * 3f + corpo * 6f;
                // os estalos dos galhos: picos curtos e soltos, mais no começo
                if (r.NextDouble() < 0.0022 * Mathf.Exp(-t / 0.5f)) estalo = 1f;
                estalo *= 0.996f;
                float galho = galhos.Passar(Ruido(r) * estalo) * 1.6f;
                float folha = folhas.Passar(Ruido(r)) * Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t / 0.5f) * 0.35f;
                y[i] = grave + galho + folha;
            }
            return Acabar(y, 0.9f);
        }

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        static float[] Acabar(float[] dados, float pico)
        {
            float max = 1e-6f;
            foreach (var v in dados) max = Mathf.Max(max, Mathf.Abs(v));
            float k = pico / max;
            int fim = Mathf.Min(dados.Length, Taxa / 30);
            for (int i = 0; i < dados.Length; i++)
            {
                float rampa = i >= dados.Length - fim ? (dados.Length - 1 - i) / (float)fim : 1f;
                dados[i] *= k * rampa;
            }
            return dados;
        }
    }
}
