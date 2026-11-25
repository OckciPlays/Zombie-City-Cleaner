using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// A rua do Dia 0 no Lakewood (22/11/2026, a etapa 4 do Docs/23-Campanha-e-Mapa.md, "o D0 na Woodcreek"): a Woodcreek de
    /// antes faz o papel da rua do Dia 0 (<see cref="RuaDoDiaZero"/>), sem cena por cima. Este objeto é o palco, posto pelo
    /// <see cref="LakewoodNaCena"/> (o prefab Prefabs/DiaZero/Palco do Dia 0 na Woodcreek, com a introdução e os NPCs)
    /// enquanto o Dia 0 dura:
    ///  - a casa da família é a do lote 104, um sobrado com garagem na Spruce Lane, a ~660 m do bunker (o Pedro escolheu o lado
    ///    norte da vila: a fuga, depois, atravessa a Woodcreek). Os nascimentos (na sala, de frente para a TV), o sofá, a TV, o
    ///    "dentro de casa" e as chaves que acabam o dia são da planta dela (a mesma obra do mapa, nova), presos numa âncora no
    ///    lugar da obra; com a obra montada (perto de alguém), a casa é ela (a TV da mobília nova fica nela);
    ///  - as lojas abrem quando a obra delas entra na cena (<see cref="AbrirALoja"/>; WoodcreekDoDiaZero.Lojas.cs);
    ///  - as pessoas e os carros da vila (WoodcreekDoDiaZero.Npcs.cs e <see cref="CarrosDaWoodcreek"/>);
    ///  - os cenários da introdução ficam no alto, 1,5 km acima da casa, longe do relevo.
    /// </summary>
    public partial class WoodcreekDoDiaZero : RuaDoDiaZero
    {
        /// <summary>O meio do lote da casa da família no mapa (o lote 104, na Spruce Lane; 22/11/2026).</summary>
        public static readonly Vector2 CasaDaFamilia = new Vector2(9921.5f, 6901f);
        /// <summary>A caminhonete fica na entrada da garagem, este tanto para a rua a partir da boca dela (m).</summary>
        const float CaminhoneteNaEntrada = 3.0f;

        /// <summary>O palco do Dia 0 do Lakewood desta cena (null fora dele).</summary>
        public static WoodcreekDoDiaZero Atual => Instancia as WoodcreekDoDiaZero;

        /// <summary>O lote da casa da família (no chão do Lakewood).</summary>
        public ChaoDeLakewood.LoteNoChao LoteDaFamilia { get; private set; }
        /// <summary>
        /// Onde a chave da caminhonete fica (no mundo) e o giro dela: em cima da mesa da sala da nossa casa (23/11/2026, o
        /// Pedro: "está faltando a chave da caminhonete no início do jogo, na nossa casa (deveria ficar na mesa da sala por
        /// exemplo)"). O anfitrião a põe junto com a caminhonete (<see cref="LakewoodNaCena"/>, o PorAChave).
        /// </summary>
        public Vector3 LugarDaChave { get; private set; }
        public float GiroDaChave { get; private set; }
        /// <summary>Em que móvel a chave ficou (para o relatório).</summary>
        public string MovelDaChave { get; private set; }

        /// <summary>Onde a caminhonete da família fica (no mundo) e o giro dela: o nariz para a garagem, a caçamba para a rua.</summary>
        public Vector3 LugarDaCaminhonete { get; private set; }
        public float GiroDaCaminhonete { get; private set; }

        Transform _ancora;
        Vector3 _cenarios;

        protected override string Chave => "woodcreek";
        public override Vector3 LugarDosCenarios => _cenarios;

        protected override void Montar()
        {
            Instancia = this;
            Pronta = false;
            Lojas.Clear();
            Npcs.Clear();
            LugaresDasCompras.Clear();
            NossaCasa = null;
            _abertas.Clear();
            if (!Application.isPlaying) return;   // (o prefab aberto no Editor: nada)
            var lw = LakewoodNaCena.Instancia;
            if (lw == null || lw.ChaoNoMapa == null)
            {
                Debug.LogWarning("[Dia 0] A Woodcreek do Dia 0 sem o Lakewood na cena: nada montado.");
                return;
            }
            // a casa da família: o lote de casa mais perto do ponto escolhido
            ChaoDeLakewood.LoteNoChao lote = null;
            float melhor = float.MaxValue;
            foreach (var l in lw.ChaoNoMapa.Lotes)
            {
                if (l.Plano == null || l.Plano.Uso != UsoDoLote.Casa || l.Plano.Casa == null) continue;
                float d = (l.Lote.meio - CasaDaFamilia).sqrMagnitude;
                if (d < melhor)
                {
                    melhor = d;
                    lote = l;
                }
            }
            if (lote == null)
            {
                Debug.LogWarning("[Dia 0] A Woodcreek não tem a casa da família.");
                return;
            }
            LoteDaFamilia = lote;
            SortearAsLojas(lw.ChaoNoMapa);
            var plano = lote.Plano;
            var p = plano.Casa;
            var M = plano.Medidas ?? Medidas.DoJogo;
            // a planta montada só pelos pontos dos móveis (a obra de verdade é a do mapa: LakewoodNaCena.Obras.cs)
            var casa = new global::ZombieCityCleaner.Mundo.Casa(p, M) { portasVivas = true, nova = true };
            casa.Montar();
            var pontos = new List<PontoDeMovel>();
            foreach (var pm in casa.moveis) pontos.Add(p.espelhar ? pm.Espelhado() : pm);
            MalhaDeObra.Devolver(casa.vis);
            MalhaDeObra.Devolver(casa.col);
            var pose = lw.PoseDaObra(lote);
            var ancora = Novo("Nossa casa (a âncora no lugar da obra)", raiz);
            ancora.transform.SetPositionAndRotation(pose.position, pose.rotation);
            _ancora = ancora.transform;
            ACasaDaFamilia(ancora, p, pontos, M);
            AChaveNaMesa(ancora.transform, p, pontos);
            _cenarios = pose.position + Vector3.up * 1500f;

            // a caminhonete na entrada da garagem (a boca dela, no lote), de nariz para a garagem
            var entrada = plano.EntradaDoCarro ?? new Vector2(0f, -lote.Lote.fundo * 0.5f + 4f);
            var boca = lote.Lote.NoMapa(entrada.x, entrada.y);
            var ponto = boca + lote.Lote.Frente * CaminhoneteNaEntrada;
            LugarDaCaminhonete = OrigemDeLakewood.NoMundo(new Vector3(ponto.x, lw.ChaoNoMapa.Altura(ponto.x, ponto.y) + 0.5f, ponto.y));
            var fundos = lote.Lote.Fundos;
            GiroDaCaminhonete = Mathf.Atan2(fundos.x, fundos.y) * Mathf.Rad2Deg;

            PorNpcsDaWoodcreek(lw);
            Pronta = true;
            resumo.Insert(0, $"A Woodcreek do Dia 0: a casa do lote {lote.Indice} ({plano.Nome}, {lote.Lote.rua}), {Npcs.Count} lugares de NPC, {LugaresDasCompras.Count} lugares no chão da casa.\n");
            Debug.Log($"[Dia 0] O palco da Woodcreek: a casa da família no lote {lote.Indice} ({plano.Nome}, {lote.Lote.rua}, em ({lote.Lote.meio.x:F0}; {lote.Lote.meio.y:F0})), " +
                      $"{Npcs.Count} NPCs, a caminhonete na entrada da garagem, a chave dela em {MovelDaChave}.");
        }

        // a chave da caminhonete: no tampo da mesa da sala do térreo (a de centro; sem ela, a de canto, a da cozinha, outra mesa
        // ou o rack da TV), um pouco fora do meio; sem nenhum, no chão perto do sofá
        void AChaveNaMesa(Transform casa, Planta p, List<PontoDeMovel> pontos)
        {
            string[] mesas = { "MesaDeCentro", "MesaDeCanto", "MesaRedonda", "MesaDeRefeicao", "Mesa", "Rack" };
            PontoDeMovel? achada = null;
            int melhor = int.MaxValue;
            foreach (var pm in pontos)
            {
                if (pm.andar != 0 || pm.tipo == null || pm.altura > 0.01f) continue;
                int k = System.Array.FindIndex(mesas, m => pm.tipo.StartsWith(m));
                if (k < 0) continue;
                int nota = k * 2 + (pm.comodo == "sala" ? 0 : 1);
                if (nota < melhor)
                {
                    melhor = nota;
                    achada = pm;
                }
            }
            if (achada.HasValue)
            {
                var pm = achada.Value;
                // o móvel novo é um conjunto (a mesa, o vaso e a revista em cima: MobiliaNova.Pecas), cada peça no lugar e na
                // escala dela: o tampo é o da peça 0 (o móvel do ponto), e a chave vai no canto dele mais longe das outras
                var pecas = new List<MobiliaNova.Peca>();
                if (MobiliaNova.EhNovo(pm.tipo)) MobiliaNova.Pecas(pm, 0u, pecas);
                var mesa = pecas.Find(q => q.indice == 0);
                Vector3 local;
                float giroDaMesa;
                if (pecas.Count > 0 && mesa.modelo != null)
                {
                    float topo = mesa.meio.y + mesa.tamanho.y;
                    var g = Quaternion.Euler(0f, mesa.giro, 0f);
                    local = mesa.meio;
                    float folgaMelhor = -1f;
                    foreach (float fx in new[] { -0.32f, 0.32f, -0.18f, 0.18f, 0f })
                        foreach (float fz in new[] { 0f, -0.22f, 0.22f })
                        {
                            var c = mesa.meio + g * new Vector3(fx * mesa.tamanho.x, 0f, fz * mesa.tamanho.z);
                            float folga = float.MaxValue;
                            foreach (var q in pecas)
                            {
                                if (q.indice == 0 || q.meio.y < topo - 0.05f) continue;   // (só o que está em cima do tampo)
                                float raio = Mathf.Max(q.tamanho.x, q.tamanho.z) * 0.5f;
                                folga = Mathf.Min(folga, Vector2.Distance(new Vector2(c.x, c.z), new Vector2(q.meio.x, q.meio.z)) - raio);
                            }
                            if (folga > folgaMelhor + 0.01f)
                            {
                                folgaMelhor = folga;
                                local = c;
                            }
                        }
                    local.y = topo + 0.012f;
                    giroDaMesa = mesa.giro;
                }
                else
                {
                    var giro = Quaternion.Euler(0f, pm.giro, 0f);
                    local = pm.centro + giro * new Vector3(pm.tamanho.x * 0.18f, 0f, 0f) + Vector3.up * (pm.altura + pm.tamanho.y + 0.012f);
                    giroDaMesa = pm.giro;
                }
                LugarDaChave = casa.TransformPoint(local);
                GiroDaChave = casa.eulerAngles.y + giroDaMesa + 35f;
                MovelDaChave = $"{pm.tipo} ({pm.comodo})";
            }
            else
            {
                LugarDaChave = OlhoNoSofa - Vector3.up * 1.08f;
                GiroDaChave = casa.eulerAngles.y;
                MovelDaChave = "o chão da sala";
            }
        }

        void LateUpdate()
        {
            if (!Pronta || LoteDaFamilia == null) return;
            AcompanharOLimite();   // (24/11/2026: a quarentena, WoodcreekDoDiaZero.Limite.cs)
            // a casa: a obra de verdade, se ela está na cena (a TV e os móveis são dela); senão, a âncora no mesmo lugar
            var lw = LakewoodNaCena.Instancia;
            var obra = lw != null ? lw.ObraDoLote(LoteDaFamilia.Indice) : null;
            NossaCasa = obra != null ? obra.transform : _ancora;
            Lojas.RemoveAll(l => l == null);
            if (_naFuga) TremularOsFogos();
        }

        // as chaves da casa, no gancho ao lado da porta da frente, por dentro: E acaba o Dia 0 (a parede da frente é a de z0
        // da planta; o gancho vai para o lado da porta com mais parede)
        protected override void PorAPortaDoFim(GameObject go, Planta p, Medidas M)
        {
            float sx = p.espelhar ? -1f : 1f, piso = global::ZombieCityCleaner.Mundo.Casa.PisoDe(p);
            float lado = p.xPortaFrente + 1.3f < p.X1 - 0.4f ? 1f : -1f;
            var chaves = new GameObject("Chaves da casa (o fim do Dia 0)") { hideFlags = HideFlags.DontSave };
            chaves.transform.SetParent(go.transform, false);
            chaves.transform.localPosition = new Vector3(sx * (p.xPortaFrente + lado * 0.95f), piso + 1.45f, p.Z0 + M.esp / 2f + 0.012f);
            chaves.transform.localRotation = Quaternion.identity;   // (o +z: para dentro da casa)
            ChavesDaCasa.Montar(chaves, material);
        }
    }
}
