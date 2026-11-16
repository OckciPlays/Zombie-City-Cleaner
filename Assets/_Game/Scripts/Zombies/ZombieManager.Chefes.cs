using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os chefes no mundo (15/11/2026; o comportamento em Zumbi.Chefes.cs, os números em <see cref="AjustesDosChefes"/>):
    /// <list type="bullet">
    /// <item>o nascimento: um chefe por vez, raro (a cada <c>intervaloDoNascimento</c>, uma chance, maior de noite, depois do
    /// <c>primeiroDepoisDe</c>), longe de um jogador e fora da vista, num lugar aberto (sem teto e sem parede em volta do
    /// tamanho dele). Sem ninguém por perto um tempo, ele vai embora. O F1 > Chefes cria qualquer um na frente;</item>
    /// <item>a morte: o Blindado se rasga nos Agregados A e B (um de cada lado do corpo), o Alongado larga o Longo; os novos
    /// já saem atrás de quem matou;</item>
    /// <item>o grito do Condutor (a horda vem, chegam outros de fora da vista, os de perto entram em frenesi; a mente de quem
    /// está perto sente), o golpe dos chefes nos jogadores (a parte do corpo, a fratura, o empurrão) e os sons (de longe: os
    /// chefes se ouvem a mais de 30 m).</item>
    /// </list>
    /// Os chefes ficam no fim da lista dos modelos (depois dos 19 zumbis), sem corpo com física (a morte é a animação).
    /// </summary>
    public partial class ZombieManager
    {
        struct Sucessor
        {
            public float quando;
            public QualChefe qual;
            public Vector3 posicao;
            public float yaw;
            public IAlvoDeZumbi alvo;
        }

        readonly List<Sucessor> _sucessores = new List<Sucessor>();
        readonly List<Zumbi> _chefesParaIr = new List<Zumbi>();
        int[] _modeloDoChefe;
        float _proximoChefe = -1f;

        bool _chefesIncluidos;

        /// <summary>
        /// Os chefes entram no fim da lista dos modelos (no começo da partida, em todas as máquinas, na mesma ordem: a do
        /// <see cref="QualChefe"/>): os prefabs Zumbi_Chefe_&lt;Nome&gt; ficam em Resources/Chefes (montados por Chefes > Montar os
        /// chefes no jogo), e não na cena, porque as montagens dos zumbis refazem a lista da cena. O índice de cada um vem daqui
        /// (<see cref="CriarDoModelo"/>), não do prefab.
        /// </summary>
        void IncluirOsChefes()
        {
            if (_chefesIncluidos) return;
            _chefesIncluidos = true;
            var chefes = new List<Zumbi>(Resources.LoadAll<Zumbi>("Chefes"));
            chefes.RemoveAll(z => z == null || z.tipo != TipoDeZumbi.Chefe);
            if (chefes.Count == 0) return;
            chefes.Sort((a, b) => ((int)a.qualChefe).CompareTo((int)b.qualChefe));
            var lista = new List<Zumbi>(modelos.Length + chefes.Count);
            foreach (var m in modelos)
                if (m == null || m.tipo != TipoDeZumbi.Chefe) lista.Add(m);   // (se a cena já tinha algum, ele sai: vale o de Resources)
            lista.AddRange(chefes);
            if (lista.Count >= 128)
            {
                Debug.LogError($"Zumbis: {lista.Count} modelos com os chefes; o byte da rede leva até 127. Os chefes ficam de fora.");
                return;
            }
            modelos = lista.ToArray();
            _modelosPorTipo.Clear();
            _modeloDoChefe = null;
            _pools = null;
        }

        /// <summary>Uma instância nova do modelo, com o número dele (o dos chefes vem da lista, não do prefab).</summary>
        Zumbi CriarDoModelo(int m)
        {
            var z = Instantiate(modelos[m], ProximoGrupo());
            z.modelo = m;
            return z;
        }

        /// <summary>Quantos chefes vivos (no anfitrião).</summary>
        public int ChefesVivos
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _ativos.Count; i++)
                    if (_ativos[i].EhChefe && !_ativos[i].Morto) n++;
                return n;
            }
        }

        /// <summary>O índice do modelo de um chefe na lista (-1: a cena não tem os chefes; monte com Chefes > Montar os chefes no jogo).</summary>
        int ModeloDoChefe(QualChefe qual)
        {
            if (_modeloDoChefe == null)
            {
                _modeloDoChefe = new int[6];
                for (int i = 0; i < _modeloDoChefe.Length; i++) _modeloDoChefe[i] = -1;
                for (int m = 0; m < modelos.Length; m++)
                    if (modelos[m] != null && modelos[m].tipo == TipoDeZumbi.Chefe && (int)modelos[m].qualChefe < _modeloDoChefe.Length)
                        _modeloDoChefe[(int)modelos[m].qualChefe] = m;
            }
            return _modeloDoChefe[(int)qual];
        }

        /// <summary>Um chefe agora (no anfitrião), virado para <paramref name="yaw"/>; com alvo, já vai atrás dele.</summary>
        public Zumbi NascerChefe(QualChefe qual, Vector3 posicao, float yaw, IAlvoDeZumbi alvo = null)
        {
            if (!IsServer || !IsSpawned) return null;
            int m = ModeloDoChefe(qual);
            if (m < 0)
            {
                Debug.LogWarning($"Sem o modelo do chefe {qual} nesta cena: rode Zombie City Cleaner > Chefes > Montar os chefes no jogo.");
                return null;
            }
            var z = Nascer(posicao, m);
            if (z == null) return null;
            z.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Engajou(z);
            if (alvo != null) z.ComecarPerseguicao(alvo, Time.time);
            return z;
        }

        /// <summary>O F1: um chefe na frente do jogador (mais longe os maiores), olhando para ele.</summary>
        public Zumbi CriarChefePerto(QualChefe qual, Vector3 pes, Vector3 frente, IAlvoDeZumbi quem)
        {
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
            frente.Normalize();
            float dist = qual switch
            {
                QualChefe.Blindado => 15f, QualChefe.Alongado => 17f, QualChefe.Condutor => 17f, QualChefe.Longo => 11f, _ => 9f,
            };
            Vector3 p = default;
            bool achou = false;
            for (float d = dist; d >= 5f && !achou; d -= 3f) achou = Sortear(pes + frente * d, 2f, out p, false);
            if (!achou) return null;
            Vector3 olhando = pes - p;
            return NascerChefe(qual, p, Mathf.Atan2(olhando.x, olhando.z) * Mathf.Rad2Deg, quem);
        }

        /// <summary>A cada quadro, no anfitrião: os que nascem da morte de outro, o chefe que vai embora e o que aparece.</summary>
        void ChefesNoServidor(float agora)
        {
            for (int i = _sucessores.Count - 1; i >= 0; i--)
            {
                var s = _sucessores[i];
                if (agora < s.quando) continue;
                _sucessores.RemoveAt(i);
                Vector3 p = s.posicao;
                if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) p = hit.position;
                NascerChefe(s.qual, p, s.yaw, s.alvo != null && s.alvo.AlvoValido ? s.alvo : null);
            }
            if (QuantosJogadores == 0 || Povoando) return;
            var c = ajustes.chefes;
            int vivos = 0;
            _chefesParaIr.Clear();
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (!z.EhChefe || z.Morto) continue;
                vivos++;
                // sem ninguém por perto um tempo: vai embora (não engajado)
                float perto = float.MaxValue;
                for (int k = 0; k < QuantosJogadores; k++) perto = Mathf.Min(perto, Vector3.Distance(Jogadores[k].pes, z.transform.position));
                if (perto > c.voltaSemNinguem && !z.Engajado)
                {
                    if (z.semNinguemDesde < 0f) z.semNinguemDesde = agora;
                    else if (agora - z.semNinguemDesde > c.tempoSemNinguem) _chefesParaIr.Add(z);
                }
                else z.semNinguemDesde = -1f;
            }
            foreach (var z in _chefesParaIr) Devolver(z);
            if (vivos > 0 || _sucessores.Count > 0)
            {
                // (o intervalo conta de quando o último chefe acabou: senão um novo vinha logo depois de matar ou perder o outro)
                _proximoChefe = Mathf.Max(_proximoChefe, agora + c.intervaloDoNascimento);
                return;
            }
            if (!c.nascerNoMundo) return;
            if (_proximoChefe < 0f) _proximoChefe = agora + c.primeiroDepoisDe;
            if (agora < _proximoChefe) return;
            _proximoChefe = agora + c.intervaloDoNascimento;
            if (Random.value >= (EDia ? c.chanceDeDia : c.chanceDeNoite)) return;
            // os inteiros, mais (os Agregados e o Longo, em geral, saem deles)
            float r = Random.value;
            var qual = r < 0.3f ? QualChefe.Blindado : r < 0.55f ? QualChefe.Alongado : r < 0.8f ? QualChefe.Condutor : r < 0.9f ? QualChefe.Longo
                     : Random.value < 0.5f ? QualChefe.AgregadoA : QualChefe.AgregadoB;
            if (ModeloDoChefe(qual) < 0 || !LugarParaChefe(qual, 45f, 70f, out var lugar, out var olhando)) return;
            NascerChefe(qual, lugar, olhando);
        }

        /// <summary>
        /// Um lugar para o chefe nascer: no NavMesh, entre as distâncias de um jogador sorteado, fora da vista de todos, sem
        /// teto em cima e sem parede em volta do tamanho dele (os grandes não cabem num beco nem embaixo de uma marquise).
        /// </summary>
        bool LugarParaChefe(QualChefe qual, float de, float ate, out Vector3 ponto, out float olhando)
        {
            ponto = default;
            olhando = 0f;
            int m = ModeloDoChefe(qual);
            if (m < 0) return false;
            float altura = Mathf.Max(2f, modelos[m].alturaDoChefe), raio = qual == QualChefe.Longo ? 1.4f : altura * 0.22f;
            ref readonly var j = ref Jogadores[Random.Range(0, QuantosJogadores)];
            for (int t = 0; t < 20; t++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(de, ate);
                var tentativa = j.pes + new Vector3(r.x, 0f, r.y);
                if (!NavMesh.SamplePosition(tentativa, out var hit, 3f, NavMesh.AllAreas) || EmAreaLivre(hit.position)) continue;
                Vector3 p = hit.position;
                if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.up, altura, 1, QueryTriggerInteraction.Ignore)) continue;   // um teto
                if (Physics.CheckCapsule(p + Vector3.up * (raio + 0.4f), p + Vector3.up * Mathf.Max(raio + 0.5f, altura - raio), raio, 1, QueryTriggerInteraction.Ignore)) continue;
                bool perto = false, visto = false;
                for (int k = 0; k < QuantosJogadores; k++)
                {
                    perto |= Vector3.Distance(Jogadores[k].pes, p) < de * 0.8f;
                    visto |= Enxerga(Jogadores[k].olho, p + Vector3.up * Mathf.Min(altura * 0.7f, 6f));
                }
                if (perto || (visto && t < 14)) continue;   // (perto de outro jogador, nunca; à vista, só sem outro lugar)
                ponto = p;
                Vector3 d = j.pes - p;
                olhando = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                return true;
            }
            return false;
        }

        /// <summary>Um chefe morreu (no anfitrião): o Blindado vira os dois Agregados; o Alongado, dois Longos.</summary>
        internal void ChefeMorreu(Zumbi z, IAlvoDeZumbi autor)
        {
            Vector3 p = z.transform.position, lado = z.transform.right, frente = z.transform.forward;
            float yaw = z.transform.eulerAngles.y, agora = Time.time;
            switch (z.qualChefe)
            {
                case QualChefe.Blindado:
                    // o tronco se rasga (quadros 12 a 48 do Dividir): uma metade sai de cada lado
                    _sucessores.Add(new Sucessor { quando = agora + 1.5f, qual = QualChefe.AgregadoA, posicao = p + lado * 2.6f + frente * 0.5f, yaw = yaw + 25f, alvo = autor });
                    _sucessores.Add(new Sucessor { quando = agora + 1.75f, qual = QualChefe.AgregadoB, posicao = p - lado * 2.6f + frente * 0.5f, yaw = yaw - 25f, alvo = autor });
                    break;
                case QualChefe.Alongado:
                    // o que sobra dele se arrasta para fora do corpo caído: dois Longos, um de cada lado (15/11/2026, a pedido)
                    _sucessores.Add(new Sucessor { quando = agora + 2.2f, qual = QualChefe.Longo, posicao = p + frente * 3f + lado * 2f, yaw = yaw + 20f, alvo = autor });
                    _sucessores.Add(new Sucessor { quando = agora + 2.6f, qual = QualChefe.Longo, posicao = p + frente * 3f - lado * 2f, yaw = yaw - 20f, alvo = autor });
                    break;
            }
        }

        void EsquecerOsChefes()
        {
            _sucessores.Clear();
            _hordaDoGrito.Clear();
            _chefesParaIr.Clear();
            _modeloDoChefe = null;
            _proximoChefe = -1f;
        }

        // ------------------------------------------------------------------ o Condutor

        /// <summary>
        /// O grito do Condutor (no anfitrião): os zumbis até o raio vêm (os de perto atrás do alvo dele; os outros vão ver) e os
        /// de perto entram em frenesi; a mente de quem está perto sente. E nasce uma horda de verdade (15/11/2026, a pedido:
        /// "quando gritar, ele vai literalmente spawnar uma horda"; HordaDoGrito).
        /// </summary>
        internal void GritoDoCondutor(Zumbi condutor, IAlvoDeZumbi alvo)
        {
            var c = ajustes.chefes;
            float agora = Time.time;
            Vector3 onde = condutor.transform.position;
            if (alvo != null && !alvo.AlvoValido) alvo = null;   // (caiu ou saiu do jogo no meio do grito)
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (z == condutor || z.Morto || z.Especial) continue;
                float d = Vector3.Distance(z.transform.position, onde);
                if (d > c.raioDoGrito) continue;
                if (alvo != null && d < c.raioDoGrito * 0.6f) z.ChamarPeloGrito(alvo, agora + c.tempoDoChamado);
                else z.Investigar(alvo != null ? alvo.transform.position : onde, agora);
                if (d < c.raioDoFrenesi) z.Enfurecer(agora + c.tempoDoFrenesi, c.fatorDoFrenesi);
            }
            StartCoroutine(HordaDoGrito(alvo, onde));
            for (int k = 0; k < QuantosJogadores; k++)
            {
                var j = Jogadores[k].jogador;
                if (j == null || j.Vida == null) continue;
                float d = Vector3.Distance(Jogadores[k].pes, onde);
                if (d < 40f) j.Vida.AbalarAgora(Mathf.Lerp(8f, 2f, d / 40f), MotivoDaMente.Susto);
            }
            Barulho(onde, null, c.raioDoGrito * 0.5f);
        }

        // os comuns das hordas dos gritos (o máximo vivos ao mesmo tempo: AjustesDosChefes.hordaMaxima)
        readonly List<(Zumbi z, int id, byte geracao)> _hordaDoGrito = new List<(Zumbi, int, byte)>();

        int HordaViva()
        {
            for (int i = _hordaDoGrito.Count - 1; i >= 0; i--)
            {
                var (z, id, geracao) = _hordaDoGrito[i];
                // (as estátuas do sol não contam: senão o limite enchia de pedra)
                if (z == null || z.Morto || z.Petrificado || z.Id != id || z.Geracao != geracao || _porId[id] != z) _hordaDoGrito.RemoveAt(i);
            }
            return _hordaDoGrito.Count;
        }

        int VivosDoTipo(TipoDeZumbi tipo)
        {
            int n = 0;
            for (int i = 0; i < _ativos.Count; i++)
                if (!_ativos[i].Morto && !_ativos[i].Petrificado && _ativos[i].tipo == tipo) n++;
            return n;
        }

        /// <summary>
        /// A horda que nasce no grito do Condutor (no anfitrião, alguns por quadro):
        /// <list type="bullet">
        /// <item>os Brutamontes (de 0 a 2, e nunca mais de 2 vivos no mundo) brotam da terra a 12-25 m do alvo: o aviso;</item>
        /// <item>os Longs (de 1 a 3, e nunca mais de 3 vivos) chegam de fora da vista, a 22-38 m, e começam a cuspir;</item>
        /// <item>os Ocos e os Corredores (de 12 a 20; 30% Corredores) chegam de fora da vista, a 25-45 m, ameaçados (vêm mesmo de
        /// dia) e em frenesi, já atrás do alvo. Das hordas dos gritos, no máximo 40 comuns vivos: o grito seguinte só completa.</item>
        /// </list>
        /// Ninguém nasce a menos de 12 m de um jogador (os Brutamontes) ou de 20 m (os outros).
        /// </summary>
        System.Collections.IEnumerator HordaDoGrito(IAlvoDeZumbi alvo, Vector3 onde)
        {
            var c = ajustes.chefes;
            int comuns = Mathf.Min(Random.Range(c.comunsNoGrito.x, c.comunsNoGrito.y + 1), Mathf.Max(0, c.hordaMaxima - HordaViva()));
            int longs = Mathf.Min(Random.Range(c.longsNoGrito.x, c.longsNoGrito.y + 1), Mathf.Max(0, c.longsNoGrito.y - VivosDoTipo(TipoDeZumbi.Long)));
            // (o enterrado, esperando alguém passar, conta: ele é um dos dois)
            int brutos = Mathf.Min(Random.Range(c.brutamontesNoGrito.x, c.brutamontesNoGrito.y + 1),
                                   Mathf.Max(0, c.brutamontesNoGrito.y - VivosDoTipo(TipoDeZumbi.Brutamonte) - (TemBrutamonteEnterrado ? 1 : 0)));
            for (int b = 0; b < brutos; b++)
            {
                if (!IsSpawned) yield break;
                Vector3 foco = AlvoDaHorda(alvo, onde);
                if (LugarDaHorda(foco, 12f, 25f, 12f, false, true, out var p))
                {
                    // (a vida guardada é a do Brutamonte que voltou para a terra: não passa para o da horda)
                    float guardada = _vidaGuardadaDoBruto;
                    _vidaGuardadaDoBruto = -1f;
                    NascerEspecial(TipoDeZumbi.Brutamonte, p, YawPara(p, foco));
                    _vidaGuardadaDoBruto = guardada;
                }
                yield return null;
            }
            for (int l = 0; l < longs; l++)
            {
                if (!IsSpawned) yield break;
                Vector3 foco = AlvoDaHorda(alvo, onde);
                if (LugarDaHorda(foco, 22f, 38f, 20f, true, false, out var p))
                {
                    var z = NascerEspecial(TipoDeZumbi.Long, p, YawPara(p, foco));
                    if (z != null && alvo != null && alvo.AlvoValido) z.ChamarPeloGrito(alvo, Time.time + c.tempoDoChamado);
                }
                yield return null;
            }
            for (int n = 0; n < comuns; n++)
            {
                if (!IsSpawned) yield break;
                if (!LugarDaHorda(onde, 25f, 45f, 20f, true, false, out var p)) continue;
                float agora = Time.time;
                var z = Random.value < c.fracaoDeCorredoresNoGrito ? NascerEspecial(TipoDeZumbi.Corredor, p, Random.Range(0f, 360f)) : Nascer(p);
                if (z == null) yield break;   // (os Ids acabaram)
                _hordaDoGrito.Add((z, z.Id, z.Geracao));
                z.Enfurecer(agora + c.tempoDoFrenesi, c.fatorDoFrenesi);
                z.MudarNivelNoServidor(Zumbi.Nivel.Medio);
                // (de longe, sem desistir aos 20 m como o Oco, e mesmo de dia: ChamarPeloGrito)
                if (alvo != null && alvo.AlvoValido) z.ChamarPeloGrito(alvo, agora + c.tempoDoChamado);
                else
                {
                    z.Ameacar(agora);
                    z.Investigar(onde, agora);   // (o alvo caiu: vêm ver o grito)
                }
                if (n % 3 == 2) yield return null;
            }
        }

        static Vector3 AlvoDaHorda(IAlvoDeZumbi alvo, Vector3 onde) => alvo != null && alvo.AlvoValido ? alvo.transform.position : onde;

        static float YawPara(Vector3 de, Vector3 para)
        {
            Vector3 d = para - de;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Um lugar para quem nasce no grito: no NavMesh, entre as distâncias do centro, nunca a menos de <paramref name="longeDeTodos"/>
        /// de um jogador; fora da vista, se pedido (sem achar, à vista mesmo); no chão aberto, se pedido (o Brutamonte brota da
        /// terra: sem teto, fora do fogo e do bunker).
        /// </summary>
        bool LugarDaHorda(Vector3 centro, float de, float ate, float longeDeTodos, bool foraDaVista, bool chaoAberto, out Vector3 ponto)
        {
            ponto = default;
            for (int t = 0; t < 16; t++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(de, ate);
                if (!NavMesh.SamplePosition(centro + new Vector3(r.x, 0f, r.y), out var hit, 3f, NavMesh.AllAreas) || EmAreaLivre(hit.position)) continue;
                if (chaoAberto && !LugarDeSair(hit.position)) continue;
                bool perto = false, visto = false;
                for (int k = 0; k < QuantosJogadores; k++)
                {
                    perto |= Vector3.Distance(Jogadores[k].pes, hit.position) < longeDeTodos;
                    if (foraDaVista) visto |= Enxerga(Jogadores[k].olho, hit.position + Vector3.up * 1.2f);
                }
                if (perto || (visto && t < 11)) continue;
                ponto = hit.position;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ o ácido do Longo (a gosma do Long por cima: ZombieManager.Gosma.cs)

        /// <summary>O ácido em cheio (no anfitrião): além de prender (a gosma), o dano na hora e o veneno.</summary>
        void AcidoEmCheio(JogadorRede j, Vector3 ponto, Poca p)
        {
            if (j == null || j.Vida == null) return;
            if (p != null) p.queima[j.OwnerClientId] = Time.time + 1f;   // (a queima da poça começa daqui a 1 s, não junto)
            if (SemDano) return;
            var c = ajustes.chefes;
            j.Vida.Acido(c.danoDoAcido, ponto, c.venenoDoAcido, c.venenoTotalDoAcido);
        }

        /// <summary>Na poça de ácido (no anfitrião, a cada 0,25 s): quem está dentro queima uma vez por segundo.</summary>
        void AcidoNaPoca(Poca p, float agora)
        {
            var c = ajustes.chefes;
            for (int k = 0; k < QuantosJogadores; k++)
            {
                ref readonly var j = ref Jogadores[k];
                if (j.jogador == null || j.jogador.Vida == null) continue;
                Vector3 d = j.pes - p.ponto;
                if (Mathf.Abs(d.y) > 1.6f || new Vector2(d.x, d.z).sqrMagnitude > p.raio * p.raio) continue;
                ulong quem = j.jogador.OwnerClientId;
                if (p.queima.TryGetValue(quem, out float proxima) && agora < proxima) continue;
                p.queima[quem] = agora + 1f;
                if (!SemDano) j.jogador.Vida.QueimarNoAcido(c.queimaNaPoca, c.venenoDoAcido * 0.5f, c.venenoTotalDoAcido * 0.5f);
            }
        }

        /// <summary>O frenesi do Condutor (no anfitrião): a horda em volta dele fica mais rápida e vai atrás do alvo dele.</summary>
        internal void FrenesiNaHorda(Zumbi condutor, float tempo)
        {
            var c = ajustes.chefes;
            float agora = Time.time;
            Vector3 onde = condutor.transform.position;
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (z == condutor || z.Morto || z.Especial) continue;
                if ((z.transform.position - onde).sqrMagnitude > c.raioDoFrenesi * c.raioDoFrenesi) continue;
                z.Enfurecer(agora + tempo, c.fatorDoFrenesi);
            }
        }

        // ------------------------------------------------------------------ o golpe nos jogadores

        /// <summary>
        /// O golpe de um chefe (no anfitrião): no jogador, numa parte do corpo (pelos pesos e pelo lado de onde veio), às vezes
        /// com a fratura, e o empurrão e o tremor na máquina dele; no sobrevivente, o dano. A causa vai para a tela de morte.
        /// </summary>
        internal void GolpearPeloChefe(Zumbi chefe, IAlvoDeZumbi alvo, float dano, string causa, TipoDeGolpe tipo, PartesDoCorpo.Pesos pesos,
                                       float chanceDeFratura, Vector3 empurrao, float tremor)
        {
            if (alvo == null || !alvo.AlvoValido || dano <= 0f) return;
            chefe.AcertouOAlvo(alvo);   // (o relógio do empacado embaixo do telhado: Zumbi.Chefes.cs)
            if (SemDano) return;
            Vector3 de = chefe.transform.position;
            if (alvo is JogadorRede j && j.Vida != null)
            {
                // de onde veio, na altura do peito, do lado do chefe (o sorteio da parte olha a altura do ponto: a mão a 6 m de
                // altura mandava quase tudo para a cabeça)
                Vector3 rumo = de - j.transform.position;
                rumo.y = 0f;
                rumo = rumo.sqrMagnitude > 0.01f ? rumo.normalized : j.transform.forward;
                Vector3 ponto = j.transform.position + Vector3.up * 1.1f + rumo * 0.6f;
                var parte = PartesDoCorpo.SortearParte(pesos, j.transform, ponto);
                var lesao = PartesDoCorpo.EhMembro(parte) && Random.value < chanceDeFratura ? Lesao.Fratura : Lesao.Nenhuma;
                j.Vida.Dano(dano, ponto, causa ?? $"Morto por {chefe.NomeDoChefe}.", tipo, parte, lesao);
                if (empurrao.sqrMagnitude > 0.01f || tremor > 0f) j.EmpurraoDoBicho(empurrao, tremor);
            }
            else alvo.LevarGolpe(dano, de);
        }

        // ------------------------------------------------------------------ em cada máquina: os sons e o tremor

        /// <summary>Um som do chefe (em cada máquina), ouvido de longe (até o alcance; os dos especiais param nos 30 m).</summary>
        internal void SomDoChefe(Zumbi z, string nome, float volume, float alcance)
        {
            var clipe = SomEspecial(nome);
            if (clipe == null) return;
            Vector3 p = z.transform.position + Vector3.up * Mathf.Clamp(z.alturaDoChefe * 0.6f, 1f, 5f);
            if ((p - PontoDeVista()).sqrMagnitude > alcance * alcance) return;
            Efeitos.Som(clipe, p, volume * VolumeDosZumbis, true, alcance);
        }

        /// <summary>O chão tremendo na tela de quem joga aqui (perto do ponto: inteiro no meio, nada no raio).</summary>
        internal void TremerPerto(Vector3 ponto, float raio, float forca)
        {
            var local = JogadorRede.Local;
            if (local == null || !local.TryGetComponent<MovimentoPrimeiraPessoa>(out var movimento) || !movimento.enabled) return;
            float d = Vector3.Distance(local.transform.position, ponto);
            if (d >= raio) return;
            float k = 1f - d / raio;
            movimento.Tremer(forca * k * k);
        }

        /// <summary>O zumbi deste Id e geração nesta máquina (no anfitrião, o de verdade; nos clientes, a cópia), ou null.</summary>
        internal Zumbi PorId(int id, byte geracao)
        {
            if (id < 0 || id >= MaxZumbis) return null;
            var z = _porId[id];
            return z != null && z.Geracao == geracao ? z : null;
        }

        /// <summary>F1 > Chefes (no anfitrião): o chefe vivo mais perto (até o raio), ou null.</summary>
        public Zumbi ChefeMaisPerto(Vector3 ponto, float raio)
        {
            Zumbi melhor = null;
            float menor = raio * raio;
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (!z.EhChefe || z.Morto) continue;
                float d = (z.transform.position - ponto).sqrMagnitude;
                if (d < menor)
                {
                    menor = d;
                    melhor = z;
                }
            }
            return melhor;
        }
    }
}
