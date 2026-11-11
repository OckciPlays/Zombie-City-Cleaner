using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Sobreviventes;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.Zombies;
using A = ZombieCityCleaner.Player.ArmadilhasDoJogador;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// As armadilhas (11/11/2026, a etapa 5a da árvore nova: o ramo Armadilhas e defesas; os números no
    /// Player/ArmadilhasDoJogador.cs). Montadas como a barricada (o kit da mochila, a sombra, o E), na mesma lista da rede;
    /// o valor da construção é o que resta delas (o alarme: os toques; o tapete: os usos; as estacas: a vida; a cerca de
    /// fogo: 0 apagada, ou o fim do fogo no relógio do servidor, ConstrucoesNoMundo.CercaDeFogo.cs). O anfitrião decide tudo
    /// (quem passou, o dano, o desgaste) num tique de 0,1 s; cada cópia vê os efeitos (os sons e as latas balançando, pelo
    /// <see cref="ArmadilhaRpc"/>).
    ///  - o alarme de latas: quem cruza o barbante em pé (o zumbi, o morador, o jogador andando; agachado passa sem tocar)
    ///    faz as latas tocarem: o som (35 m), os zumbis a 12 m vêm ver (o barulho dos tiros, sem autor) e o dono recebe o
    ///    aviso no HUD (até 60 m, com a distância e o lado). Rearma em 4 s; cada toque gasta um, e o último arrebenta o
    ///    barbante (sobra 1 sucata);
    ///  - o tapete de pregos: o zumbi que pisa leva o dano (uma vez por passagem) e fica lento por 4 s; o morador leva o
    ///    dano; o jogador andando ou correndo (o dono também) leva o golpe na perna, e agachado passa; a roda do carro fura
    ///    o pneu. Cada zumbi, morador e pneu gasta um uso; gasto, ele some e sobram uns pregos;
    ///  - as estacas: seguram como a barricada (o colisor e o buraco no NavMesh), e quem encosta na frente (o lado das
    ///    pontas) leva o dano por segundo; a vida cai na metade do ritmo da barricada. O Brutamonte as quebra, mas leva 60.
    /// </summary>
    public partial class ConstrucoesNoMundo
    {
        // ------------------------------------------------------------------ montar e recolher (no anfitrião)

        // o valor da armadilha recém-montada: o que resta do kit (o desgaste dele) no nível de quem monta
        static float ValorDaArmadilha(Item tipo, byte desgaste, int nivel)
        {
            float resto = (100 - Mathf.Clamp(desgaste, 0, 99)) / 100f;
            switch (tipo)
            {
                case Item.AlarmeLatas: return Mathf.Max(1, Mathf.RoundToInt(A.ToquesDe(nivel) * resto));
                case Item.TapetePregos: return Mathf.Max(1, Mathf.RoundToInt(A.UsosDoTapeteDe(nivel) * resto));
                case Item.Estacas: return A.VidaDasEstacasDe(nivel);
                default: return 0f;   // a cerca de fogo: apagada
            }
        }

        /// <summary>
        /// O máximo do valor da armadilha no nível dela (os toques, os usos, a vida, os segundos de fogo); desde a etapa 5b,
        /// também a vida máxima das barricadas (a solta e a de tábuas), com a Fortaleza do dono (o bit) nelas e nas estacas.
        /// </summary>
        public static float MaximoDe(Construcao c) => c.tipo switch
        {
            Item.AlarmeLatas => A.ToquesDe(c.nivel),
            Item.TapetePregos => A.UsosDoTapeteDe(c.nivel),
            Item.Estacas => A.VidaDasEstacasDe(c.nivel) * (Fortaleza(c) ? A.FatorDaFortaleza : 1f),
            Item.CercaFogo => A.SegundosDaCercaDe(c.nivel),
            Item.Barricada => A.VidaDaBarricadaSolta(c.nivel, Fortaleza(c)),
            Item.TabuasBarricada => A.VidaDaBarricadaDeTabuas(Janela(c), Reforcada(c), c.nivel, Fortaleza(c)),
            _ => 1f,
        };

        // o desgaste do kit que volta para a mochila (o que já foi dos usos)
        static int DesgasteDe(Construcao c) => Mathf.Clamp(Mathf.RoundToInt((1f - c.valor / Mathf.Max(1f, MaximoDe(c))) * 100f), 0, 99);

        // no anfitrião, o E numa armadilha: recolher (ou acender a cerca, com fogo na mão)
        void UsarArmadilha(int k, Construcao c, InventarioJogador inventario)
        {
            var quem = inventario.GetComponent<JogadorRede>();
            switch (c.tipo)
            {
                case Item.AlarmeLatas:
                    _lista.RemoveAt(k);
                    if (c.valor > MaximoDe(c) * 0.5f)
                    {
                        Devolver(inventario, c, Item.AlarmeLatas);
                        inventario.AvisarDono($"Recolheu o alarme de latas ({Mathf.CeilToInt(c.valor)} toques)");
                    }
                    else
                    {
                        inventario.EntregarItem(Item.Sucata, 1);
                        inventario.AvisarDono("O barbante estava gasto: sobrou 1 sucata");
                    }
                    break;
                case Item.TapetePregos:
                    _lista.RemoveAt(k);
                    Devolver(inventario, c, Item.TapetePregos);
                    inventario.AvisarDono($"Recolheu o tapete de pregos ({Mathf.CeilToInt(c.valor)} usos)");
                    break;
                case Item.Estacas:
                    _lista.RemoveAt(k);
                    if (c.valor / MaximoDe(c) * 100f >= A.EstacasVoltamCom) inventario.EntregarItem(Item.Estacas, 1);
                    else
                    {
                        inventario.EntregarItem(Item.Madeira, 2);
                        inventario.AvisarDono("As estacas estavam gastas: sobraram 2 madeiras");
                    }
                    break;
                case Item.CercaFogo:
                    if (c.valor > 0f) return;   // acesa: só queima
                    if (quem != null && (quem.TochaAcesa || quem.LampiaoAceso))
                    {
                        AcenderACerca(k, c);
                        return;
                    }
                    _lista.RemoveAt(k);
                    inventario.EntregarItem(Item.CercaFogo, 1);
                    break;
            }
        }

        // o kit volta com os usos que sobraram (o desgaste); sem lugar na mochila, fica no chão
        static void Devolver(InventarioJogador inventario, Construcao c, Item item)
        {
            int desgaste = DesgasteDe(c);
            if (!inventario.Receber(item, -1, desgaste)) ItensNoMundo.Instancia?.Criar(item, c.pos, c.giro, -1, desgaste);
        }

        /// <summary>
        /// Para onde a sombra de montar começa virada, somada ao olhar de quem monta (o MontadorDeConstrucao): as estacas com as
        /// pontas para longe dele (ele fica atrás delas); a cerca de fogo com o rastro para a direita, atravessado na frente
        /// dele; o resto, virado para ele (o barbante e o tapete atravessados).
        /// </summary>
        public static float GiroDaSombra(Item tipo) => tipo == Item.Estacas ? 0f : tipo == Item.CercaFogo ? 90f : 180f;

        /// <summary>A dica da armadilha na mira (o que resta dela e o E).</summary>
        public string DicaDaArmadilha(Construcao c, ArmaJogador quem)
        {
            float max = MaximoDe(c);
            switch (c.tipo)
            {
                case Item.AlarmeLatas:
                    return $"Alarme de latas ({Mathf.CeilToInt(c.valor)} de {max:0} toques)   ·   [E] Recolher" + (c.valor > max * 0.5f ? "" : " (o barbante está gasto: sobra 1 sucata)");
                case Item.TapetePregos:
                    return $"Tapete de pregos ({Mathf.CeilToInt(c.valor)} de {max:0} usos)   ·   [E] Recolher";
                case Item.Estacas:
                    float pct = c.valor / max * 100f;
                    return $"Estacas ({Mathf.CeilToInt(pct)}%)   ·   [E] Desmontar" + (pct >= A.EstacasVoltamCom ? "" : " (sobram 2 madeiras)");
                case Item.CercaFogo:
                    if (c.valor > 0f)
                        return $"Cerca de fogo acesa ({Mathf.CeilToInt(Mathf.Max(0f, c.valor - Agora))} s)" + (ChuvaEm(c.pos) ? "   ·   a chuva está apagando" : "");
                    var j = JogadorRede.Local;
                    if (j != null && (j.TochaAcesa || j.LampiaoAceso)) return "Cerca de fogo   ·   [E] Acender o pavio";
                    return "Cerca de fogo apagada   ·   [E] Recolher   ·   acende com fogo na mão";
            }
            return null;
        }

        // ------------------------------------------------------------------ o anfitrião: quem passa

        // quem pode passar numa armadilha do chão (o zumbi, o morador, o jogador), onde está e onde estava no tique de antes
        struct Corpo
        {
            public int chave;
            public Vector3 pos, antes;
            public Zumbi zumbi;
            public Sobrevivente morador;
            public JogadorRede jogador;
            /// <summary>O jogador andando em pé (não agachado, e se mexendo).</summary>
            public bool emPe;
        }

        const float TiqueDasArmadilhas = 0.1f, TiqueDasEstacas = 0.5f;
        const float PertoDaArmadilha = 3f;   // só quem está a menos disto de uma (m) entra na conta
        float _proximoTiqueDasArmadilhas, _proximoTiqueDasEstacas, _proximaLimpeza;
        readonly List<Corpo> _corpos = new List<Corpo>();
        readonly List<Vector3> _armadilhasDoChao = new List<Vector3>();
        readonly Dictionary<int, (Vector3 pos, float quando)> _ondeEstava = new Dictionary<int, (Vector3, float)>();
        readonly List<int> _velhos = new List<int>();
        // quem estava dentro de cada armadilha no tique de antes (a chave da armadilha e a de quem passa): uma vez por passagem
        HashSet<long> _dentro = new HashSet<long>(), _dentroAgora = new HashSet<long>();
        readonly Dictionary<ushort, float> _rearme = new Dictionary<ushort, float>();

        /// <summary>Só no anfitrião (o teste): quantas vezes as latas tocaram, o último aviso do alarme, os pneus furados e quem pisou nos pregos.</summary>
        public int ToquesDasLatas { get; private set; }
        public string UltimoAvisoDoAlarme { get; private set; }
        public int PneusFurados { get; private set; }
        public int PisadasNosPregos { get; private set; }

        // em cada cópia, a cada quadro: o anfitrião decide; cada um vê a cerca acesa
        void ArmadilhasAgora()
        {
            CercasAgora();   // a linha de fogo (ConstrucoesNoMundo.CercaDeFogo.cs)
            if (!IsServer || !IsSpawned) return;
            float agora = Time.time;
            if (agora < _proximoTiqueDasArmadilhas) return;
            _proximoTiqueDasArmadilhas = agora + TiqueDasArmadilhas;
            ZombieManager.Instancia?.LentosAgora();   // a velocidade de quem pisou nos pregos volta
            _armadilhasDoChao.Clear();
            bool estacas = false, cerca = false;
            foreach (var c in _lista)
            {
                if (c.tipo == Item.AlarmeLatas || c.tipo == Item.TapetePregos) _armadilhasDoChao.Add(c.pos);
                else if (c.tipo == Item.Estacas) estacas = true;
                else if (c.tipo == Item.CercaFogo) cerca = true;
            }
            if (_armadilhasDoChao.Count > 0)
            {
                JuntarCorpos(agora);
                PassosNasArmadilhas(agora);
                RodasNosTapetes(agora);
            }
            (_dentro, _dentroAgora) = (_dentroAgora, _dentro);
            _dentroAgora.Clear();
            if (estacas && agora >= _proximoTiqueDasEstacas)
            {
                _proximoTiqueDasEstacas = agora + TiqueDasEstacas;
                EstacasNoServidor(TiqueDasEstacas);
            }
            if (cerca) CercasNoServidor(agora);
            // de vez em quando, esquece onde estava quem sumiu
            if (agora >= _proximaLimpeza)
            {
                _proximaLimpeza = agora + 10f;
                _velhos.Clear();
                foreach (var kv in _ondeEstava) if (agora - kv.Value.quando > 5f) _velhos.Add(kv.Key);
                foreach (var k in _velhos) _ondeEstava.Remove(k);
            }
        }

        // a chave de quem passa (o objeto, enquanto ele existir)
        static int Chave(Object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);

        bool PertoDeUmaArmadilha(Vector3 p)
        {
            foreach (var a in _armadilhasDoChao)
            {
                float dx = p.x - a.x, dz = p.z - a.z;
                if (dx * dx + dz * dz < PertoDaArmadilha * PertoDaArmadilha && Mathf.Abs(p.y - a.y) < 3f) return true;
            }
            return false;
        }

        // onde ele estava no tique de antes (se foi agora há pouco; senão, onde está), e guarda onde está. Um salto maior
        // que o passo máximo (m) é um teleporte (a cama, o teste), não uma passada: não cruza nada
        Vector3 Antes(int chave, Vector3 pos, float agora, out float dt, float passoMaximo = 2.5f)
        {
            dt = 0f;
            Vector3 antes = pos;
            if (_ondeEstava.TryGetValue(chave, out var v) && agora - v.quando < 0.35f && (pos - v.pos).sqrMagnitude < passoMaximo * passoMaximo)
            {
                antes = v.pos;
                dt = agora - v.quando;
            }
            _ondeEstava[chave] = (pos, agora);
            return antes;
        }

        void JuntarCorpos(float agora)
        {
            _corpos.Clear();
            var zm = ZombieManager.Instancia;
            if (zm != null)
                foreach (var z in zm.Ativos)
                {
                    if (z == null || z.Morto) continue;
                    var p = z.transform.position;
                    if (!PertoDeUmaArmadilha(p)) continue;
                    int chave = Chave(z);
                    _corpos.Add(new Corpo { chave = chave, pos = p, antes = Antes(chave, p, agora, out _), zumbi = z });
                }
            foreach (var s in Sobrevivente.Todos)
            {
                if (s == null || !s.AlvoValido) continue;
                var p = s.transform.position;
                if (!PertoDeUmaArmadilha(p)) continue;
                int chave = Chave(s);
                _corpos.Add(new Corpo { chave = chave, pos = p, antes = Antes(chave, p, agora, out _), morador = s });
            }
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned || j.Vida == null || j.Vida.Caido.Value) continue;
                if (j.Passageiro != null && j.Passageiro.NoCarro) continue;
                var p = j.transform.position;
                if (!PertoDeUmaArmadilha(p)) continue;
                int chave = Chave(j);
                var antes = Antes(chave, p, agora, out float dt);
                Vector3 passo = p - antes;
                passo.y = 0f;
                bool andando = dt > 0f && passo.magnitude / dt > 0.5f;
                _corpos.Add(new Corpo { chave = chave, pos = p, antes = antes, jogador = j, emPe = andando && !j.Agachado.Value });
            }
        }

        // o retângulo da armadilha no chão (no espaço dela: o meio do X e do Z, e a altura em que o pé conta)
        static void ZonaDe(Item tipo, out float meioX, out float meioZ, out float baixo, out float alto)
        {
            if (tipo == Item.AlarmeLatas)
            {
                meioX = A.MeioDoBarbante;
                meioZ = A.PertoDoBarbante;
                baixo = -0.6f;
                alto = 1.0f;
            }
            else
            {
                var b = ItensDasArmadilhas.Montada(Item.TapetePregos)?.bounds ?? new Bounds(new Vector3(0f, 0.05f, 0f), new Vector3(1.2f, 0.1f, 0.35f));
                meioX = b.extents.x + 0.05f;
                meioZ = b.extents.z + 0.08f;
                baixo = -0.5f;
                alto = 0.6f;
            }
        }

        // a chave de quem está numa armadilha
        static long ChaveDentro(ushort id, int chave) => ((long)id << 32) | (uint)chave;

        void PassosNasArmadilhas(float agora)
        {
            for (int k = _lista.Count - 1; k >= 0; k--)
            {
                var c = _lista[k];
                if (c.tipo != Item.AlarmeLatas && c.tipo != Item.TapetePregos) continue;
                ZonaDe(c.tipo, out float mx, out float mz, out float baixo, out float alto);
                var inv = Quaternion.Inverse(Quaternion.Euler(0f, c.giro, 0f));
                bool alarme = c.tipo == Item.AlarmeLatas;
                bool armado = !alarme || !_rearme.TryGetValue(c.id, out float ate) || agora >= ate;
                bool acabou = false;
                foreach (var corpo in _corpos)
                {
                    if ((corpo.pos - c.pos).sqrMagnitude > PertoDaArmadilha * PertoDaArmadilha) continue;
                    // o jogador agachado passa sem tocar; parado, não pisa de novo
                    if (corpo.jogador != null && !corpo.emPe) continue;
                    var a = inv * (corpo.antes - c.pos);
                    var b = inv * (corpo.pos - c.pos);
                    if (b.y < baixo || b.y > alto) continue;
                    if (!CruzaRetangulo(new Vector2(a.x, a.z), new Vector2(b.x, b.z), mx, mz)) continue;
                    long chave = ChaveDentro(c.id, corpo.chave);
                    _dentroAgora.Add(chave);
                    if (_dentro.Contains(chave)) continue;   // já estava: uma vez por passagem
                    if (alarme)
                    {
                        if (!armado) continue;
                        armado = false;
                        acabou = AlarmeTocou(k, ref c, corpo);
                    }
                    else acabou = PisouNoTapete(k, ref c, corpo);
                    if (acabou) break;
                }
                if (!acabou && c.valor != _lista[k].valor) _lista[k] = c;
            }
        }

        // ------------------------------------------------------------------ o alarme de latas

        // as latas tocaram: o som, as latas balançando, o barulho para os zumbis e o aviso do dono (true: o barbante arrebentou)
        bool AlarmeTocou(int k, ref Construcao c, Corpo quem)
        {
            ToquesDasLatas++;
            c.valor -= 1f;
            _rearme[c.id] = Time.time + A.RearmeDoAlarme;
            Vector3 latas = c.pos + Quaternion.Euler(0f, c.giro, 0f) * ItensDasArmadilhas.VazioDasLatas;
            ZombieManager.Barulho(latas, null, A.BarulhoDoAlarme);
            AvisarDoAlarme(c, latas);
            if (c.valor <= 0f)
            {
                _lista.RemoveAt(k);
                ItensNoMundo.Instancia?.Criar(Item.Sucata, c.pos, c.giro, 1);
                ArmadilhaRpc(c.id, EfeitoArrebentou, latas);
                return true;
            }
            ArmadilhaRpc(c.id, EfeitoLatas, latas);
            return false;
        }

        // o dono, se estiver perto (60 m), recebe o aviso: a distância e para que lado
        void AvisarDoAlarme(Construcao c, Vector3 latas)
        {
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned || j.Chave.Value != c.dono || j.Inventario == null) continue;
                Vector3 d = latas - j.transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                // (a Fortaleza do dono, 11/11/2026, a etapa 5b: o aviso chega de qualquer distância)
                if (dist > A.AvisoDoAlarme && !A.FortalezaDe(j)) return;
                var frente = j.transform.forward;
                frente.y = 0f;
                string lado = dist < 2f ? "aqui do seu lado" : $"a {dist:0} m, {Lado(Vector3.SignedAngle(frente, d, Vector3.up))}";
                UltimoAvisoDoAlarme = $"As latas tocaram: {lado}";
                j.Inventario.AvisarDono(UltimoAvisoDoAlarme);
                return;
            }
        }

        static string Lado(float angulo)
        {
            float a = Mathf.Abs(angulo);
            string s = angulo >= 0f ? "à direita" : "à esquerda";
            return a <= 22.5f ? "à frente" : a <= 67.5f ? $"à frente, {s}" : a <= 112.5f ? s : a <= 157.5f ? $"atrás, {s}" : "atrás";
        }

        // ------------------------------------------------------------------ o tapete de pregos

        // pisou no tapete: o dano, a lentidão e o desgaste (true: o tapete acabou)
        bool PisouNoTapete(int k, ref Construcao c, Corpo quem)
        {
            float fator = A.FatorDeDano(c.nivel);
            var dono = DonoNaPartida(c.dono);
            if (quem.zumbi != null)
            {
                var zm = ZombieManager.Instancia;
                zm?.FerirPelaArmadilha(quem.zumbi, A.DanoDoTapete * fator, dono);
                if (!quem.zumbi.Morto) zm?.PisouNosPregos(quem.zumbi, A.SegundosLento);
            }
            else if (quem.morador != null) quem.morador.LevarGolpe(A.DanoDoTapete * fator, c.pos);
            else if (quem.jogador != null)
            {
                // o jogador não gasta o tapete; o do dono com o Armadilheiro 2 não o fere
                if (quem.jogador.Chave.Value == c.dono && A.TapeteSemJogadores(c.nivel)) return false;
                quem.jogador.Vida?.Dano(A.DanoDoTapeteNoJogador, c.pos, "Pisou num tapete de pregos.", TipoDeGolpe.Corte,
                                        Random.value < 0.5f ? ParteDoCorpo.PernaE : ParteDoCorpo.PernaD);
                PisadasNosPregos++;
                ArmadilhaRpc(c.id, EfeitoPregos, quem.pos);
                return false;
            }
            PisadasNosPregos++;
            return GastarOTapete(k, ref c, quem.pos);
        }

        bool GastarOTapete(int k, ref Construcao c, Vector3 onde)
        {
            c.valor -= 1f;
            if (c.valor <= 0f)
            {
                _lista.RemoveAt(k);
                ItensNoMundo.Instancia?.Criar(Item.Pregos, c.pos, c.giro, A.PregosQueSobram);
                ArmadilhaRpc(c.id, EfeitoPregosGastos, onde);
                return true;
            }
            ArmadilhaRpc(c.id, EfeitoPregos, onde);
            return false;
        }

        // as rodas dos carros passando por cima dos tapetes: o pneu fura (e gasta um uso)
        void RodasNosTapetes(float agora)
        {
            foreach (var carro in Carro.Todos)
            {
                if (carro == null || !carro.IsSpawned || carro.Velocidade < 0.3f) continue;
                int rodas = carro.RodasParaOsPregos;
                if (rodas == 0) continue;
                bool perto = false;
                foreach (var c in _lista)
                    if (c.tipo == Item.TapetePregos && (c.pos - carro.transform.position).sqrMagnitude < 12f * 12f) perto = true;
                if (!perto) continue;
                for (int i = 0; i < rodas; i++)
                {
                    Vector3 p = carro.CentroDaRoda(i);
                    int chave = unchecked(Chave(carro) * 8 + i + 1000003);
                    // o carro anda rápido: a roda é conferida mesmo longe (o tique de antes vale até 0,35 s)
                    Vector3 antes = Antes(chave, p, agora, out _, 8f);
                    for (int k = _lista.Count - 1; k >= 0; k--)
                    {
                        var c = _lista[k];
                        if (c.tipo != Item.TapetePregos || (c.pos - p).sqrMagnitude > 8f * 8f) continue;
                        ZonaDe(c.tipo, out float mx, out float mz, out _, out _);
                        var inv = Quaternion.Inverse(Quaternion.Euler(0f, c.giro, 0f));
                        var a = inv * (antes - c.pos);
                        var b = inv * (p - c.pos);
                        if (b.y < -0.4f || b.y > 1.2f) continue;
                        // a largura do pneu
                        if (!CruzaRetangulo(new Vector2(a.x, a.z), new Vector2(b.x, b.z), mx + 0.1f, mz + 0.12f)) continue;
                        long dentro = ChaveDentro(c.id, chave);
                        _dentroAgora.Add(dentro);
                        if (_dentro.Contains(dentro) || !carro.PneuBomNaRoda(i)) continue;
                        carro.FurarNoPrego(i);
                        PneusFurados++;
                        if (!GastarOTapete(k, ref c, p)) _lista[k] = c;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ as estacas

        // a cada meio segundo: quem encosta na frente leva o dano, e a vida cai como a da barricada (na metade do ritmo)
        void EstacasNoServidor(float passo)
        {
            var zm = ZombieManager.Instancia;
            if (zm == null) return;
            for (int k = _lista.Count - 1; k >= 0; k--)
            {
                var c = _lista[k];
                if (c.tipo != Item.Estacas) continue;
                var malha = MalhaDe(c.tipo);
                if (malha == null) continue;
                var b = malha.bounds;
                var inv = Quaternion.Inverse(Quaternion.Euler(0f, c.giro, 0f));
                var dono = DonoNaPartida(c.dono);
                float dano = A.DanoDasEstacas * A.FatorDeDano(c.nivel) * passo;
                foreach (var z in zm.Ativos)
                {
                    if (z == null || z.Morto || !z.Engajado) continue;
                    var local = inv * (z.transform.position - c.pos);
                    if (Mathf.Abs(local.y) > 1.5f) continue;
                    // na frente (do lado das pontas) e encostado
                    var d = new Vector2(Mathf.Max(0f, Mathf.Abs(local.x - b.center.x) - b.extents.x), Mathf.Max(0f, Mathf.Abs(local.z - b.center.z) - b.extents.z));
                    if (d.magnitude < 0.9f && local.z > b.max.z - 0.3f) zm.FerirPelaArmadilha(z, dano, dono);
                }
                int zumbis = ZumbisNa(c);
                if (zumbis == 0) continue;
                BateuNa(c.id);   // (o sossego antes do conserto da Fortaleza, 11/11/2026, a etapa 5b)
                c.valor -= DanoPorZumbi * A.RitmoDoDesgaste * zumbis * passo;
                if (c.valor <= 0f)
                {
                    _lista.RemoveAt(k);
                    ItensNoMundo.Instancia?.Criar(Item.Madeira, c.pos, c.giro, 2);
                    ArmadilhaRpc(c.id, EfeitoQuebrou, c.pos + Vector3.up * 0.5f);
                }
                else _lista[k] = c;
            }
        }

        // o Brutamonte passou por cima delas: quebram (as tábuas já ficaram no chão), e ele leva o dano
        void EstacasQuebradas(Construcao c, Zumbi quem)
        {
            ArmadilhaRpc(c.id, EfeitoQuebrou, c.pos + Vector3.up * 0.5f);
            if (quem != null && !quem.Morto)
                ZombieManager.Instancia?.FerirPelaArmadilha(quem, A.DanoNoBrutamonte * A.FatorDeDano(c.nivel), DonoNaPartida(c.dono));
        }

        // ------------------------------------------------------------------ o que cada cópia vê

        const byte EfeitoLatas = 1, EfeitoArrebentou = 2, EfeitoPregos = 3, EfeitoPregosGastos = 4, EfeitoQuebrou = 5, EfeitoAcendeu = 6;

        [Rpc(SendTo.ClientsAndHost)]
        void ArmadilhaRpc(ushort id, byte efeito, Vector3 onde)
        {
            switch (efeito)
            {
                case EfeitoLatas:
                    Efeitos.Som(SomDasArmadilhas.Latas, onde, 1f, true, A.OuvidoDasLatas);
                    if (_objetos.TryGetValue(id, out var o) && o != null)
                    {
                        var latas = o.GetComponentInChildren<LatasBalancando>();
                        if (latas != null) latas.Tocar();
                    }
                    break;
                case EfeitoArrebentou:
                    Efeitos.Som(SomDasArmadilhas.Latas, onde, 1f, true, A.OuvidoDasLatas);
                    Efeitos.Som(SomDasArmadilhas.Barbante, onde, 0.9f, true, 25f);
                    break;
                case EfeitoPregos:
                    Efeitos.Som(SomDasArmadilhas.Pregos, onde, 0.9f, true, 25f);
                    break;
                case EfeitoPregosGastos:
                    Efeitos.Som(SomDasArmadilhas.Pregos, onde, 0.9f, true, 25f);
                    Efeitos.Som(SomDasArmadilhas.Madeira, onde, 0.5f, true, 20f);
                    break;
                case EfeitoQuebrou:
                    Efeitos.Som(SomDasArmadilhas.Madeira, onde, 1f, true, 35f);
                    break;
                case EfeitoAcendeu:
                    Efeitos.Som(SomDoFogo.Labareda, onde, 1f, true, 50f);
                    break;
            }
        }

        // as partes que cada cópia monta: as latas soltas do alarme (balançam no vazio Latas) e o rastro da cerca de fogo
        void MontarArmadilha(GameObject go, Construcao c)
        {
            var d = ItensDasArmadilhas.Dados;
            if (d == null) return;
            if (c.tipo == Item.AlarmeLatas && d.alarmeSemLatas != null && d.latas != null)
            {
                go.GetComponent<MeshFilter>().sharedMesh = d.alarmeSemLatas;
                var latas = new GameObject("Latas") { layer = go.layer };
                latas.transform.SetParent(go.transform, false);
                latas.transform.localPosition = d.vazioDasLatas;
                latas.AddComponent<MeshFilter>().sharedMesh = d.latas;
                var r = latas.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                latas.AddComponent<LatasBalancando>();
            }
            else if (c.tipo == Item.CercaFogo) MontarORastro(go, c);
        }

        /// <summary>
        /// Só no anfitrião (o teste): as armadilhas montadas (o id, o tipo, o valor e o máximo, o nível e, na cerca, se está
        /// acesa).
        /// </summary>
        public string ResumoDasArmadilhas()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in _lista)
                if (Itens.EhArmadilha(c.tipo))
                    sb.Append(System.FormattableString.Invariant($"[{c.id} {c.tipo}: {(c.tipo == Item.CercaFogo ? (c.valor > 0f ? $"acesa, {c.valor - Agora:F1} s" : "apagada") : $"{c.valor:F1} de {MaximoDe(c):F0}")}, nível {c.nivel}, ({c.pos.x:F1}, {c.pos.z:F1}), giro {c.giro:F0}] "));
            return sb.ToString();
        }

        // o dono, se ele estiver na partida (o XP e a Muralha de fogo)
        static JogadorRede DonoNaPartida(ulong chave)
        {
            if (chave == 0) return null;
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.IsSpawned && j.Chave.Value == chave) return j;
            return null;
        }

        void EsquecerAArmadilha(ushort id)
        {
            _rearme.Remove(id);
            _linhas.Remove(id);
        }

        void EsquecerAsArmadilhas()
        {
            _rearme.Clear();
            _linhas.Clear();
            _dentro.Clear();
            _dentroAgora.Clear();
            _ondeEstava.Clear();
            _corpos.Clear();
        }

        // ------------------------------------------------------------------ a conta do retângulo

        /// <summary>O segmento de a até b (no plano) passa pelo retângulo de -mx a mx e de -mz a mz (Liang–Barsky)?</summary>
        static bool CruzaRetangulo(Vector2 a, Vector2 b, float mx, float mz)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            return Cortar(-d.x, a.x + mx, ref t0, ref t1) && Cortar(d.x, mx - a.x, ref t0, ref t1)
                && Cortar(-d.y, a.y + mz, ref t0, ref t1) && Cortar(d.y, mz - a.y, ref t0, ref t1);
        }

        static bool Cortar(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
            float r = q / p;
            if (p < 0f)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }
    }

    /// <summary>As latas do alarme balançando no barbante quando ele toca (11/11/2026, a etapa 5a): uma oscilação que some.</summary>
    public class LatasBalancando : MonoBehaviour
    {
        float _desde = -99f;
        Quaternion _parado;
        bool _guardou;

        public void Tocar()
        {
            if (!_guardou)
            {
                _parado = transform.localRotation;
                _guardou = true;
            }
            _desde = Time.time;
            enabled = true;
        }

        void Update()
        {
            float t = Time.time - _desde;
            if (!_guardou) return;
            if (t > 2.2f)
            {
                transform.localRotation = _parado;
                enabled = false;
                return;
            }
            float caindo = Mathf.Exp(-t / 0.55f);
            float x = 28f * caindo * Mathf.Sin(2f * Mathf.PI * 2.3f * t);
            float z = 12f * caindo * Mathf.Sin(2f * Mathf.PI * 3.1f * t + 1.1f);
            transform.localRotation = _parado * Quaternion.Euler(x, 0f, z);
        }
    }
}
