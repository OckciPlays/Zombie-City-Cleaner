using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O povoamento do mundo gerado e os rastejantes embaixo dos carros (a pedido em 28/10/2026, a etapa 5: "pode liberar
    /// todos os zumbis"; antes, no mundo gerado, nenhum zumbi nascia sozinho). Só no anfitrião, e só com o mundo gerado
    /// (na Demo_Cidade valem as hordas da cena).
    /// <para><b>As células:</b> o mapa é cortado em células de 40 m. Quando o meio de uma chega a 52 m de um jogador (o
    /// NavMesh do mundo vai a 60 m dele) e o NavMesh já chegou ali, ela se povoa uma vez: na cidade, de 1 a 4 zumbis
    /// acordados e, de dia, até 2 estátuas; fora da cidade, às vezes um acordado ou uma estátua; e, com 4 árvores ou
    /// mais na célula (a mata, as praças), de 1 a 3 debaixo das copas. Ninguém nasce a menos de 22 m de um jogador. De dia
    /// e ao amanhecer, os acordados nascem onde o sol não bate (de preferência sob um teto, dentro das casas), e as
    /// estátuas onde ele bate (na rua, já de pedra, paradas no meio de um passo); à noite, os acordados nascem em qualquer
    /// lugar (metade dentro das casas), e estátuas não (elas se soltariam). Essas quantidades vão vezes ×2 de dia e ×3 à
    /// noite (<see cref="DensidadeDeDia"/>, <see cref="DensidadeDeNoite"/>; a etapa 6, a pedido em 28/10/2026: "tem muito
    /// pouco zumbi no mapa", e depois de testar no F1, "de dia 2x e à noite 3x"); ao escurecer, as células do dia ganham
    /// a diferença.</para>
    /// <para><b>Recolher:</b> o zumbi que não está atrás de ninguém, a mais de 130 m de todos os jogadores, volta ao pool,
    /// e a célula longe de todos pode se povoar de novo quando alguém voltar.</para>
    /// <para><b>Os rastejantes:</b> um em cada 10 carros abandonados (sorteado pela semente do mundo e o número do carro)
    /// tem um zumbi sem as pernas embaixo. Quando alguém mexe no carro (abre o capô, apoia ou ergue o macaco, tira uma
    /// roda), ele sai de baixo, do lado de quem mexeu, e agarra: ferido de propósito, ele não larga nem no sol (vira pedra
    /// agarrado). Cada carro, uma vez por partida. No F1, "em todos os carros" (para testar) e "um agora no carro mais
    /// perto".</para>
    /// </summary>
    public partial class ZombieManager
    {
        [Header("O mundo gerado (o povoamento)")]
        [Tooltip("O lado de cada célula do povoamento (m).")]
        public float celulaDoPovoamento = 40f;
        [Tooltip("Uma célula se povoa quando o meio dela chega a esta distância de um jogador (m; o NavMesh vai a 60 m).")]
        public float alcanceDoPovoamento = 52f;
        [Tooltip("Ninguém nasce mais perto que isto de um jogador (m). Quem escolheu ver mais longe (Configurações > Gráficos > Distância de visão) tem mais: até 46 m (UI/Graficos.NascerAPartirDe).")]
        public float longeDoJogador = 22f;
        // a quantidade que o Pedro testou e aprovou (com ×2 de dia e ×3 à noite, DensidadeDeDia e DensidadeDeNoite). Na
        // etapa 6 estes números subiram no código (3-6, 1-3, 25%, 12%), mas a cena aberta no Editor continuou com os de
        // antes, e foi com eles que a quantidade foi testada: voltaram para estes em 29/10/2026. Ao mudar um número aqui,
        // reabra a cena Mundo antes de testar (a cena aberta guarda o valor antigo) e não a salve com o valor velho
        [Tooltip("Zumbis acordados por célula, na cidade (mínimo, máximo).")]
        public Vector2Int acordadosNaCidade = new Vector2Int(1, 4);
        [Tooltip("Estátuas por célula, na cidade, de dia (mínimo, máximo).")]
        public Vector2Int estatuasNaCidade = new Vector2Int(0, 2);
        [Tooltip("Fora das cidades: a chance de um zumbi acordado por célula, e a de uma estátua (de dia).")]
        public float acordadoForaDaCidade = 0.15f, estatuaForaDaCidade = 0.06f;
        [Tooltip("Debaixo das árvores (na mata, nas praças): com pelo menos tantas árvores na célula, ela ganha estes zumbis (mínimo, máximo); de dia, escondidos debaixo das copas.")]
        public int arvoresParaAMata = 4;
        public Vector2Int acordadosNaMata = new Vector2Int(1, 3);
        [Tooltip("Longe assim de todos os jogadores (m), o zumbi que não está atrás de ninguém volta ao pool.")]
        public float distanciaDeRecolher = 130f;
        [Tooltip("Um em quantos carros abandonados tem um rastejante embaixo.")]
        public int umRastejanteEmCadaCarros = 10;

        /// <summary>O mundo gerado se povoa (o F1 desliga, para testar sem ninguém em volta).</summary>
        public readonly NetworkVariable<bool> PovoarOMundo = new NetworkVariable<bool>(true);
        /// <summary>Teste (F1): todo carro abandonado tem um rastejante embaixo.</summary>
        public readonly NetworkVariable<bool> RastejanteEmTodos = new NetworkVariable<bool>(false);
        // quantos nascem em cada célula, vezes isto: de dia ×2 e à noite ×3 (a pedido em 28/10/2026, depois de testar no F1);
        // o F1 ainda muda os dois, para testar
        /// <summary>De dia, multiplica quantos nascem em cada célula (o padrão, ×2; o F1 muda).</summary>
        public readonly NetworkVariable<float> DensidadeDeDia = new NetworkVariable<float>(2f);
        /// <summary>À noite (e ao amanhecer), multiplica quantos nascem em cada célula (o padrão, ×3; o F1 muda).</summary>
        public readonly NetworkVariable<float> DensidadeDeNoite = new NetworkVariable<float>(3f);

        /// <summary>A densidade de agora: a do dia ou a da noite.</summary>
        public float DensidadeAgora => Mathf.Clamp(EDia ? DensidadeDeDia.Value : DensidadeDeNoite.Value, 0f, 6f);

        enum Lugar { Qualquer, Sombra, Sol }

        // as células já povoadas, com a densidade com que foram povoadas (ao escurecer, as do dia ganham o que falta)
        readonly Dictionary<Vector2Int, float> _celulas = new Dictionary<Vector2Int, float>();
        readonly List<(Vector2Int celula, float distancia, float antes)> _candidatas = new List<(Vector2Int, float, float)>();
        readonly List<Vector2Int> _esquecer = new List<Vector2Int>();
        readonly HashSet<int> _sustos = new HashSet<int>();
        float _proximoPovoamento, _proximoRecolher;

        /// <summary>Para conferir (o teste): quantos o povoamento quis pôr e quantos achou lugar.</summary>
        public int PovoamentoQuis { get; private set; }
        public int PovoamentoPos { get; private set; }

        static DadosDoMundo Dados => MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;

        /// <summary>
        /// Uma vez por segundo, no anfitrião: recolhe os de longe e povoa até 2 células, as mais perto primeiro: as novas e as
        /// povoadas com menos do que a densidade de agora (ao escurecer, de ×2 para ×3: ganham a diferença, como se mais
        /// zumbis saíssem à noite). Ao clarear não somem: os que sobram se escondem ou viram pedra.
        /// </summary>
        void PovoarEmVolta(float agora)
        {
            if (agora < _proximoPovoamento) return;
            _proximoPovoamento = agora + 1f;
            var dados = Dados;
            if (dados == null || QuantosJogadores == 0) return;
            Recolher(agora);
            if (!PovoarOMundo.Value) return;
            PovoarAndares(dados);   // os andares dos prédios que carregaram (ZombieManager.Predios.cs)
            float c = celulaDoPovoamento, agoraD = DensidadeAgora;
            _candidatas.Clear();
            for (int k = 0; k < QuantosJogadores; k++)
            {
                Vector3 p = Jogadores[k].pes;
                // quem vê mais longe (UI/Graficos) faz os zumbis nascerem mais longe dele: o alcance da célula cresce junto
                // (até onde o NavMesh, ~60 m, ainda dá lugar), e a célula quase toda dentro da distância de nascer espera
                // ele se afastar (povoada agora, ficaria com poucos para sempre)
                float nascer = Jogadores[k].nascer, alcance = Mathf.Max(alcanceDoPovoamento, nascer + 14f);
                float esperar = nascer > longeDoJogador ? nascer - 12f : 0f;
                int r = Mathf.CeilToInt(alcance / c);
                int cx = Mathf.FloorToInt(p.x / c), cz = Mathf.FloorToInt(p.z / c);
                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var celula = new Vector2Int(cx + dx, cz + dz);
                    float antes = _celulas.TryGetValue(celula, out float ja) ? ja : -1f;
                    if (antes >= agoraD - 0.01f) continue;
                    float d = new Vector2((celula.x + 0.5f) * c - p.x, (celula.y + 0.5f) * c - p.z).magnitude;
                    if (d <= alcance && d >= esperar) _candidatas.Add((celula, d, antes));
                }
            }
            if (_candidatas.Count == 0) return;
            _candidatas.Sort((a, b) => a.distancia.CompareTo(b.distancia));
            int feitas = 0;
            foreach (var (celula, _, antes) in _candidatas)
            {
                if (feitas == 2) break;
                if (_celulas.TryGetValue(celula, out float ja) && ja >= agoraD - 0.01f) continue;   // a mesma célula perto de dois jogadores
                // o NavMesh já chegou ali? (senão tenta de novo no próximo segundo)
                Vector3 meio = new Vector3((celula.x + 0.5f) * c, dados.Altura((celula.x + 0.5f) * c, (celula.y + 0.5f) * c), (celula.y + 0.5f) * c);
                if (!NavMesh.SamplePosition(meio, out _, c * 0.5f, NavMesh.AllAreas)) continue;
                _celulas[celula] = agoraD;
                // nova: a densidade inteira; já povoada (com menos): só a diferença, em outros lugares (outra sorte)
                PovoarCelula(celula, dados, meio, antes < 0f ? agoraD : agoraD - antes, antes < 0f ? 0 : 1 + Mathf.RoundToInt(antes * 10f));
                feitas++;
            }
        }

        void PovoarCelula(Vector2Int celula, DadosDoMundo dados, Vector3 meio, float densidade, int rodada)
        {
            var sorte = new System.Random(Misturar(dados.Semente + rodada * 7919, celula.x, celula.y));
            bool cidade = dados.CidadeEm(meio.x, meio.z) != null;
            int acordados = Vezes(cidade ? sorte.Next(acordadosNaCidade.x, acordadosNaCidade.y + 1) : sorte.NextDouble() < acordadoForaDaCidade ? 1 : 0, densidade, sorte);
            int estatuas = Vezes(cidade ? sorte.Next(estatuasNaCidade.x, estatuasNaCidade.y + 1) : sorte.NextDouble() < estatuaForaDaCidade ? 1 : 0, densidade, sorte);
            if (!EDia) estatuas = 0;   // à noite e ao amanhecer, elas se soltariam
            var onde = ProtegerDoSol ? Lugar.Sombra : Lugar.Qualquer;
            PovoamentoQuis += acordados + estatuas;
            for (int i = 0; i < acordados; i++)
                if (PontoNaCelula(sorte, dados, meio, onde, out var p) && Nascer(p) != null) PovoamentoPos++;
            for (int i = 0; i < estatuas; i++)
                if (PontoNaCelula(sorte, dados, meio, Lugar.Sol, out var p))
                {
                    var z = Nascer(p);
                    if (z == null) continue;
                    z.PetrificarJa();
                    PovoamentoPos++;
                }
            PovoarAMata(sorte, meio, densidade);
        }

        // a mata (e as praças com árvores): com árvores bastantes na célula, uns zumbis debaixo das copas (de dia, é a
        // sombra deles; à noite, eles andam por ali)
        void PovoarAMata(System.Random sorte, Vector3 meio, float densidade)
        {
            var floresta = FlorestaDoMundo.Instancia;
            if (floresta == null) return;
            float m = celulaDoPovoamento * 0.5f;
            int arvores = floresta.ArvoreEm(meio.x - m, meio.z - m, meio.x + m, meio.z + m, sorte, out _, out _);
            if (arvores < arvoresParaAMata) return;
            int quantos = Vezes(sorte.Next(acordadosNaMata.x, acordadosNaMata.y + 1), densidade, sorte);
            PovoamentoQuis += quantos;
            for (int i = 0; i < quantos; i++)
                for (int t = 0; t < 6; t++)
                {
                    floresta.ArvoreEm(meio.x - m, meio.z - m, meio.x + m, meio.z + m, sorte, out var tronco, out float raio);
                    // do lado do tronco, debaixo da copa (sem encostar nele: o tronco tem colisão)
                    float ang = (float)sorte.NextDouble() * Mathf.PI * 2f, dist = Mathf.Lerp(0.7f, Mathf.Max(0.8f, raio * 0.75f), (float)sorte.NextDouble());
                    var tentativa = tronco + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * dist;
                    if (!NavMesh.SamplePosition(tentativa, out var hit, 1.2f, NavMesh.AllAreas)) continue;
                    Vector3 p = hit.position;
                    if (EmAreaLivre(p) || PertoDemaisParaNascer(p)) continue;
                    if (ProtegerDoSol && Exposto(p)) continue;
                    if (Nascer(p) != null) PovoamentoPos++;
                    break;
                }
        }

        // a quantidade vezes a densidade, sorteando o que sobra (1,5 × 3 = 4 ou 5)
        static int Vezes(int n, float densidade, System.Random sorte)
        {
            float v = n * densidade;
            int inteiro = Mathf.FloorToInt(v);
            return inteiro + (sorte.NextDouble() < v - inteiro ? 1 : 0);
        }

        /// <summary>
        /// Teste (F1): os de pé que não estão atrás de ninguém voltam ao pool, e as células se esquecem: tudo em volta se
        /// povoa de novo (com a densidade de agora), a partir de 22 m.
        /// </summary>
        public int RepovoarEmVolta()
        {
            if (!IsServer || !IsSpawned) return 0;
            int n = 0;
            for (int i = _ativos.Count - 1; i >= 0; i--)
            {
                var z = _ativos[i];
                if (z.Morto || z.Engajado) continue;
                Devolver(z);
                n++;
            }
            _celulas.Clear();
            _andaresPovoados.Clear();
            _proximoPovoamento = 0f;
            PovoamentoQuis = PovoamentoPos = 0;
            return n;
        }

        /// <summary>
        /// Um ponto do NavMesh dentro da célula, longe dos jogadores: na sombra (de preferência sob um teto: dentro de uma
        /// casa), no sol, ou em qualquer lugar (à noite, um em cada três ainda procura um teto).
        /// </summary>
        bool PontoNaCelula(System.Random sorte, DadosDoMundo dados, Vector3 meio, Lugar onde, out Vector3 ponto)
        {
            float c = celulaDoPovoamento;
            bool querTeto = onde == Lugar.Sombra || (onde == Lugar.Qualquer && sorte.NextDouble() < 0.5);
            // 28 tentativas (eram 14: perto do jogador e procurando um teto, muitas falhavam e a célula ficava com menos)
            for (int t = 0; t < 28; t++)
            {
                float x = meio.x + ((float)sorte.NextDouble() - 0.5f) * c, z = meio.z + ((float)sorte.NextDouble() - 0.5f) * c;
                var tentativa = new Vector3(x, dados.Altura(x, z), z);
                if (!NavMesh.SamplePosition(tentativa, out var hit, 3f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;
                if (EmAreaLivre(p) || PertoDemaisParaNascer(p)) continue;
                bool teto = t < 20 && querTeto;   // nas últimas tentativas, aceita sem teto
                if (onde == Lugar.Sol && (!Exposto(p) || Coberto(p))) continue;
                if (onde == Lugar.Sombra && Exposto(p)) continue;
                if (teto && !Coberto(p)) continue;
                ponto = p;
                return true;
            }
            ponto = default;
            return false;
        }

        // perto demais de algum jogador para um zumbi nascer: cada um pela distância de visão dele (UI/Graficos.NascerAPartirDe;
        // no mínimo longeDoJogador)
        bool PertoDemaisParaNascer(Vector3 p)
        {
            for (int k = 0; k < QuantosJogadores; k++)
            {
                Vector3 d = Jogadores[k].pes - p;
                d.y = 0f;
                float minimo = Mathf.Max(longeDoJogador, Jogadores[k].nascer);
                if (d.sqrMagnitude < minimo * minimo) return true;
            }
            return false;
        }

        bool PertoDeAlguem(Vector3 p, float distancia2)
        {
            for (int k = 0; k < QuantosJogadores; k++)
            {
                Vector3 d = Jogadores[k].pes - p;
                d.y = 0f;
                if (d.sqrMagnitude < distancia2) return true;
            }
            return false;
        }

        // a cada 2 s: os de longe voltam ao pool, e as células longe de todos podem se povoar de novo
        void Recolher(float agora)
        {
            if (agora < _proximoRecolher) return;
            _proximoRecolher = agora + 2f;
            float r2 = distanciaDeRecolher * distanciaDeRecolher;
            for (int i = _ativos.Count - 1; i >= 0; i--)
            {
                var z = _ativos[i];
                if (z.Morto || z.Engajado || PertoDeAlguem(z.transform.position, r2)) continue;
                Devolver(z);
            }
            float c = celulaDoPovoamento, rc = (distanciaDeRecolher + c) * (distanciaDeRecolher + c);
            _esquecer.Clear();
            foreach (var celula in _celulas.Keys)
                if (!PertoDeAlguem(new Vector3((celula.x + 0.5f) * c, 0f, (celula.y + 0.5f) * c), rc)) _esquecer.Add(celula);
            foreach (var celula in _esquecer) _celulas.Remove(celula);
            EsquecerAndares(rc);
        }

        static int Misturar(int semente, int a, int b)
        {
            unchecked
            {
                uint h = (uint)semente * 2654435761u;
                h ^= (uint)a * 2246822519u + 0x9E3779B9u + (h << 6) + (h >> 2);
                h ^= (uint)b * 3266489917u + 0x9E3779B9u + (h << 6) + (h >> 2);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        // ------------------------------------------------------------------ os rastejantes embaixo dos carros

        /// <summary>Este carro abandonado tem um rastejante embaixo (um em cada 10, pela semente do mundo e o número do carro).</summary>
        public bool TemRastejante(int carro)
        {
            if (carro < 0 || _sustos.Contains(carro)) return false;
            if (RastejanteEmTodos.Value) return true;
            var dados = Dados;
            int semente = dados != null ? dados.Semente : 0;
            return Misturar(semente, carro, 7177) % Mathf.Max(1, umRastejanteEmCadaCarros) == 0;
        }

        /// <summary>
        /// No anfitrião: um jogador mexeu no carro abandonado (abriu o capô, apoiou ou ergueu o macaco, tirou uma roda). Se o
        /// carro tem um rastejante embaixo, ele sai agora, do lado de quem mexeu.
        /// </summary>
        public void MexeramNoCarro(int carro, JogadorRede jogador)
        {
            if (!IsServer || !IsSpawned || jogador == null || !TemRastejante(carro)) return;
            _sustos.Add(carro);
            var objetos = ObjetosDoMundo.Instancia;
            if (objetos != null && objetos.PoseDoCarro(carro, out var pos, out var giro, out _)) SoltarRastejante(pos, giro, jogador);
        }

        /// <summary>Teste (F1): o rastejante sai agora do carro abandonado mais perto do jogador (até 25 m).</summary>
        public bool RastejanteNoCarroMaisPerto(JogadorRede jogador)
        {
            if (!IsServer || !IsSpawned || jogador == null) return false;
            int carro = CarroMaisPerto(jogador.transform.position, 25f, out var pos, out var giro);
            if (carro < 0) return false;
            _sustos.Add(carro);
            return SoltarRastejante(pos, giro, jogador);
        }

        static int CarroMaisPerto(Vector3 p, float raio, out Vector3 posicao, out Quaternion giro)
        {
            posicao = default;
            giro = Quaternion.identity;
            var dados = Dados;
            var objetos = ObjetosDoMundo.Instancia;
            if (dados?.Objetos == null || objetos == null) return -1;
            var min = dados.BlocoDe(p.x - raio, p.z - raio);
            var max = dados.BlocoDe(p.x + raio, p.z + raio);
            int melhor = -1;
            float menor = raio * raio;
            for (int bz = min.y; bz <= max.y; bz++)
            for (int bx = min.x; bx <= max.x; bx++)
            {
                var lista = dados.Objetos.DoBloco(bx, bz);
                if (lista == null) continue;
                foreach (int i in lista)
                {
                    if (!objetos.PoseDoCarro(i, out var pos, out var g, out _)) continue;
                    float d = (pos - p).sqrMagnitude;
                    if (d >= menor) continue;
                    menor = d;
                    melhor = i;
                    posicao = pos;
                    giro = g;
                }
            }
            return melhor;
        }

        /// <summary>
        /// Sai de baixo do carro: na beira dele do lado de quem mexeu (o ponto da caixa do carro mais perto do jogador), já
        /// rastejando (sem as pernas), de frente para o jogador, e agarra.
        /// </summary>
        bool SoltarRastejante(Vector3 carro, Quaternion giro, JogadorRede jogador)
        {
            Vector3 j = jogador.transform.position;
            Vector3 local = Quaternion.Inverse(giro) * (j - carro);
            Vector3 naBeira = new Vector3(Mathf.Clamp(local.x, -0.9f, 0.9f), 0f, Mathf.Clamp(local.z, -2.1f, 2.1f));
            Vector3 beira = carro + giro * naBeira;
            Vector3 fora = j - beira;
            fora.y = 0f;
            if (fora.sqrMagnitude < 0.01f) fora = giro * Vector3.right;
            fora.Normalize();
            if (!NavMesh.SamplePosition(beira + fora * 0.45f, out var hit, 1.2f, NavMesh.AllAreas)
                && !NavMesh.SamplePosition(j - fora * 0.9f, out hit, 1.5f, NavMesh.AllAreas)) return false;
            var z = Nascer(hit.position);
            if (z == null) return false;
            float agora = Time.time;
            z.transform.rotation = Quaternion.LookRotation(fora);
            z.VirarRastejante();
            z.Ferir(agora);   // agarra mesmo no sol (vira pedra agarrado)
            z.ComecarPerseguicao(jogador, agora);
            return true;
        }
    }
}
