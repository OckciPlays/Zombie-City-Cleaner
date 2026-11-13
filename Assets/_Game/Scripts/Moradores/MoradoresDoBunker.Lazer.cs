using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Salvar;
using ZombieCityCleaner.World;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.Moradores
{
    /// <summary>
    /// O lazer dos moradores no anfitrião (a parte 8 do Plano_NPCs_Bunker.md da pasta de arte, a segunda fatia):
    ///  - <b>o cigarro</b>: o fumante com a vontade cheia fuma onde está (em pé, no sofá, na cadeira ou no banco; fora da
    ///    cozinha e da enfermaria), com o maço das vagas e as animações do jogador (acender, as tragadas e o peteleco). Sem
    ///    maço, ele vai buscar um (na mesinha do fumo, ou onde houver); sem cigarro há mais de um dia, fica triste. Perto do
    ///    cinzeiro da mesinha, a bituca vai nele (ele vai enchendo; cheio, alguém esvazia);
    ///  - <b>a sinuca</b>: ele pega um taco no suporte (e a caixa de bolas, se a mesa está sem elas), põe as bolas e joga de
    ///    verdade (a física da mesa, pela rede, como a do jogador): sozinho ele treina, a dois cada um na sua vez (quem
    ///    encaçapa joga de novo). Um jogador na mesa, ele vai fazer outra coisa. No fim, as bolas voltam para a caixa e o
    ///    taco e a caixa voltam ao suporte;
    ///  - <b>a TV e o rádio</b>: os sofás de frente para a TV da sala de estar (a tela acende) e a cadeira da estação de rádio
    ///    das operações (o rádio chia); <b>dobrar a roupa</b>, um afazer, na mesa da lavanderia e da rouparia.
    /// </summary>
    public partial class MoradoresDoBunker
    {
        // ------------------------------------------------------------------ o cigarro

        public enum EstadoDoFumo : byte { Nada, Acendendo, Aceso, Jogando }

        /// <summary>A vontade de fumar cheia: ele fuma.</summary>
        const float VontadeCheia = 99.5f;
        /// <summary>As tragadas de um cigarro e o tempo entre elas (s; o cigarro dura uns 60 s, como o do jogador).</summary>
        const int TragadasPorCigarro = FumoJogador.TragadasPorCigarro;
        const float EntreTragadas = 8f, DuracaoDoCigarro = 70f;
        /// <summary>Até onde do cinzeiro a bituca vai nele (m, no chão do andar).</summary>
        const float PertoDoCinzeiroM = 1.7f;

        static bool Fumante(Morador m) => m.ficha != null && m.ficha.Tem(ListasDosMoradores.Fumante);

        static int _cinzeiro = -2, _suporte = -2;

        /// <summary>O cinzeiro da mesinha do fumo (o índice em <see cref="MoveisDoBunker.Todos"/>; -1: não tem).</summary>
        static int Cinzeiro => _cinzeiro != -2 ? _cinzeiro : (_cinzeiro = System.Array.FindIndex(MoveisDoBunker.Todos, x => x.modelo == "Cinzeiro"));
        /// <summary>O suporte dos tacos (a estante dele).</summary>
        static int Suporte => _suporte != -2 ? _suporte : (_suporte = System.Array.FindIndex(MoveisDoBunker.Todos, x => x.modelo == "SuporteDeTacos"));

        bool PertoDoCinzeiro(Morador m)
        {
            int c = Cinzeiro;
            if (c < 0 || m.vida.andar != MoveisDoBunker.Todos[c].andar) return false;
            return Plano(m.vida.pos - MoveisDoBunker.Todos[c].lugar) < PertoDoCinzeiroM;
        }

        // ele pode fumar aqui: parado no lugar, no lazer ou à toa, em pé, no sofá, na cadeira ou no banco (com as mãos
        // livres), fora da cozinha e da enfermaria
        static bool PodeFumarAqui(Morador m)
        {
            var v = m.vida;
            if (!v.noLugar || v.lugar < 0 || v.crise != 0) return false;
            var at = v.Atividade;
            if (at != AtividadeDoMorador.Lazer && at != AtividadeDoMorador.Nada && at != AtividadeDoMorador.Trabalho) return false;
            var l = LugaresDoBunker.Todos[v.lugar];
            if (l.comodo == "cozinha" || l.comodo == "enfermaria" || l.comodo == "isolamento") return false;
            var acao = AcoesDoCorpo.Acao(v.acao);
            return acao == AcaoDoCorpo.Andando || acao == AcaoDoCorpo.Sofa || acao == AcaoDoCorpo.Cadeira || acao == AcaoDoCorpo.Triste
                   || acao == AcaoDoCorpo.Banco;
        }

        // fumando, ele continua: andando para outro lugar (a camada de cima vai junto) ou parado onde pode fumar
        static bool PodeContinuarFumando(Morador m)
        {
            var v = m.vida;
            var at = v.Atividade;
            if (v.crise != 0 || (at != AtividadeDoMorador.Lazer && at != AtividadeDoMorador.Nada && at != AtividadeDoMorador.Trabalho)) return false;
            return !v.noLugar || PodeFumarAqui(m);
        }

        // a cada quadro: a vontade cheia acende (com o maço nas vagas), as tragadas, o fim (o peteleco ou o cinzeiro)
        void Fumar(Morador m, float agora)
        {
            if (!Fumante(m)) return;
            var v = m.vida;
            switch (m.fumo)
            {
                case EstadoDoFumo.Nada:
                    if (v.fumo < VontadeCheia) return;
                    if (v.fumoCheioDesde < 0f) v.fumoCheioDesde = agora;
                    // sem cigarro há mais de um dia
                    if (agora - v.fumoCheioDesde > 24f) HumorDoMorador.PorEnquanto(v, "SemCigarro", -12f, agora);
                    if (Vaga(m, Itens.EhFumo) < 0) return;
                    if (PodeFumarAqui(m)) Acender(m, agora);
                    else PausaParaFumar(m, agora);
                    return;
                case EstadoDoFumo.Acendendo:
                    if (m.corpo == null || Time.time - m.fumoDesde >= FumoJogador.TempoDeAcender)
                    {
                        m.fumo = EstadoDoFumo.Aceso;
                        m.fumoDesde = Time.time;
                        m.tragadas = 0;
                        m.proximaTragada = Time.time + 2f + (float)m.acaso.NextDouble() * 2f;
                        MaoNoCorpo(m);
                    }
                    break;
                case EstadoDoFumo.Aceso:
                {
                    bool acabou = m.corpo == null || m.tragadas >= TragadasPorCigarro || agora >= m.fumoAte || Time.time - m.fumoDesde > DuracaoDoCigarro;
                    bool tragando = Time.time - m.ultimaTragada < FumoJogador.TempoDeTragar;
                    if (!acabou && !tragando && Time.time >= m.proximaTragada)
                    {
                        m.tragadas++;
                        m.ultimaTragada = Time.time;
                        m.proximaTragada = Time.time + EntreTragadas + (float)m.acaso.NextDouble() * 4f;
                        if (m.corpo != null && m.corpo.IsSpawned) m.corpo.Tragadas.Value++;
                    }
                    if (m.corpo != null && m.corpo.IsSpawned)
                    {
                        float usado = Mathf.Max((Time.time - m.fumoDesde) / DuracaoDoCigarro, m.tragadas / (float)TragadasPorCigarro);
                        byte s = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - usado) * 255f);
                        if (m.corpo.Sobra.Value != s) m.corpo.Sobra.Value = s;
                    }
                    if (acabou && !tragando) JogarFora(m, agora);
                    break;
                }
                case EstadoDoFumo.Jogando:
                    if (m.corpo == null || Time.time - m.fumoDesde >= FumoJogador.TempoDoPeteleco)
                    {
                        m.fumo = EstadoDoFumo.Nada;
                        MaoNoCorpo(m);
                    }
                    return;
            }
            // interrompido (foi dormir, comer, buscar na estante, teve uma crise): o cigarro aceso vai fora; o que acendia volta
            if (m.fumo != EstadoDoFumo.Nada && !PodeContinuarFumando(m))
            {
                if (m.fumo == EstadoDoFumo.Aceso) JogarFora(m, agora);
                else
                {
                    m.fumo = EstadoDoFumo.Nada;
                    MaoNoCorpo(m);
                }
            }
        }

        // nos afazeres (com as mãos ocupadas), a vontade cheia: uma pausa para o cigarro, em pé num lugar do mesmo andar onde
        // se pode fumar (fora da cozinha e da enfermaria), e depois ele volta à agenda
        void PausaParaFumar(Morador m, float agora)
        {
            var v = m.vida;
            if (!v.noLugar || v.crise != 0 || v.Atividade != AtividadeDoMorador.Trabalho || agora < m.semPausaAte) return;
            m.semPausaAte = agora + 1f;
            var lugares = new List<int>();
            foreach (int k in LugaresDoBunker.DoTipo(TipoDoLugar.EmPe))
            {
                var l = LugaresDoBunker.Todos[k];
                if (l.andar != v.andar || !Livre(k, m) || l.comodo == "cozinha" || l.comodo == "enfermaria" || l.comodo == "isolamento") continue;
                lugares.Add(k);
            }
            if (lugares.Count == 0) return;
            // o mais perto (os em pé do cômodo dele primeiro)
            lugares.Sort((a, b) => Plano(LugaresDoBunker.Todos[a].raiz - v.pos).CompareTo(Plano(LugaresDoBunker.Todos[b].raiz - v.pos)));
            int lugar = lugares[0];
            Mudar(m, AtividadeDoMorador.Lazer, lugar, LugaresDoBunker.Todos[lugar].Acao, agora + 0.5f, agora);
        }

        void Acender(Morador m, float agora)
        {
            m.fumo = EstadoDoFumo.Acendendo;
            m.fumoDesde = Time.time;
            m.fumoAte = agora + 1f;
            if (m.corpo != null && m.corpo.IsSpawned) m.corpo.Sobra.Value = 255;
            MaoNoCorpo(m);
        }

        // o cigarro acabou: ele sai do maço, a vontade zera e ele fica contente; a bituca no cinzeiro (perto dele) ou no chão
        void JogarFora(Morador m, float agora)
        {
            var v = m.vida;
            int c = Vaga(m, Itens.EhFumo);
            if (c >= 0)
            {
                Gastar(m, c);
                if (Vaga(m, Itens.EhFumo) < 0) Registrar($"{m.Nome} fumou o último cigarro do maço.");
            }
            v.fumo = 0f;
            v.fumoCheioDesde = -1f;
            HumorDoMorador.Tirar(v, "SemCigarro");
            HumorDoMorador.Por(v, "Fumou", 6f, agora + 6f);
            bool noCinzeiro = PertoDoCinzeiro(m) && Bunker != null && Bunker.IsSpawned;
            if (noCinzeiro)
            {
                int n = Bunker.Bitucas;
                if (n >= BunkerDoMundo.BitucasNoCinzeiro)
                {
                    Registrar($"{m.Nome} esvaziou o cinzeiro da mesinha do fumo.");
                    n = 0;
                }
                Bunker.PorAsBitucas(n + 1);
            }
            m.fumo = EstadoDoFumo.Jogando;
            m.fumoDesde = Time.time;
            m.noCinzeiro = noCinzeiro;
            MaoNoCorpo(m);
        }

        /// <summary>O que vai na mão do corpo agora: o fumo, ou o taco na sinuca.</summary>
        MaoDoMorador MaoAgora(Morador m)
        {
            switch (m.fumo)
            {
                case EstadoDoFumo.Acendendo: return MaoDoMorador.Acendendo;
                case EstadoDoFumo.Aceso: return MaoDoMorador.Aceso;
                case EstadoDoFumo.Jogando: return m.noCinzeiro ? MaoDoMorador.NoCinzeiro : MaoDoMorador.Peteleco;
            }
            if (m.partida != null && m.vida.Atividade == AtividadeDoMorador.Sinuca)
            {
                int t = Vaga(m, Itens.EhTaco);
                if (t >= 0) return (MaoDoMorador)((int)MaoDoMorador.Taco + Itens.IndiceDoTaco((Item)m.vida.vagas[t].item));
            }
            return MaoDoMorador.Nada;
        }

        void MaoNoCorpo(Morador m)
        {
            if (m.corpo == null || !m.corpo.IsSpawned) return;
            byte b = (byte)MaoAgora(m);
            if (m.corpo.Mao.Value != b) m.corpo.Mao.Value = b;
        }

        // o fumante com a vontade quase cheia e sem maço nas vagas vai buscar um (de novo em 3 horas, se não achar)
        bool BuscarOMaco(Morador m, float agora)
        {
            var v = m.vida;
            if (!Fumante(m) || v.fumo < 85f || Vaga(m, Itens.EhFumo) >= 0 || agora < m.semMacoAte || v.crise != 0) return false;
            if (BuscarItem(m, Itens.EhFumo, agora)) return true;
            m.semMacoAte = agora + 3f;
            return false;
        }

        // ------------------------------------------------------------------ buscar e guardar no lugar

        /// <summary>Vai buscar o item que serve no lugar mais perto (a estante, o suporte, a mesinha): a busca da estante.</summary>
        bool BuscarItem(Morador m, System.Func<Item, bool> quer, float agora)
        {
            var v = m.vida;
            if (!CabeNasVagas(m)) return false;
            if (!EstoqueDoBunker.Procurar(quer, v.andar, v.pos, k => Livre(k, m), out var it, out int lugar)) return false;
            m.buscaEstante = it.estante;
            m.buscaId = it.id;
            m.pegou = false;
            m.chegouEm = Time.time;
            Mudar(m, AtividadeDoMorador.Buscar, lugar, (byte)AcoesDoCorpo.NaEstante(EstantesDoBunker.AlturaDe(it)), agora + 1f, agora);
            return true;
        }

        // depois da sinuca: o taco e a caixa de bolas voltam ao suporte (se tem vaga; senão, ficam com ele)
        bool Devolver(Morador m, float agora)
        {
            var v = m.vida;
            if (m.partida != null || m.querSinuca || v.crise != 0) return false;
            int vaga = Vaga(m, i => Itens.EhTaco(i) || Itens.EhCaixaDeBolas(i));
            int suporte = Suporte;
            var e = EstantesDoBunker.Instancia;
            if (vaga < 0 || suporte < 0 || e == null || !e.IsSpawned) return false;
            var item = (Item)v.vagas[vaga].item;
            if (!e.Espaco(suporte, item, out int x, out _, out _)) return false;
            var noLugar = new ItemNaEstante { estante = (ushort)suporte, item = item, x = (byte)x };
            float lado = EstantesDoBunker.LadoDe(noLugar);
            int lugar = -1;
            float melhor = float.MaxValue;
            foreach (int k in LugaresDoBunker.DoTipo(TipoDoLugar.Estante))
            {
                var l = LugaresDoBunker.Todos[k];
                if (l.movel != suporte || !Livre(k, m)) continue;
                float d = Mathf.Abs(l.lado - lado);
                if (d < melhor)
                {
                    melhor = d;
                    lugar = k;
                }
            }
            if (lugar < 0) return false;
            m.buscaEstante = suporte;
            m.guardar = item;
            m.pegou = false;
            m.chegouEm = Time.time;
            Mudar(m, AtividadeDoMorador.Guardar, lugar, (byte)AcoesDoCorpo.NaEstante(EstantesDoBunker.AlturaDe(noLugar)), agora + 1f, agora);
            return true;
        }

        // no suporte (ou na estante): o item sai das vagas e vai para ela
        void GuardarNoLugar(Morador m)
        {
            m.pegou = true;
            var e = EstantesDoBunker.Instancia;
            int vaga = Vaga(m, i => i == m.guardar);
            if (e == null || m.buscaEstante < 0 || vaga < 0) return;
            var g = m.vida.vagas[vaga];
            if (e.GuardarDoMorador(m.buscaEstante, m.guardar, g.qtd)) m.vida.vagas.RemoveAt(vaga);
        }

        // ------------------------------------------------------------------ a TV, o rádio, o canto do fumo e a roupa

        static HashSet<int> _daTV;

        /// <summary>O lugar é um sofá de frente para a TV da sala de estar (a até 7 m dela e virado para ela).</summary>
        public static bool VeATV(int lugar)
        {
            if (_daTV == null)
            {
                _daTV = new HashSet<int>();
                int tv = System.Array.FindIndex(MoveisDoBunker.Todos, x => x.modelo == "TVConsole");
                if (tv >= 0)
                {
                    var p = MoveisDoBunker.Todos[tv].lugar;
                    foreach (int k in LugaresDoBunker.DoTipo(TipoDoLugar.Sofa))
                    {
                        var l = LugaresDoBunker.Todos[k];
                        if (l.andar != MoveisDoBunker.Todos[tv].andar || l.comodo != "sala") continue;
                        var d = p - l.raiz;
                        d.y = 0f;
                        var frente = Quaternion.Euler(0f, l.giro, 0f) * Vector3.forward;
                        if (d.magnitude < 7f && Vector3.Dot(frente, d.normalized) > 0.25f) _daTV.Add(k);
                    }
                }
            }
            return _daTV.Contains(lugar);
        }

        int LivreDaLista(Morador m, IEnumerable<int> lugares)
        {
            var livres = new List<int>();
            foreach (int k in lugares) if (Livre(k, m)) livres.Add(k);
            return livres.Count == 0 ? -1 : livres[m.acaso.Next(livres.Count)];
        }

        int DaTV(Morador m)
        {
            VeATV(-1);
            if (m.vida.lugar >= 0 && _daTV.Contains(m.vida.lugar)) return m.vida.lugar;
            return LivreDaLista(m, _daTV);
        }

        // o canto do fumo: os lugares perto do cinzeiro (os em pé dos lados da mesinha, e os sofás, se houver)
        int NoCantoDoFumo(Morador m)
        {
            int c = Cinzeiro;
            if (c < 0) return -1;
            var p = MoveisDoBunker.Todos[c].lugar;
            var lista = new List<int>();
            for (int k = 0; k < LugaresDoBunker.Todos.Length; k++)
            {
                var l = LugaresDoBunker.Todos[k];
                if ((l.tipo == TipoDoLugar.Sofa || l.tipo == TipoDoLugar.EmPe) && l.andar == MoveisDoBunker.Todos[c].andar && Plano(l.raiz - p) < PertoDoCinzeiroM)
                    lista.Add(k);
            }
            if (m.vida.lugar >= 0 && lista.Contains(m.vida.lugar)) return m.vida.lugar;
            return LivreDaLista(m, lista);
        }

        /// <summary>O lugar é a cadeira do rádio.</summary>
        static bool NoRadio(int lugar) => lugar >= 0 && LugaresDoBunker.Todos[lugar].tipo == TipoDoLugar.Radio;

        // a TV e o rádio ligados enquanto alguém vê ou ouve (no lazer, sentado no lugar)
        void LigarOLazer()
        {
            if (Bunker == null || !Bunker.IsSpawned) return;
            bool tv = false, radio = false;
            foreach (var m in _moradores)
            {
                var v = m.vida;
                if (!v.noLugar || v.lugar < 0 || v.Atividade != AtividadeDoMorador.Lazer) continue;
                var acao = AcoesDoCorpo.Acao(v.acao);
                if (acao == AcaoDoCorpo.Sofa && VeATV(v.lugar)) tv = true;
                if (acao == AcaoDoCorpo.Cadeira && NoRadio(v.lugar)) radio = true;
            }
            Bunker.LigarOLazer(tv, radio);
        }

        // ------------------------------------------------------------------ o que vem com o bunker

        // uma vez por mundo: os dois tacos e a caixa de bolas no suporte e, com um fumante, dois maços na mesinha do fumo (o
        // que eles guardaram nesses anos, como a reserva de comida)
        void PorOLazerDoComeco()
        {
            var salvo = Salvamento.Atual;
            var e = EstantesDoBunker.Instancia;
            if (e == null || !e.IsServer || (salvo != null && salvo.lazerDosMoradores) || _moradores.Count == 0) return;
            if (salvo != null) salvo.lazerDosMoradores = true;
            var suporte = EstoqueDoBunker.Estantes(TipoDoEstoque.Suporte);
            int n = 0;
            foreach (var item in new[] { Item.TacoMadeira, Item.TacoEscuro, Item.CaixaDeBolas })
                if (e.GuardarNasEstantes(suporte, item, -1, 0, 0)) n++;
            bool fumantes = _moradores.Exists(Fumante);
            int macos = 0;
            if (fumantes)
                for (int k = 0; k < 2; k++)
                    if (e.GuardarNasEstantes(EstoqueDoBunker.Estantes(TipoDoEstoque.Fumo), Item.MacoDeCigarros, MacoCheio, 0, 0)) macos++;
            if (n + macos > 0)
                Registrar($"Os tacos e a caixa de bolas foram para o suporte da sala de jogos{(macos > 0 ? $", e {macos} maços para a mesinha do fumo" : "")}.");
        }

        /// <summary>Os cigarros de um maço cheio.</summary>
        static int MacoCheio => Itens.Pilha(Item.MacoDeCigarros);

        // ------------------------------------------------------------------ a sinuca

        public enum EstadoDaPartida : byte { Esperando, Andando, Batendo, Rolando }

        /// <summary>A partida dos moradores na mesa de sinuca do bunker (uma só: a da sala de jogos).</summary>
        public sealed class Partida
        {
            public ulong chave;
            public Vector3 centro;
            public readonly List<Morador> jogadores = new List<Morador>();
            public int vez, tacadas, encacapadas;
            public Morador comAsBolas;
            public EstadoDaPartida estado;
            public float desde;
            public readonly FisicaDaSinuca sim = new FisicaDaSinuca();
            public ushort foraAntes;
            public Vector2 mira;
            public float forca, proximoQuadro, somBola, somTabela;
            public int somCacapas;
            public bool somTacada;
            public readonly HashSet<string> quem = new HashSet<string>();
        }

        Partida _partida;

        /// <summary>A partida de agora (o F1 e o terminal), ou null.</summary>
        public Partida PartidaDeSinuca => _partida;

        // a tacada no Sinuca_Tacada do bloco_morador.py: a bola branca na frente da raiz (o x do Blender é o -x daqui, o -y o
        // z) e o taco indo para a frente e um pouco para a esquerda; a batida no quadro 60 (24 por segundo)
        static readonly Vector3 BolaNaTacada = new Vector3(-0.073f, 0f, 1.03f);
        const float GiroDoTaco = 10.2f, TempoDaBatida = 60f / 24f;

        // no lazer: entrar na partida (com o taco, e a caixa de bolas se a mesa está sem elas) ou ir buscar o que falta.
        // A sinuca é um lazer entre os outros (o Jogador, o lazer favorito); com um jogando sozinho, outro pode entrar
        bool QuerSinuca(Morador m, float agora, float dura)
        {
            var s = SinucaDoMundo.Instancia;
            if (s == null || !s.IsSpawned || Bunker == null || !Bunker.ChaveDaSinuca(out ulong chave, out var centro)) return false;
            s.Estado(chave, out var mesa);
            if (mesa.jogador != SinucaDoMundo.Ninguem && mesa.jogador != SinucaDoMundo.Moradores)
            {
                m.querSinuca = m.forcarSinuca = false;
                return false;
            }
            if (!m.querSinuca && !m.forcarSinuca)
            {
                if (_partida != null && _partida.jogadores.Count >= 2) return false;
                double chance = _partida != null ? 0.55 : m.ficha.Tem("Jogador") ? 0.5 : 0.15;
                if (m.acaso.NextDouble() >= chance) return false;
            }
            m.querSinuca = true;
            // o taco (do suporte, ou de onde houver)
            if (Vaga(m, Itens.EhTaco) < 0)
            {
                if (BuscarItem(m, Itens.EhTaco, agora)) return true;
                m.querSinuca = m.forcarSinuca = false;
                return false;
            }
            // as bolas: na mesa, na partida (alguém pôs ou vai pôr), com outro que vai jogar, ou a caixa cheia com ele
            bool bolas = mesa.comBolas || _partida != null
                         || _moradores.Exists(o => o != m && (o.querSinuca || o.forcarSinuca) && Vaga(o, i => i == Item.CaixaDeBolas) >= 0);
            if (!bolas && Vaga(m, i => i == Item.CaixaDeBolas) < 0)
            {
                if (BuscarItem(m, i => i == Item.CaixaDeBolas, agora)) return true;
                m.querSinuca = m.forcarSinuca = false;
                return false;
            }
            int l = Livre(m, TipoDoLugar.Sinuca, 1);
            if (l < 0)
            {
                m.querSinuca = m.forcarSinuca = false;
                return false;
            }
            if (_partida == null) _partida = new Partida { chave = chave, centro = centro, desde = Time.time };
            if (!_partida.jogadores.Contains(m)) _partida.jogadores.Add(m);
            _partida.quem.Add(m.Nome);
            m.partida = _partida;
            m.querSinuca = m.forcarSinuca = false;
            Mudar(m, AtividadeDoMorador.Sinuca, l, (byte)AcaoDoCorpo.Andando, agora + Mathf.Max(dura, 1.2f), agora);
            return true;
        }

        // ele saiu da partida (a hora acabou, a fome, um jogador pegou a mesa): o motivo e, se foi o último, o fim dela
        void SairDaPartida(Morador m, float agora)
        {
            var p = m.partida;
            m.partida = null;
            m.livre = false;
            if (p == null) return;
            p.jogadores.Remove(m);
            if (p.tacadas > 0 || Time.time - p.desde > 20f)
            {
                HumorDoMorador.Por(m.vida, "Sinuca", m.ficha.Tem("Jogador") ? 16f : 8f, agora + 24f);
                m.vida.ultimoLazer = agora;
            }
            MaoNoCorpo(m);
            if (p.jogadores.Count == 0) AcabarAPartida(p);
        }

        // ninguém mais jogando: as bolas voltam para a caixa (de quem pôs elas na mesa) e a mesa fica livre
        void AcabarAPartida(Partida p)
        {
            if (_partida == p) _partida = null;
            var s = SinucaDoMundo.Instancia;
            if (s == null || !s.IsSpawned) return;
            if (p.comAsBolas != null)
            {
                int vazia = Vaga(p.comAsBolas, i => i == Item.CaixaDeBolasVazia);
                if (vazia >= 0 && s.MoradorRecolheAsBolas(p.chave)) p.comAsBolas.vida.vagas[vazia].item = (int)Item.CaixaDeBolas;
            }
            s.MoradoresSoltam(p.chave);
            if (p.tacadas > 0)
            {
                var nomes = new List<string>(p.quem);
                string quem = nomes.Count == 1 ? nomes[0] : string.Join(", ", nomes.GetRange(0, nomes.Count - 1)) + " e " + nomes[nomes.Count - 1];
                Registrar($"{quem} {(nomes.Count == 1 ? "jogou" : "jogaram")} sinuca ({p.tacadas} {(p.tacadas == 1 ? "tacada" : "tacadas")}, {p.encacapadas} {(p.encacapadas == 1 ? "bola encaçapada" : "bolas encaçapadas")}).");
            }
        }

        // a cada quadro: as bolas na mesa, a vez de cada um, a tacada e as bolas rolando (só com o corpo e a mesa de perto;
        // no papel, só o tempo passa)
        void AtualizarAPartida()
        {
            var p = _partida;
            if (p == null) return;
            p.jogadores.RemoveAll(x => x.partida != p || x.vida.Atividade != AtividadeDoMorador.Sinuca);
            if (p.jogadores.Count == 0)
            {
                AcabarAPartida(p);
                return;
            }
            var s = SinucaDoMundo.Instancia;
            if (s == null || !s.IsSpawned) return;
            if (!s.MoradoresPegam(p.chave, p.centro))
            {
                // um jogador pegou a mesa: a partida acaba
                foreach (var j in p.jogadores) j.vida.ate = Agora;
                return;
            }
            s.Estado(p.chave, out var naRede);
            if (!naRede.comBolas)
            {
                // quem tem a caixa cheia põe as bolas, já no lugar dele
                var dono = p.jogadores.Find(j => j.vida.noLugar && Vaga(j, i => i == Item.CaixaDeBolas) >= 0);
                if (dono == null || !s.MoradorPoeAsBolas(p.chave, p.centro)) return;
                dono.vida.vagas[Vaga(dono, i => i == Item.CaixaDeBolas)].item = (int)Item.CaixaDeBolasVazia;
                p.comAsBolas = dono;
                p.desde = Time.time;
                return;
            }
            var mesa = s.NaCena(p.chave);
            var jv = p.jogadores[p.vez % p.jogadores.Count];
            if (p.estado == EstadoDaPartida.Rolando)
            {
                Rolar(p, s, mesa != null && jv.corpo != null);
                return;
            }
            if (mesa == null || jv.corpo == null) return;
            switch (p.estado)
            {
                case EstadoDaPartida.Esperando:
                    if (Time.time - p.desde < 1.2f || !jv.vida.noLugar || jv.passo != Passo.NoLugar) return;
                    EscolherATacada(p, s, mesa, jv);
                    p.estado = EstadoDaPartida.Andando;
                    p.desde = Time.time;
                    break;
                case EstadoDaPartida.Andando:
                    if (jv.passo == Passo.NoLugar && jv.vida.noLugar && jv.livre)
                    {
                        jv.corpo.Tacadas.Value++;
                        p.estado = EstadoDaPartida.Batendo;
                        p.desde = Time.time;
                    }
                    else if (Time.time - p.desde > 25f)
                    {
                        // não chegou: passa a vez
                        p.vez++;
                        p.estado = EstadoDaPartida.Esperando;
                        p.desde = Time.time;
                    }
                    break;
                case EstadoDaPartida.Batendo:
                    if (Time.time - p.desde >= TempoDaBatida) Bater(p, s);
                    break;
            }
        }

        // a tacada da vez: a bola mais fácil de encaçapar e onde a raiz dele fica para ela (atrás da branca, na mira)
        void EscolherATacada(Partida p, SinucaDoMundo s, MesaDeSinuca mesa, Morador j)
        {
            var pos = new Vector2[FisicaDaSinuca.Bolas];
            s.Posicoes(p.chave, pos);
            s.Estado(p.chave, out var naRede);
            p.sim.Carregar(pos, naRede.fora);
            if (!p.sim.Dentro(FisicaDaSinuca.Branca)) p.sim.VoltarABranca();
            var (mira, forca) = Mirar(p.sim, j);
            p.mira = mira;
            p.forca = forca;
            var t = mesa.mesa;
            var branca = p.sim.Pos[FisicaDaSinuca.Branca];
            var bolaW = t.TransformPoint(new Vector3(branca.x, mesa.alturaDoPano, branca.y));
            var dirW = t.TransformDirection(new Vector3(mira.x, 0f, mira.y));
            dirW.y = 0f;
            dirW.Normalize();
            float giro = Mathf.Atan2(dirW.x, dirW.z) * Mathf.Rad2Deg + GiroDoTaco;
            var raizW = bolaW - Quaternion.Euler(0f, giro, 0f) * BolaNaTacada;
            // fora da mesa: recua na mira até a raiz sair dela (a mesa no tamanho real, com a folga do corpo)
            float mx = 1.15f + 0.2f, mz = 0.655f + 0.2f;
            for (int k = 0; k < 40; k++)
            {
                var q = t.InverseTransformPoint(raizW);
                if (Mathf.Abs(q.x) > mx || Mathf.Abs(q.z) > mz) break;
                raizW -= dirW * 0.05f;
            }
            var raiz = Local(raizW);
            raiz.y = Base(1);
            IrPara(j, raiz, Mathf.Repeat(giro - Bunker.Raiz.eulerAngles.y, 360f));
        }

        // a mira: a bola (não a preta, enquanto houver outras) e a caçapa com o corte mais fácil e o caminho livre; o erro
        // de quem joga (o Jogador acerta mais; a vista fraca e a tristeza, menos)
        static (Vector2 mira, float forca) Mirar(FisicaDaSinuca sim, Morador j)
        {
            float R = FisicaDaSinuca.Raio, mc = FisicaDaSinuca.MeioComprimento, ml = FisicaDaSinuca.MeioLargura;
            var cacapas = new[]
            {
                new Vector2(-mc - 0.01f, -ml - 0.01f), new Vector2(0f, -ml - 0.03f), new Vector2(mc + 0.01f, -ml - 0.01f),
                new Vector2(-mc - 0.01f, ml + 0.01f), new Vector2(0f, ml + 0.03f), new Vector2(mc + 0.01f, ml + 0.01f),
            };
            var branca = sim.Pos[FisicaDaSinuca.Branca];
            int restantes = 0;
            for (int b = 1; b < FisicaDaSinuca.Bolas; b++) if (sim.Dentro(b) && b != FisicaDaSinuca.Preta) restantes++;
            float melhor = -1f;
            Vector2 mira = Vector2.right;
            float distancia = 1f;
            for (int b = 1; b < FisicaDaSinuca.Bolas; b++)
            {
                if (!sim.Dentro(b) || (b == FisicaDaSinuca.Preta && restantes > 0)) continue;
                var bola = sim.Pos[b];
                foreach (var c in cacapas)
                {
                    var d = (c - bola).normalized;
                    var fantasma = bola - d * 2f * R;
                    var ate = fantasma - branca;
                    float dist = ate.magnitude;
                    if (dist < 2f * R) continue;
                    float corte = Vector2.Angle(ate, d);
                    if (corte > 70f || !Livre(sim, branca, fantasma, b) || !Livre(sim, bola, c, b)) continue;
                    float nota = Mathf.Cos(corte * Mathf.Deg2Rad) / (dist + 0.7f * Vector2.Distance(bola, c) + 0.2f);
                    if (nota > melhor)
                    {
                        melhor = nota;
                        mira = ate / dist;
                        distancia = dist + Vector2.Distance(bola, c);
                    }
                }
            }
            if (melhor < 0f)
            {
                // nenhuma boa: na bola mais perto, com força
                float perto = float.MaxValue;
                for (int b = 1; b < FisicaDaSinuca.Bolas; b++)
                {
                    if (!sim.Dentro(b)) continue;
                    float d = Vector2.Distance(sim.Pos[b], branca);
                    if (d > 1e-3f && d < perto)
                    {
                        perto = d;
                        mira = (sim.Pos[b] - branca) / d;
                    }
                }
                distancia = 1.6f;
            }
            float erro = j.ficha.Tem("Jogador") ? 1.2f : 3f;
            if (j.ficha.TemCondicao("VistaFraca")) erro += 3f;
            if (HumorDoMorador.Faixa(j.vida.humor) == FaixaDoHumor.Triste) erro += 1.5f;
            float g = ((float)j.acaso.NextDouble() + (float)j.acaso.NextDouble() - 1f) * erro * Mathf.Deg2Rad;
            mira = new Vector2(mira.x * Mathf.Cos(g) - mira.y * Mathf.Sin(g), mira.x * Mathf.Sin(g) + mira.y * Mathf.Cos(g));
            float forca = Mathf.Clamp(0.28f + 0.2f * distancia + (float)j.acaso.NextDouble() * 0.08f, 0.3f, 0.85f);
            return (mira, forca);
        }

        // nenhuma bola (fora a do lance) no caminho de a até b
        static bool Livre(FisicaDaSinuca sim, Vector2 a, Vector2 b, int bola)
        {
            var d = b - a;
            float l = d.magnitude;
            if (l < 1e-4f) return true;
            d /= l;
            for (int k = 0; k < FisicaDaSinuca.Bolas; k++)
            {
                if (k == bola || k == FisicaDaSinuca.Branca || !sim.Dentro(k)) continue;
                var w = sim.Pos[k] - a;
                float t = Mathf.Clamp(Vector2.Dot(w, d), 0f, l);
                if ((w - d * t).sqrMagnitude < 4f * FisicaDaSinuca.Raio * FisicaDaSinuca.Raio * 0.95f) return false;
            }
            return true;
        }

        // o corpo vai a um ponto fora dos lugares (a raiz da tacada), em pé
        void IrPara(Morador m, Vector3 raiz, float giro)
        {
            m.livre = true;
            m.livreRaiz = raiz;
            m.livreGiro = giro;
            m.vida.noLugar = false;
            if (m.corpo == null) return;
            Seguir(m);
            m.passo = Passo.Levantando;
            m.desde = Time.time;
        }

        void Bater(Partida p, SinucaDoMundo s)
        {
            p.foraAntes = p.sim.Fora;
            p.sim.Vel[FisicaDaSinuca.Branca] = p.mira * Mathf.Max(0.12f, FisicaDaSinuca.VelocidadeMaxima * Mathf.Pow(p.forca, 1.3f));
            p.estado = EstadoDaPartida.Rolando;
            p.tacadas++;
            p.proximoQuadro = 0f;
            p.somTacada = true;
        }

        // as bolas rolando (de perto: aos poucos, com os quadros para todos; longe ou sem corpo: até parar, de uma vez)
        void Rolar(Partida p, SinucaDoMundo s, bool deVerdade)
        {
            if (deVerdade) p.sim.Simular(Mathf.Min(Time.deltaTime, 0.05f));
            else p.sim.AteParar();
            p.somBola = Mathf.Max(p.somBola, p.sim.BatidaDeBola);
            p.somTabela = Mathf.Max(p.somTabela, p.sim.BatidaNaTabela);
            p.somCacapas += p.sim.Cacapas;
            p.sim.Limpar();
            if (deVerdade && Time.time >= p.proximoQuadro)
            {
                p.proximoQuadro = Time.time + 0.05f;
                s.QuadroDosMoradores(p.chave, Quadro(p));
            }
            if (!p.sim.Parada) return;
            // parou: o que caiu, a branca de volta, o jogo de novo (a preta, ou todas) e a vez
            ushort caidas = (ushort)(p.sim.Fora & ~p.foraAntes);
            bool preta = (caidas & (1 << FisicaDaSinuca.Preta)) != 0;
            int fora = 0;
            for (int b = 1; b < FisicaDaSinuca.Bolas; b++) if (!p.sim.Dentro(b)) fora++;
            bool todas = fora >= FisicaDaSinuca.Bolas - 1;
            bool branca = (caidas & 1) != 0;
            if (!preta && !todas && branca) p.sim.VoltarABranca();
            for (int b = 1; b < FisicaDaSinuca.Bolas; b++) if ((caidas & (1 << b)) != 0) p.encacapadas++;
            if (deVerdade) s.QuadroDosMoradores(p.chave, Quadro(p));
            s.FimDosMoradores(p.chave, (Vector2[])p.sim.Pos.Clone(), p.sim.Fora, preta || todas);
            // quem encaçapa (sem a branca cair) joga de novo
            if ((caidas & ~1) == 0 || branca || preta) p.vez++;
            p.estado = EstadoDaPartida.Esperando;
            p.desde = Time.time;
        }

        static QuadroDaSinuca Quadro(Partida p)
        {
            var q = new QuadroDaSinuca
            {
                pos = (Vector2[])p.sim.Pos.Clone(), fora = p.sim.Fora,
                bola = (byte)Mathf.RoundToInt(Mathf.Clamp01(p.somBola) * 255f), tabela = (byte)Mathf.RoundToInt(Mathf.Clamp01(p.somTabela) * 255f),
                cacapas = (byte)Mathf.Min(255, p.somCacapas), tacada = (byte)(p.somTacada ? 1 : 0),
            };
            p.somBola = p.somTabela = 0f;
            p.somCacapas = 0;
            p.somTacada = false;
            return q;
        }

        // ------------------------------------------------------------------ os testes (o F1 BUNKER)

        /// <summary>No anfitrião: a vontade de fumar de todos os fumantes cheia (e os sem maço vão buscar um).</summary>
        public void FumoCheio()
        {
            float agora = Agora;
            foreach (var m in _moradores)
            {
                if (!Fumante(m)) continue;
                m.vida.fumo = 100f;
                m.semMacoAte = 0f;
                if (Vaga(m, Itens.EhFumo) < 0) m.vida.ate = agora;
            }
        }

        /// <summary>No anfitrião: os dois primeiros que podem vão jogar sinuca agora.</summary>
        public void SinucaAgora()
        {
            float agora = Agora;
            int n = 0;
            foreach (var m in _moradores)
            {
                if (n >= 2) break;
                if (m.partida != null || m.vida.crise != 0) continue;
                m.forcarSinuca = true;
                m.vida.ate = agora;
                n++;
            }
        }
    }
}
