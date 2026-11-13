using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Sobreviventes;
using ZombieCityCleaner.Zombies;
using A = ZombieCityCleaner.Player.ArmadilhasDoJogador;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// O tiro na cerca de fogo (12/11/2026, a pedido: "Atirar no tanque, ou no fio de combustível e fazer pegar fogo"; "o
    /// galão estoura, sim, queima jogadores e zumbis perto deles"; "o tiro acende sempre"). Os números no
    /// Player/ArmadilhasDoJogador.cs.
    ///  - O tiro (de qualquer arma de fogo; na espingarda, qualquer chumbo) que pega o galão faz ele estourar: os zumbis a
    ///    menos de <see cref="A.RaioDoEstouro"/> pegam fogo e levam o calor de uma vez, os jogadores e os moradores se
    ///    queimam (mais perto, mais; a parede no meio protege), o combustível espalhado vira uma área de fogo no chão, como a
    ///    do molotov, e a cerca acende pelo pavio (se ainda estava apagada). O galão some (<see cref="BitGalaoEstourou"/>).
    ///    O estouro faz barulho: os zumbis em volta vêm ver.
    ///  - O tiro que bate no chão em cima do rastro (a menos de <see cref="A.TiroNoRastro"/>) acende a cerca ali: o fogo
    ///    corre para os dois lados (o ponto fica no <see cref="Construcao.largura"/>, que a cerca não usa).
    /// Quem atira confere na máquina dele (o galão é um gatilho: o raio do tiro passa por ele, então o colisor é conferido à
    /// parte) e manda ao anfitrião, que decide.
    /// </summary>
    public partial class ConstrucoesNoMundo
    {
        /// <summary>O bit da cerca de fogo no <see cref="Construcao.extra"/>: o galão já estourou (some, e não estoura de novo).</summary>
        public const byte BitGalaoEstourou = 8;

        static bool GalaoEstourou(Construcao c) => (c.extra & BitGalaoEstourou) != 0;

        readonly List<(ushort id, Vector3 ponto, bool galao)> _tirosNasCercas = new List<(ushort, Vector3, bool)>();

        /// <summary>Quantos galões estouraram e quantos tiros acenderam um rastro nesta partida (o teste).</summary>
        public int Estouros { get; private set; }
        public int RastrosAcesosNoTiro { get; private set; }

        // ------------------------------------------------------------------ em quem atira

        /// <summary>
        /// No dono do tiro, a cada raio (cada chumbo): ele pegou o galão de uma cerca de fogo (antes de <paramref name="ate"/>,
        /// a distância até onde a bala parou), ou parou no chão (<paramref name="noChao"/>) em cima do rastro?
        /// </summary>
        public void ConferirTiroNasCercas(Vector3 origem, Vector3 direcao, float ate, Vector3 ponto, bool noChao)
        {
            if (!IsSpawned) return;
            var raio = new Ray(origem, direcao);
            foreach (var c in _lista)
            {
                if (c.tipo != Item.CercaFogo) continue;
                int ja = _tirosNasCercas.FindIndex(t => t.id == c.id);
                if (ja >= 0 && _tirosNasCercas[ja].galao) continue;
                if (!GalaoEstourou(c) && _objetos.TryGetValue(c.id, out var o) && o != null && o.TryGetComponent<BoxCollider>(out var col)
                    && col.Raycast(raio, out var h, ate + 0.05f))
                {
                    // (o galão vale mais que o rastro do mesmo disparo)
                    if (ja >= 0) _tirosNasCercas[ja] = (c.id, h.point, true);
                    else _tirosNasCercas.Add((c.id, h.point, true));
                    continue;
                }
                if (ja < 0 && noChao && !CercaAcesa(c) && NaLinha(LinhaDaCerca(c), 0, AmostrasDaLinha - 1, ponto, A.TiroNoRastro, out var perto))
                    _tirosNasCercas.Add((c.id, perto, false));
            }
        }

        /// <summary>No dono do tiro, no fim do disparo: os acertos nas cercas vão ao anfitrião (um por cerca).</summary>
        public void MandarTirosNasCercas()
        {
            if (_tirosNasCercas.Count == 0) return;
            if (IsSpawned)
                foreach (var t in _tirosNasCercas) TiroNaCercaRpc(t.id, t.ponto, t.galao);
            _tirosNasCercas.Clear();
        }

        // ------------------------------------------------------------------ no anfitrião

        [Rpc(SendTo.Server)]
        void TiroNaCercaRpc(ushort id, Vector3 ponto, bool galao, RpcParams parametros = default)
        {
            var inventario = InventarioDe(NetworkManager, parametros.Receive.SenderClientId);
            var quem = inventario != null ? inventario.GetComponent<JogadorRede>() : null;
            if (quem == null) return;
            TiroNaCerca(id, ponto, galao, quem);
        }

        /// <summary>Só no anfitrião (o teste): o tiro de <paramref name="quem"/> pegou o galão ou o rastro da cerca no ponto.</summary>
        public bool TiroNaCerca(ushort id, Vector3 ponto, bool galao, JogadorRede quem)
        {
            if (!IsServer) return false;
            int k = Indice(id);
            if (k < 0) return false;
            var c = _lista[k];
            if (c.tipo != Item.CercaFogo) return false;
            // (o ponto tem de ser da cerca, e quem atirou, no alcance das armas)
            float perto = A.ComprimentoDaCerca + 1.5f;
            if ((ponto - c.pos).sqrMagnitude > perto * perto) return false;
            if (quem != null && (quem.transform.position - ponto).sqrMagnitude > 350f * 350f) return false;
            if (galao)
            {
                if (GalaoEstourou(c)) return false;
                EstourarOGalao(k, c, quem);
                return true;
            }
            if (CercaAcesa(c)) return false;
            AcenderACerca(k, c, IndiceMaisPerto(LinhaDaCerca(c), ponto));
            RastrosAcesosNoTiro++;
            return true;
        }

        /// <summary>No anfitrião, o F1 e o teste: uma cerca de fogo apagada no chão, no ponto (o rastro sai para +Z do giro).</summary>
        public ushort CercaDeTeste(Vector3 ponto, float giro, ulong dono)
        {
            if (!IsServer || !IsSpawned) return 0;
            if (Physics.Raycast(ponto + Vector3.up * 1.5f, Vector3.down, out var chao, 4f, 1, QueryTriggerInteraction.Ignore)) ponto = chao.point;
            ushort id = _proximoId++;
            _lista.Add(new Construcao { id = id, tipo = Item.CercaFogo, pos = ponto, giro = giro, valor = 0f, dono = dono });
            return id;
        }

        static int IndiceMaisPerto(Vector3[] linha, Vector3 p)
        {
            int melhor = 0;
            float menor = float.MaxValue;
            for (int i = 0; i < linha.Length; i++)
            {
                float d = new Vector2(linha[i].x - p.x, linha[i].z - p.z).sqrMagnitude;
                if (d < menor)
                {
                    menor = d;
                    melhor = i;
                }
            }
            return melhor;
        }

        void EstourarOGalao(int k, Construcao c, JogadorRede quem)
        {
            c.extra |= BitGalaoEstourou;
            _lista[k] = c;
            if (!CercaAcesa(c)) AcenderACerca(k, c);   // (o fogo corre do pavio pelo rastro)
            Estouros++;
            Vector3 centro = c.pos + Vector3.up * 0.25f;
            float raio = A.RaioDoEstouro;
            float queimando = quem != null ? FogoDoJogador.SegundosQueimandoDe(quem) : FogoDoJogador.SegundosQueimando;
            // os zumbis perto pegam fogo e levam o calor de uma vez
            ZombieManager.Instancia?.FogoNaArea(centro, raio, A.EstouroNosZumbis, quem, queimando);
            // os moradores e os jogadores: mais perto, mais (a parede no meio protege)
            foreach (var s in Sobrevivente.Todos)
            {
                if (s == null || !s.AlvoValido) continue;
                Vector3 alvo = s.transform.position + Vector3.up;
                float d = Vector3.Distance(alvo, centro);
                if (d > raio || Escondido(centro, alvo)) continue;
                float dano = Mathf.Lerp(A.EstouroNoMeio, A.EstouroNaBeira, d / raio) * A.EstouroNosMoradores;
                if (quem != null) s.LevarTiro(RegiaoDoSobrevivente.Tronco, dano, alvo, quem, false);
                else s.LevarGolpe(dano, centro);
            }
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned || j.Vida == null) continue;
                Vector3 alvo = j.transform.position + Vector3.up * 0.9f;
                float d = Vector3.Distance(alvo, centro);
                if (d > raio || Escondido(centro, alvo)) continue;
                j.Vida.Dano(Mathf.Lerp(A.EstouroNoMeio, A.EstouroNaBeira, d / raio), centro, "Queimou-se no estouro de um galão.");
            }
            // o barulho: os zumbis em volta vêm ver
            ZombieManager.Barulho(centro, quem, 45f);
            // o combustível espalhado: uma área de fogo no chão, como a do molotov
            Vector3 chao = c.pos;
            if (Physics.Raycast(c.pos + Vector3.up * 0.5f, Vector3.down, out var h, 3f, MascaraDoChao, QueryTriggerInteraction.Ignore)) chao = h.point;
            var a = new AreaDeFogo
            {
                id = _proximaArea++, centro = chao, dono = quem, raio = A.RaioDoFogoDoGalao, resta = A.SegundosDoFogoDoGalao,
                coberta = World.Tempo.SobTeto(chao), muralha = FogoDoJogador.MuralhaDe(quem),
            };
            _areas.Add(a);
            EstouroRpc(centro, a.id, chao, a.raio, a.resta);
        }

        // há uma parede (o cenário) entre o estouro e quem está perto?
        static bool Escondido(Vector3 de, Vector3 ate) => Physics.Linecast(de, ate, MascaraDoChao, QueryTriggerInteraction.Ignore);

        // ------------------------------------------------------------------ o que cada cópia vê

        [Rpc(SendTo.ClientsAndHost)]
        void EstouroRpc(Vector3 centro, int area, Vector3 chao, float raio, float segundos)
        {
            MostrarAreaDeFogo(area, chao, raio, segundos);
            Efeitos.Som(SomDoFogo.Estouro, centro, 1f, true, 140f);
            var go = new GameObject("Estouro do galão");
            go.transform.SetParent(transform, false);
            go.transform.position = centro;
            // a bola de fogo: um jorro de chamas grandes de uma vez, para os lados e para cima
            var bola = Fogo(go.transform, Vector3.zero, materialDoFogo, 2.4f, 0f, true, go.layer);
            var ps = bola.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 120;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.25f);
            // (laranja e meio transparente: as chamas aditivas umas por cima das outras ficavam brancas, como uma nuvem)
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.16f, 0.7f), new Color(1f, 0.32f, 0.06f, 0.55f));
            main.gravityModifier = -0.3f;   // (o ar quente sobe)
            var emissao = ps.emission;
            emissao.rateOverTime = 0f;
            var forma = ps.shape;
            forma.shapeType = ParticleSystemShapeType.Hemisphere;
            forma.radius = 0.35f;
            ps.Emit(70);
            // o clarão: a luz forte que apaga em meio segundo
            var luz = go.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.6f, 0.25f);
            luz.range = 18f;
            luz.intensity = 9f;
            luz.shadows = LightShadows.None;
            go.AddComponent<ClaraoDoEstouro>().luz = luz;
            Destroy(go, 2.5f);
        }
    }

    /// <summary>O clarão do galão que estourou (12/11/2026): a luz forte que apaga.</summary>
    public class ClaraoDoEstouro : MonoBehaviour
    {
        public Light luz;

        void Update()
        {
            if (luz == null) return;
            luz.intensity = Mathf.MoveTowards(luz.intensity, 0f, Time.deltaTime * 14f);
            if (luz.intensity <= 0f) luz.enabled = false;
        }
    }
}
