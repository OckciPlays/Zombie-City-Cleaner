using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;
// (a árvore de habilidades do jogador, da develop, também tem uma Habilidade)
using Habilidade = ZombieCityCleaner.Mundo.Habilidade;

namespace ZombieCityCleaner.Moradores
{
    /// <summary>
    /// O humor dos moradores no anfitrião (a parte 8 do Plano_NPCs_Bunker.md da pasta de arte, a primeira fatia): a cada
    /// passo das barras, os motivos que valem agora (a fome, a sede, o lazer, a reza, a paixão, a fartura e a escassez, o
    /// casal, o portão aberto para o medroso, os itens no chão para o arrumado, um jogador que volta), o humor andando até a
    /// conta (<see cref="HumorDoMorador"/>) e as crises: as leves (chorar trancado no quarto, comer sem fome, andar sem
    /// rumo), abaixo de 15 por 2 horas, e a forte (jogar no chão o que está numa estante), abaixo de 5 por 1 hora. Depois
    /// de uma crise, o "desabafou" (+20 por 1 dia) e 1 dia sem outra.
    /// </summary>
    public partial class MoradoresDoBunker
    {
        // a conta do bunker inteiro (a cada 2 s): os dias do estoque, o portão e os itens no chão
        float _proximaConta, _diasDoEstoque = -1f;
        bool _fartura, _escassez;
        int _itensNoChao;
        // os jogadores fora (o cliente → desde quando, nas horas do jogo)
        readonly Dictionary<ulong, float> _foraDesde = new Dictionary<ulong, float>();
        const float DiasDeFartura = 7f, DiasDeEscassez = 2f, HorasFora = 48f;

        // o humor de todos, depois das barras (dh: as horas do jogo do passo)
        void HumorDeTodos(float dh, float agora)
        {
            if (Time.unscaledTime >= _proximaConta)
            {
                _proximaConta = Time.unscaledTime + 2f;
                ContarOBunker(agora);
            }
            foreach (var m in _moradores)
            {
                Motivos(m, dh, agora);
                var v = m.vida;
                float alvo = HumorDoMorador.Alvo(m.ficha, v, CicloDoDia.Hora);
                v.humor = Mathf.MoveTowards(v.humor, alvo, HumorDoMorador.PorHora * dh);
                Avisos(m);
                Crises(m, agora);
            }
        }

        void ContarOBunker(float agora)
        {
            // o estoque (a comida e a bebida que eles alcançam), com o registro quando ele entra e sai da escassez
            bool fartura = false, escassez = false;
            // (só depois da reserva do começo: antes dela, o estoque vazio dava a escassez no registro)
            if (_reservaPosta && _moradores.Count > 0 && EstantesDoBunker.Instancia != null && EstantesDoBunker.Instancia.IsSpawned)
            {
                _diasDoEstoque = EstoqueDoBunker.Dias(_moradores.Count);
                fartura = _diasDoEstoque > DiasDeFartura;
                escassez = _diasDoEstoque < DiasDeEscassez;
            }
            if (escassez && !_escassez) Registrar($"O estoque de comida e água dá para menos de {DiasDeEscassez:0} dias.");
            if (!escassez && _escassez) Registrar("O estoque voltou a ter comida e água para uns dias.");
            _fartura = fartura;
            _escassez = escassez;

            // os itens no chão do bunker (para o arrumado; só conta se alguém é)
            _itensNoChao = 0;
            if (_moradores.Exists(x => x.ficha.Tem("Arrumado")) && ItensNoMundo.Instancia != null && ItensNoMundo.Instancia.IsSpawned)
                foreach (var it in ItensNoMundo.Instancia.Copia())
                    if (BunkerDoMundo.Dentro(it.apoio)) _itensNoChao++;

            // os jogadores que voltam depois de 2 dias fora
            var ainda = new HashSet<ulong>();
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.Criado) continue;
                ulong c = j.OwnerClientId;
                ainda.Add(c);
                var p = j.transform.position;
                bool fora = !BunkerDoMundo.NaArea(p) && !BunkerDoMundo.Dentro(p);
                if (fora)
                {
                    if (!_foraDesde.ContainsKey(c)) _foraDesde[c] = agora;
                    continue;
                }
                if (!_foraDesde.TryGetValue(c, out float desde)) continue;
                _foraDesde.Remove(c);
                if (agora - desde < HorasFora) continue;
                foreach (var m in _moradores) HumorDoMorador.Por(m.vida, "Voltou", 10f, agora + 24f);
                Registrar($"{j.NomeNoMapa} voltou depois de {(agora - desde) / 24f:0} dias fora.");
            }
            var sairam = new List<ulong>();
            foreach (var c in _foraDesde.Keys) if (!ainda.Contains(c)) sairam.Add(c);
            foreach (var c in sairam) _foraDesde.Remove(c);
        }

        // os motivos que valem agora para ele
        void Motivos(Morador m, float dh, float agora)
        {
            var v = m.vida;
            var f = m.ficha;
            var acao = AcoesDoCorpo.Acao(v.acao);
            var at = v.Atividade;
            if (v.ultimoLazer < 0f) v.ultimoLazer = agora;

            if (v.fome < AgendaDoMorador.Urgente) HumorDoMorador.Por(v, "Fome", -15f, agora + 24f);
            if (v.sede < AgendaDoMorador.Urgente) HumorDoMorador.Por(v, "Sede", -15f, agora + 24f);

            // o sono que conta (para o dormiu bem ou mal, ao acordar)
            if (v.noLugar && acao == AcaoDoCorpo.Dormindo) v.dormiuHoras += dh;

            // o lazer: meia hora vendo TV, lendo ou ouvindo o rádio (no lazer); a reza do religioso também é lazer (a sinuca dá
            // o motivo dela no fim da partida)
            if (v.noLugar && at == AtividadeDoMorador.Lazer && (acao == AcaoDoCorpo.Sofa || acao == AcaoDoCorpo.Lendo || (acao == AcaoDoCorpo.Cadeira && NoRadio(v.lugar))))
            {
                v.lazerAgora += dh;
                if (v.lazerAgora >= 0.5f)
                {
                    HumorDoMorador.Por(v, "Lazer", 8f, agora + 24f);
                    v.ultimoLazer = agora;
                    v.lazerAgora = 0f;
                }
            }
            else v.lazerAgora = 0f;
            if (v.noLugar && at == AtividadeDoMorador.Rezar && acao == AcaoDoCorpo.Rezando)
            {
                v.rezaAgora += dh;
                if (v.rezaAgora >= 0.2f)
                {
                    HumorDoMorador.Por(v, "Rezou", 6f, agora + 12f);
                    v.ultimoLazer = agora;
                    v.rezaAgora = 0f;
                }
            }
            else v.rezaAgora = 0f;
            if (agora - v.ultimoLazer > 48f) HumorDoMorador.PorEnquanto(v, "SemLazer", -8f, agora);

            // fazendo o que gosta (a paixão da habilidade do afazer), enquanto faz e mais 2 horas
            if (v.noLugar && at == AtividadeDoMorador.Trabalho)
            {
                var p = acao switch
                {
                    AcaoDoCorpo.LavandoALouca or AcaoDoCorpo.Cozinhando => f.PaixaoEm(Habilidade.Cozinha),
                    AcaoDoCorpo.Consertando => (Paixao)Mathf.Max((int)f.PaixaoEm(Habilidade.Mecanica), (int)f.PaixaoEm(Habilidade.Oficio)),
                    _ => Paixao.Nenhuma,
                };
                if (p != Paixao.Nenhuma) HumorDoMorador.Por(v, "Paixao", p == Paixao.Apaixonado ? 8f : 4f, agora + 2f);
            }

            // o bunker: a fartura e a escassez (o acumulador sente o dobro; o guloso gosta mais da fartura)
            bool acumulador = f.Tem("Acumulador");
            if (_fartura) HumorDoMorador.PorEnquanto(v, "Fartura", (acumulador ? 16f : 8f) + (f.Tem("Guloso") ? 4f : 0f), agora);
            if (_escassez) HumorDoMorador.PorEnquanto(v, "Escassez", acumulador ? -24f : -12f, agora);

            // o casal no bunker, os dois bem
            var laco = f.lacos.Find(x => x.tipo == LacoDoMorador.Casal);
            var outro = laco != null ? _moradores.Find(x => x.ficha.id == laco.outro) : null;
            if (outro != null && outro.vida.humor >= HumorDoMorador.Triste) HumorDoMorador.PorEnquanto(v, "Casal", 5f, agora);

            // o portão aberto (o medroso) e os itens no chão (o arrumado)
            if (f.Tem("Medroso") && Bunker != null && Bunker.Aberta(BunkerDoMundo.Portao)) HumorDoMorador.PorEnquanto(v, "Portao", -6f, agora);
            if (f.Tem("Arrumado") && _itensNoChao > 0) HumorDoMorador.PorEnquanto(v, "Baguncado", _itensNoChao > 5 ? -8f : -4f, agora);

            HumorDoMorador.Vencer(v, agora);
        }

        // ao acordar (a atividade de dormir acabou): dormiu bem na cama dele ou mal (o sofá, o insone uma noite em três)
        void Acordou(Morador m, float agora)
        {
            var v = m.vida;
            if (v.dormiuHoras < 3f)
            {
                v.dormiuHoras = 0f;
                return;
            }
            bool naCama = v.cama >= 0 && v.lugar == v.cama;
            bool insone = m.ficha.TemCondicao("Insonia") && m.acaso.NextDouble() < 1.0 / 3.0;
            if (naCama && !insone)
            {
                HumorDoMorador.Tirar(v, "DormiuMal");
                HumorDoMorador.Por(v, "CamaBoa", 5f, agora + 24f);
            }
            else
            {
                HumorDoMorador.Tirar(v, "CamaBoa");
                HumorDoMorador.Por(v, "DormiuMal", m.ficha.Tem("Simples") ? -4f : -8f, agora + 24f);
            }
            v.dormiuHoras = 0f;
        }

        // o registro diz quando ele fica triste (de novo só depois de melhorar)
        void Avisos(Morador m)
        {
            var v = m.vida;
            if (v.humor < HumorDoMorador.Triste && !v.avisouTriste)
            {
                v.avisouTriste = true;
                string pior = PiorMotivo(m);
                Registrar($"{m.Nome} está {(HumorDoMorador.Faixa(v.humor) == FaixaDoHumor.Triste ? "triste" : "mal")}{(pior != null ? $" ({pior})" : "")}.");
            }
            else if (v.humor > HumorDoMorador.Triste + 7f) v.avisouTriste = false;
        }

        /// <summary>O motivo que mais pesa para baixo, em palavras ("passou fome"), ou null.</summary>
        string PiorMotivo(Morador m)
        {
            MotivoDoMorador pior = null;
            if (m.vida.motivos != null)
                foreach (var x in m.vida.motivos) if (x.valor < 0f && (pior == null || x.valor < pior.valor)) pior = x;
            return pior != null ? NomeDoMotivo(pior.chave, m.ficha.mulher) : null;
        }

        static string NomeDoMotivo(string chave, bool mulher) => chave == "Baguncado" ? "itens no chão do bunker" : HumorDoMorador.Nome(chave, mulher);

        /// <summary>Os motivos dele em texto ("+8 se divertiu"), do que mais pesa ao que menos, com o fixo dos traços.</summary>
        public List<string> TextosDosMotivos(Morador m)
        {
            var v = m.vida;
            var lista = new List<(float valor, string texto)>();
            if (v.motivos != null)
                foreach (var x in v.motivos)
                {
                    if (Mathf.Approximately(x.valor, 0f)) continue;
                    lista.Add((x.valor, $"{Sinal(x.valor)} {NomeDoMotivo(x.chave, m.ficha.mulher)}"));
                }
            var f = m.ficha;
            if (f.Tem("Alegre")) lista.Add((10f, $"+10 {(f.mulher ? "alegre" : "alegre")} (sempre)"));
            if (f.Tem("Pessimista")) lista.Add((-10f, "−10 pessimista (sempre)"));
            if (f.Tem("Notivago") && (CicloDoDia.Hora >= 20f || CicloDoDia.Hora < 6f)) lista.Add((4f, $"+4 {(f.mulher ? "notívaga" : "notívago")} de noite"));
            lista.Sort((a, b) => Mathf.Abs(b.valor).CompareTo(Mathf.Abs(a.valor)));
            return lista.ConvertAll(x => x.texto);
        }

        static string Sinal(float v) => v >= 0f ? $"+{v:0}" : $"−{-v:0}";

        // ------------------------------------------------------------------ as crises

        void Crises(Morador m, float agora)
        {
            var v = m.vida;
            if (v.crise != 0)
            {
                if (agora >= v.criseAte) AcabarACrise(m, agora);
                return;
            }
            v.baixoDesde = v.humor < HumorDoMorador.CriseLeve ? (v.baixoDesde < 0f ? agora : v.baixoDesde) : -1f;
            v.muitoBaixoDesde = v.humor < HumorDoMorador.CriseForte ? (v.muitoBaixoDesde < 0f ? agora : v.muitoBaixoDesde) : -1f;
            if (agora - v.ultimaCrise < HumorDoMorador.EntreCrises) return;
            if (v.muitoBaixoDesde >= 0f && agora - v.muitoBaixoDesde >= HumorDoMorador.AteACriseForte && TemOQueJogar())
            {
                ComecarACrise(m, CriseDoMorador.JogarNoChao, agora + 2f, agora);
                return;
            }
            if (v.baixoDesde < 0f || agora - v.baixoDesde < HumorDoMorador.AteACriseLeve) return;
            double r = m.acaso.NextDouble();
            if (r < 0.4) ComecarACrise(m, CriseDoMorador.Chorar, agora + 4f, agora);
            else if (r < 0.7 && EstoqueDoBunker.Contar().comidas > 0) ComecarACrise(m, CriseDoMorador.Comilanca, agora + 3f, agora);
            else ComecarACrise(m, CriseDoMorador.Vagar, agora + 4f, agora);
        }

        static bool TemOQueJogar()
        {
            var e = EstantesDoBunker.Instancia;
            if (e == null || !e.IsSpawned) return false;
            foreach (var it in e.Guardados) if (EstoqueDoBunker.Alcanca(it)) return true;
            return false;
        }

        void ComecarACrise(Morador m, CriseDoMorador c, float ate, float agora)
        {
            var v = m.vida;
            v.crise = (byte)c;
            v.criseAte = ate;
            v.ate = agora;   // decide de novo agora (a crise passa na frente)
            m.pegou = false;
            m.buscaEstante = -1;
            Registrar($"{m.Nome} teve uma crise: {HumorDoMorador.NomeDaCrise(c, m.ficha.mulher)}.");
        }

        void AcabarACrise(Morador m, float agora)
        {
            var v = m.vida;
            var c = v.Crise;
            v.crise = 0;
            v.ultimaCrise = agora;
            v.baixoDesde = v.muitoBaixoDesde = -1f;
            v.ate = agora;
            HumorDoMorador.Por(v, "Desabafou", 20f, agora + 24f);
            if (c != CriseDoMorador.JogarNoChao && c != CriseDoMorador.Comilanca) Registrar($"{m.Nome} se acalmou.");
        }

        /// <summary>
        /// No anfitrião, o F1: o humor de todos num valor (um motivo de teste por 6 horas que leva a conta até lá, já com o
        /// número nele, sem esperar a descida), ou de volta ao normal (negativo: sem o motivo de teste).
        /// </summary>
        public void HumorAte(float alvo)
        {
            float agora = Agora;
            foreach (var m in _moradores)
            {
                var v = m.vida;
                HumorDoMorador.Tirar(v, "Teste");
                if (alvo < 0f) continue;
                float valor = alvo - HumorDoMorador.Comeco - HumorDoMorador.DosTracos(m.ficha, CicloDoDia.Hora);
                foreach (var x in v.motivos) valor -= x.valor;
                HumorDoMorador.Por(v, "Teste", valor, agora + 6f);
                v.humor = HumorDoMorador.Alvo(m.ficha, v, CicloDoDia.Hora);
                // a crise pode vir já (o teste não espera o dia entre duas crises)
                v.ultimaCrise = -999f;
            }
        }

        // a crise na hora de decidir: o lugar e a ação dela (true: decidiu). A comilança vai pelo comer (o Decidir).
        bool DecidirACrise(Morador m, float agora)
        {
            var v = m.vida;
            switch (v.Crise)
            {
                case CriseDoMorador.Chorar:
                {
                    int l = NoQuartoDele(m);
                    Mudar(m, AtividadeDoMorador.Crise, l, (byte)AcaoDoCorpo.Chorando, v.criseAte, agora);
                    return true;
                }
                case CriseDoMorador.Vagar:
                {
                    // um lugar em pé qualquer, num andar sorteado, por uns 20 a 40 minutos (e de novo)
                    int andar = m.acaso.Next(0, Andares - 1);
                    int l = Livre(m, TipoDoLugar.EmPe, andar);
                    if (l == v.lugar) l = Livre(m, TipoDoLugar.EmPe, (andar + 1) % (Andares - 1));
                    Mudar(m, AtividadeDoMorador.Crise, l, l >= 0 ? LugaresDoBunker.Todos[l].Acao : (byte)0,
                          Mathf.Min(v.criseAte, agora + 0.33f + (float)m.acaso.NextDouble() * 0.33f), agora);
                    return true;
                }
                case CriseDoMorador.JogarNoChao:
                {
                    if (m.buscaEstante >= 0 && v.Atividade == AtividadeDoMorador.Crise) return true;
                    if (!EstoqueDoBunker.Procurar(_ => true, v.andar, v.pos, k => Livre(k, m), out var it, out int lugar))
                    {
                        v.crise = (byte)CriseDoMorador.Vagar;
                        return DecidirACrise(m, agora);
                    }
                    m.buscaEstante = it.estante;
                    m.buscaId = it.id;
                    m.pegou = false;
                    m.chegouEm = Time.time;
                    Mudar(m, AtividadeDoMorador.Crise, lugar, (byte)AcoesDoCorpo.NaEstante(EstantesDoBunker.AlturaDe(it)), v.criseAte, agora);
                    return true;
                }
            }
            return false;
        }

        // um lugar em pé no quarto dele (o cômodo da cama), ou no andar dos quartos
        int NoQuartoDele(Morador m)
        {
            var v = m.vida;
            if (v.lugar >= 0 && LugaresDoBunker.Todos[v.lugar].tipo == TipoDoLugar.EmPe && v.Atividade == AtividadeDoMorador.Crise) return v.lugar;
            if (v.cama >= 0)
            {
                var cama = LugaresDoBunker.Todos[v.cama];
                var todos = LugaresDoBunker.Todos;
                var noQuarto = new List<int>();
                foreach (int i in LugaresDoBunker.DoTipo(TipoDoLugar.EmPe))
                    if (todos[i].andar == cama.andar && todos[i].nome == cama.nome && Livre(i, m)) noQuarto.Add(i);
                if (noQuarto.Count > 0) return noQuarto[m.acaso.Next(noQuarto.Count)];
                return Livre(m, TipoDoLugar.EmPe, cama.andar);
            }
            return Livre(m, TipoDoLugar.EmPe, 3);
        }

        // a crise forte, na estante: até 4 itens do que ela tem vão para o chão na frente dele
        void JogarNoChao(Morador m)
        {
            m.pegou = true;
            var v = m.vida;
            var e = EstantesDoBunker.Instancia;
            var chao = ItensNoMundo.Instancia;
            if (e == null || chao == null || m.buscaEstante < 0 || v.lugar < 0) return;
            var ids = new List<ushort>();
            foreach (var it in e.Guardados) if (it.estante == m.buscaEstante && EstoqueDoBunker.Alcanca(it)) ids.Add(it.id);
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = m.acaso.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }
            var l = LugaresDoBunker.Todos[v.lugar];
            var frente = Quaternion.Euler(0f, l.giro, 0f) * Vector3.forward;
            var lado = Vector3.Cross(Vector3.up, frente);
            int n = 0;
            foreach (var id in ids)
            {
                if (n >= 4) break;
                if (!e.TirarUm(m.buscaEstante, id, out var item)) continue;
                // no chão entre a estante e ele, um pouco espalhados
                var p = l.raiz + frente * (0.15f + (float)m.acaso.NextDouble() * 0.5f) + lado * ((float)m.acaso.NextDouble() - 0.5f) * 1.2f;
                p.y = Base(l.andar);
                chao.Criar(item, Bunker.Raiz.TransformPoint(p), (float)m.acaso.NextDouble() * 360f);
                n++;
            }
            if (n > 0) Registrar($"{m.Nome} jogou no chão {(n == 1 ? "um item" : $"{n} itens")} {NoLugar(m.buscaEstante, false)}.");
            m.buscaEstante = -1;
        }
    }
}
