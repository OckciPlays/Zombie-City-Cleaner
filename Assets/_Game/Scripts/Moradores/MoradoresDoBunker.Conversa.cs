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
    /// A conversa com os moradores e o Ygayr, no anfitrião (25/11/2026, a etapa 8):
    ///  - <b>a fala</b> (<see cref="FalaDoMorador"/>): o que a conversa mostra de cada um, escrito no corpo a cada 3 s;
    ///  - <b>parado na conversa</b>: quem abriu a tela segura o morador (o agente para; em pé, ele vira para quem fala) e,
    ///    quando ela fecha (ou ele se afasta mais de 6 m, ou passam 2 min), o horário dele anda o tempo que ficou parado
    ///    (a atividade acaba mais tarde, o caminho chega mais tarde), para ele voltar de onde parou. Com dois ou mais na
    ///    mesma conversa, ele só volta quando a última tela fecha, e olha para o mais perto (25/11/2026, a revisão);
    ///  - <b>o Ygayr Franscheschy</b> (a pedido: "um morador fixo, sempre na mesma cadeira da sala comum do B1, fumando um
    ///    cigarro atrás do outro"): a cadeira mais perto do cinzeiro da mesinha do fumo (ninguém mais senta nela), a vontade de
    ///    fumar sempre de volta uns segundos depois de cada cigarro, o maço que nunca acaba, e a fome, a sede e o sono que nunca
    ///    o tiram de lá (nem uma crise). Num jogo do Lakewood sem ele (os salvos de antes), ele chega.
    /// </summary>
    public partial class MoradoresDoBunker
    {
        // ------------------------------------------------------------------ a fala

        float _proximaFala;

        void AtualizarAsFalas()
        {
            bool todas = Time.unscaledTime >= _proximaFala;
            if (todas) _proximaFala = Time.unscaledTime + 3f;
            foreach (var m in _moradores)
            {
                if (m.corpo == null || !m.corpo.IsSpawned) continue;
                var antes = m.corpo.Fala.Value;
                // (25/11/2026, a revisão) fora da rodada de 3 s, só quando ele foi dormir ou acordou (ou mudou de atividade):
                // a tela calcula a saudação uma vez, ao abrir, e com a fala de 3 s atrás o morador que acabava de deitar
                // respondia acordado
                if (!todas && antes.Pronta && antes.atividade == m.vida.atividade && antes.dormindo == FalaDoMorador.Dormindo(m.vida)) continue;
                var f = FalaDoMorador.Da(m.ficha, m.vida);
                if (!f.Equals(antes)) m.corpo.Fala.Value = f;
            }
        }

        /// <summary>
        /// No anfitrião (o corpo, quando a tela abre ou fecha): quem conversa segura o morador. (25/11/2026, a revisão: cada
        /// tela de cada máquina conta; ele só é solto quando a última fecha, e não quando fecha a do último que abriu.)
        /// </summary>
        public void ConversaComMorador(CorpoDoMorador corpo, JogadorRede quem, bool comecou)
        {
            var m = _moradores.Find(x => x.corpo == corpo);
            if (m == null || quem == null) return;
            if (comecou)
            {
                if (!m.naConversa)
                {
                    m.naConversa = true;
                    m.conversandoCom.Clear();
                    m.conversaDesde = Time.time;
                    m.conversaDesdeAgora = Agora;
                }
                if (!m.conversandoCom.Contains(quem)) m.conversandoCom.Add(quem);
                m.conversandoAte = Time.time + 120f;
            }
            // (quem já tinha saído da conta, longe demais, não solta os outros)
            else if (m.conversandoCom.Remove(quem) && m.conversandoCom.Count == 0) SoltarDaConversa(m, Agora);
        }

        // a cada quadro, no lugar do Viver: conversando, ele fica parado (e, em pé, olhando para quem fala)
        bool Conversando(Morador m, float agora)
        {
            if (!m.naConversa) return false;
            var c = m.corpo;
            // (25/11/2026, a revisão) sai da conta quem saiu da rede ou se afastou mais de 6 m; ele segue parado enquanto
            // sobrar alguém, e olha para o mais perto
            JogadorRede perto = null;
            float menor = float.MaxValue;
            if (c != null && c.IsSpawned)
                for (int i = m.conversandoCom.Count - 1; i >= 0; i--)
                {
                    var j = m.conversandoCom[i];
                    float d2 = j != null && j.IsSpawned ? (j.transform.position - c.transform.position).sqrMagnitude : float.MaxValue;
                    if (d2 >= 6f * 6f)
                    {
                        m.conversandoCom.RemoveAt(i);
                        continue;
                    }
                    if (d2 < menor)
                    {
                        menor = d2;
                        perto = j;
                    }
                }
            if (perto == null || Time.time >= m.conversandoAte)
            {
                SoltarDaConversa(m, agora);
                return false;
            }
            if (c.agente != null && c.agente.enabled && c.agente.isOnNavMesh && !c.agente.isStopped) c.agente.isStopped = true;
            bool emPe = m.passo == Passo.Indo || (m.passo == Passo.NoLugar && AcoesDoCorpo.Postura(AcoesDoCorpo.Acao(m.vida.acao)) == PosturaDoCorpo.EmPe);
            if (emPe)
            {
                var d = perto.transform.position - c.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    c.transform.rotation = Quaternion.Slerp(c.transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-6f * Time.deltaTime));
            }
            return true;
        }

        // a conversa acabou: o horário dele anda o tempo parado, e o corpo volta a andar
        void SoltarDaConversa(Morador m, float agora)
        {
            if (!m.naConversa) return;
            m.naConversa = false;
            m.conversandoCom.Clear();
            float real = Time.time - m.conversaDesde, jogo = Mathf.Max(0f, agora - m.conversaDesdeAgora);
            var v = m.vida;
            // (25/11/2026, a revisão) acordado no meio do sono, a hora de levantar não muda: ele só abriu o olho (a tela de
            // quem dorme tem só o "pode voltar a dormir"), e o sono continuou enchendo enquanto isso
            if (jogo > 0f && !FalaDoMorador.Dormindo(v))
            {
                v.ate += jogo;
                if (v.chegaNaEscada > 0f) v.chegaNaEscada += jogo;
                if (v.chegaNoLugar > 0f) v.chegaNoLugar += jogo;
            }
            if (real > 0f)
            {
                m.desde += real;
                m.chegouEm += real;
                if (m.chegouNaEscada >= 0f) m.chegouNaEscada += real;
            }
            var c = m.corpo;
            if (c != null && c.agente != null && c.agente.enabled && c.agente.isOnNavMesh) c.agente.isStopped = false;
        }

        // ------------------------------------------------------------------ o Ygayr

        /// <summary>O morador é o Ygayr Franscheschy, o fumante da sala comum.</summary>
        public static bool EhOYgayr(FichaDoMorador f) =>
            f != null && f.nome == SorteioDosMoradores.NomeDoYgayr && f.sobrenome == SorteioDosMoradores.SobrenomeDoYgayr;

        static int _cadeiraDoYgayr = -2;

        /// <summary>
        /// A cadeira do Ygayr (o índice em <see cref="LugaresDoBunker.Todos"/>; -1: nenhuma): a cadeira (ou o sofá) mais perto do
        /// cinzeiro da mesinha do fumo, no andar dele; sem o cinzeiro, a primeira da sala comum do B1.
        /// </summary>
        static int CadeiraDoYgayr
        {
            get
            {
                if (_cadeiraDoYgayr != -2) return _cadeiraDoYgayr;
                _cadeiraDoYgayr = -1;
                var todos = LugaresDoBunker.Todos;
                int c = Cinzeiro;
                float menor = float.MaxValue;
                for (int k = 0; k < todos.Length; k++)
                {
                    var l = todos[k];
                    if (l.tipo != TipoDoLugar.Cadeira && l.tipo != TipoDoLugar.Sofa) continue;
                    float d;
                    if (c >= 0)
                    {
                        if (l.andar != MoveisDoBunker.Todos[c].andar) continue;
                        d = Plano(l.raiz - MoveisDoBunker.Todos[c].lugar) + (l.tipo == TipoDoLugar.Sofa ? 0.5f : 0f);
                    }
                    else
                    {
                        if (l.andar != 1 || l.comodo != "sala") continue;   // (o B1 é o andar 1)
                        d = k;
                    }
                    if (d < menor)
                    {
                        menor = d;
                        _cadeiraDoYgayr = k;
                    }
                }
                return _cadeiraDoYgayr;
            }
        }

        // a cada quadro, antes do Viver dele: a cadeira, o cigarro atrás do outro e nada que o tire de lá
        void CuidarDoYgayr(Morador m, float agora)
        {
            if (!EhOYgayr(m.ficha)) return;
            var v = m.vida;
            v.fome = Mathf.Max(v.fome, 60f);
            v.sede = Mathf.Max(v.sede, 60f);
            v.sono = Mathf.Max(v.sono, 60f);
            v.humor = Mathf.Max(v.humor, HumorDoMorador.CriseLeve + 7f);   // (triste, mas sem crise)
            // o maço que nunca acaba (25/11/2026, a revisão: cheio de novo antes do último cigarro; antes, ele só voltava depois
            // de vazio, e o terminal registrava "Ygayr fumou o último cigarro do maço." a cada uns 17 minutos)
            int maco = Vaga(m, Itens.EhFumo);
            if (maco < 0 && v.vagas.Count < AgendaDoMorador.Vagas(m.ficha))
                v.vagas.Add(new VagaDoMorador { item = (int)Item.MacoDeCigarros, qtd = MacoCheio });
            else if (maco >= 0 && v.vagas[maco].qtd < 3) v.vagas[maco].qtd = MacoCheio;
            // um cigarro atrás do outro (uns 12 s entre eles). (25/11/2026, a revisão) No papel, cada cigarro acaba num quadro,
            // e com os mesmos 12 s ele fumava uns 5 por minuto, enchendo o cinzeiro: lá, a vontade volta no tempo de um cigarro
            // inteiro com o corpo (acender, as tragadas até a hora do cigarro acabar e o peteleco, uns 60 s, e os 12 s)
            float entre = 12f;
            if (m.corpo == null)
            {
                float hora = CicloDoDia.HorasPorSegundo > 0f ? 1f / CicloDoDia.HorasPorSegundo : 60f;
                entre += FumoJogador.TempoDeAcender + Mathf.Min(DuracaoDoCigarro, Mathf.Min(TragadasPorCigarro * (EntreTragadas + 2f), hora)) + FumoJogador.TempoDoPeteleco;
            }
            if (m.fumo == EstadoDoFumo.Nada && Time.time - m.fumoDesde > entre) v.fumo = 100f;
            int cadeira = CadeiraDoYgayr;
            if (cadeira < 0 || v.crise != 0) return;
            if (v.lugar != cadeira || v.Atividade != AtividadeDoMorador.Lazer) Mudar(m, AtividadeDoMorador.Lazer, cadeira, LugaresDoBunker.Todos[cadeira].Acao, agora + 24f, agora);
            else if (v.ate < agora + 1f) v.ate = agora + 24f;
        }

        // no começo (o Lakewood): o Ygayr chega, se ainda não mora lá
        static void PorOYgayr(MundoSalvo salvo)
        {
            if (salvo == null || !salvo.lakewood || salvo.moradores == null || salvo.moradores.Count == 0) return;
            if (salvo.moradores.Exists(EhOYgayr)) return;
            int id = Mathf.Max(salvo.proximoMorador, 1);
            foreach (var f in salvo.moradores) id = Mathf.Max(id, f.id + 1);
            if (salvo.npcsDoMapa != null) foreach (var f in salvo.npcsDoMapa) id = Mathf.Max(id, f.id + 1);
            var y = SorteioDosMoradores.OYgayr(salvo.semente != 0 ? salvo.semente : 1, id);
            salvo.moradores.Add(y);
            salvo.proximoMorador = id + 1;
            if (salvo.npcsDescobertos != null && !salvo.npcsDescobertos.Contains(y.id)) salvo.npcsDescobertos.Add(y.id);
            Debug.Log($"[Moradores] O Ygayr Franscheschy chegou ao bunker ({y.idade} anos, o fumante da sala comum).");
        }
    }
}
