using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A física da sinuca, num plano: x ao longo da mesa e y na largura (o z da mesa), em metros do tamanho real (antes da
    /// escala da mesa; as medidas são as do <c>mobilia_servico.py</c> da pasta de arte). As 16 bolas (a 0 é a branca, a 8
    /// a preta), o atrito do pano, as batidas entre elas e nas tabelas de borracha e as 6 caçapas: onde a tabela tem o
    /// vão (nos cantos e no meio das tabelas compridas), a bola que passa da borda cai. Passos fixos, sem a física da
    /// Unity; não precisa sair igual em todas as máquinas (só quem joga simula, e os outros recebem as posições).
    /// </summary>
    public sealed class FisicaDaSinuca
    {
        public const int Bolas = 16;
        public const int Branca = 0, Preta = 8;
        public const float Raio = 0.0286f;
        /// <summary>A borda das tabelas de borracha (onde a bola encosta), do meio da mesa: o pano livre é 1,89 × 0,90 m.</summary>
        public const float MeioComprimento = 0.945f, MeioLargura = 0.45f;
        /// <summary>A marca da branca (na cabeceira) e o pé do triângulo (no outro lado), a um quarto da mesa.</summary>
        public const float Marca = 0.47f;
        /// <summary>A velocidade da branca com a tacada mais forte (m/s).</summary>
        public const float VelocidadeMaxima = 4.6f;

        // as tabelas de borracha: nas compridas, de 6 cm do meio (a caçapa do meio) até 86 cm (a do canto); nas curtas,
        // até 37 cm do meio. Fora disso é o vão da caçapa (as do canto largas: com a tabela até 91,5 cm, a bola que vinha
        // na diagonal batia nela antes da boca e voltava). O modelo da mesa (mobilia_servico.py) tem as mesmas medidas
        const float TabelaLongaDe = 0.06f, TabelaLongaAte = 0.86f, TabelaCurtaAte = 0.37f;
        // o pano freia a bola (uma parte fixa e uma que cresce com a velocidade: as tacadas fortes não rolam para sempre)
        const float Atrito = 0.17f, Arrasto = 0.08f, Parou = 0.004f;
        const float RestituicaoBola = 0.95f, RestituicaoTabela = 0.76f;
        const float Passo = 1f / 600f;

        public readonly Vector2[] Pos = new Vector2[Bolas];
        public readonly Vector2[] Vel = new Vector2[Bolas];
        /// <summary>As bolas que já caíram (um bit por bola).</summary>
        public ushort Fora;

        // o que soou desde a última leitura: a batida mais forte entre bolas e numa tabela (0 a 1) e as caçapas
        public float BatidaDeBola, BatidaNaTabela;
        public int Cacapas;
        /// <summary>As bolas que caíram desde a última leitura (um bit por bola).</summary>
        public ushort CairamAgora;

        float _resto;

        public bool Dentro(int i) => (Fora & (1 << i)) == 0;

        public bool Parada
        {
            get
            {
                for (int i = 0; i < Bolas; i++)
                    if (Dentro(i) && Vel[i] != Vector2.zero) return false;
                return true;
            }
        }

        public void Carregar(System.Collections.Generic.IReadOnlyList<Vector2> posicoes, ushort fora)
        {
            for (int i = 0; i < Bolas; i++)
            {
                Pos[i] = i < posicoes.Count ? posicoes[i] : Vector2.zero;
                Vel[i] = Vector2.zero;
            }
            Fora = fora;
            _resto = 0f;
            Limpar();
        }

        public void Limpar()
        {
            BatidaDeBola = BatidaNaTabela = 0f;
            Cacapas = 0;
            CairamAgora = 0;
        }

        /// <summary>Anda o tempo dado (em passos fixos de 1/600 s).</summary>
        public void Simular(float tempo)
        {
            _resto += tempo;
            while (_resto >= Passo)
            {
                Um(Passo);
                _resto -= Passo;
            }
        }

        /// <summary>Até todas pararem (no máximo um minuto de jogo).</summary>
        public void AteParar()
        {
            for (int k = 0; k < 600 * 60 && !Parada; k++) Um(Passo);
        }

        void Um(float dt)
        {
            for (int i = 0; i < Bolas; i++)
            {
                if (!Dentro(i)) continue;
                var v = Vel[i];
                float s = v.magnitude;
                if (s <= 0f) continue;
                float s2 = s - (Atrito + Arrasto * s) * dt;
                Vel[i] = s2 <= Parou ? Vector2.zero : v * (s2 / s);
                Pos[i] += Vel[i] * dt;
            }
            BatidasEntreBolas();
            for (int i = 0; i < Bolas; i++)
                if (Dentro(i)) Tabelas(i);
        }

        void BatidasEntreBolas()
        {
            const float d2 = 4f * Raio * Raio;
            for (int i = 0; i < Bolas; i++)
            {
                if (!Dentro(i)) continue;
                for (int j = i + 1; j < Bolas; j++)
                {
                    if (!Dentro(j)) continue;
                    var d = Pos[j] - Pos[i];
                    float dist2 = d.sqrMagnitude;
                    if (dist2 >= d2 || dist2 < 1e-12f) continue;
                    float dist = Mathf.Sqrt(dist2);
                    var n = d / dist;
                    // separa as duas (meio a meio)
                    var empurra = n * ((2f * Raio - dist) * 0.5f);
                    Pos[i] -= empurra;
                    Pos[j] += empurra;
                    // a batida (massas iguais): a parte da velocidade ao longo da linha dos centros passa de uma para a outra
                    float vn = Vector2.Dot(Vel[i] - Vel[j], n);
                    if (vn <= 0f) continue;
                    float impulso = vn * (1f + RestituicaoBola) * 0.5f;
                    Vel[i] -= n * impulso;
                    Vel[j] += n * impulso;
                    BatidaDeBola = Mathf.Max(BatidaDeBola, Mathf.Clamp01(vn / 3f));
                }
            }
        }

        void Tabelas(int i)
        {
            var p = Pos[i];
            var v = Vel[i];
            float lx = MeioComprimento - Raio, ly = MeioLargura - Raio;
            // as compridas (y): a tabela, ou o vão da caçapa do meio e das do canto
            if (Mathf.Abs(p.y) > ly)
            {
                float ax = Mathf.Abs(p.x);
                if (ax >= TabelaLongaDe && ax <= TabelaLongaAte)
                {
                    float s = Mathf.Sign(p.y);
                    p.y = s * ly;
                    if (v.y * s > 0f)
                    {
                        BatidaNaTabela = Mathf.Max(BatidaNaTabela, Mathf.Clamp01(Mathf.Abs(v.y) / 3f));
                        v.y = -v.y * RestituicaoTabela;
                        v.x *= 0.96f;
                    }
                }
                else if (Mathf.Abs(p.y) > ly + Raio * 0.6f)
                {
                    Cair(i);
                    return;
                }
            }
            // as curtas (x): a tabela, ou o vão das caçapas do canto
            if (Mathf.Abs(p.x) > lx)
            {
                if (Mathf.Abs(p.y) <= TabelaCurtaAte)
                {
                    float s = Mathf.Sign(p.x);
                    p.x = s * lx;
                    if (v.x * s > 0f)
                    {
                        BatidaNaTabela = Mathf.Max(BatidaNaTabela, Mathf.Clamp01(Mathf.Abs(v.x) / 3f));
                        v.x = -v.x * RestituicaoTabela;
                        v.y *= 0.96f;
                    }
                }
                else if (Mathf.Abs(p.x) > lx + Raio * 0.6f)
                {
                    Cair(i);
                    return;
                }
            }
            // por garantia: o que saiu da mesa caiu
            if (Mathf.Abs(p.x) > MeioComprimento + 0.1f || Mathf.Abs(p.y) > MeioLargura + 0.1f)
            {
                Cair(i);
                return;
            }
            Pos[i] = p;
            Vel[i] = v;
        }

        void Cair(int i)
        {
            Fora |= (ushort)(1 << i);
            CairamAgora |= (ushort)(1 << i);
            Vel[i] = Vector2.zero;
            Cacapas++;
        }

        /// <summary>A branca volta para a marca (ou um pouco atrás dela, se outra bola estiver lá).</summary>
        public void VoltarABranca()
        {
            Fora &= unchecked((ushort)~(1 << Branca));
            Vel[Branca] = Vector2.zero;
            var p = new Vector2(-Marca, 0f);
            for (int tentativa = 0; tentativa < 30; tentativa++)
            {
                bool livre = true;
                for (int j = 1; j < Bolas && livre; j++)
                    if (Dentro(j) && (Pos[j] - p).sqrMagnitude < 4.4f * Raio * Raio) livre = false;
                if (livre) break;
                p.x -= Raio * 1.2f;
                if (p.x < -MeioComprimento + Raio) p = new Vector2(-Marca, p.y + Raio * 2.4f);
            }
            Pos[Branca] = p;
        }

        /// <summary>
        /// O jogo arrumado: as 15 no triângulo, com a ponta no pé da mesa e a preta no meio da terceira fileira (as outras
        /// sorteadas), e a branca na marca. Devolve as posições (a máscara das que caíram fica 0).
        /// </summary>
        public static Vector2[] Arrumadas(int semente)
        {
            var pos = new Vector2[Bolas];
            var r = new System.Random(semente);
            var resto = new System.Collections.Generic.List<int>();
            for (int i = 1; i < Bolas; i++)
                if (i != Preta) resto.Add(i);
            for (int i = resto.Count - 1; i > 0; i--)
            {
                int k = r.Next(i + 1);
                (resto[i], resto[k]) = (resto[k], resto[i]);
            }
            float folga = Raio * 2.004f;
            float passoX = folga * 0.8660254f;
            int usado = 0;
            for (int fileira = 0; fileira < 5; fileira++)
                for (int j = 0; j <= fileira; j++)
                {
                    var p = new Vector2(Marca + fileira * passoX, (j - fileira * 0.5f) * folga);
                    int bola = fileira == 2 && j == 1 ? Preta : resto[usado++];
                    pos[bola] = p;
                }
            pos[Branca] = new Vector2(-Marca, 0f);
            return pos;
        }
    }
}
