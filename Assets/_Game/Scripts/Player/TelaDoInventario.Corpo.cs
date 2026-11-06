using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using Pecas = ZombieCityCleaner.Vehicles.PecasDoMotor;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A aba CORPO da tela do inventário (05/11/2026, o sistema de vida por membro; H abre direto nela).
    /// - No lugar da grade, a silhueta grande (<c>CorpoNoHud.DesenharSilhueta</c>,
    ///   a mesma do HUD) e as 6 etiquetas, como as do capô: o nome, a vida da parte, a barra e o estado em palavras
    ///   ("sangrando", "enfaixado: fecha em 2 dias", "fratura com tala: sara em 14 h", "inutilizado"). Clicar numa parte (ou
    ///   na etiqueta) escolhe; de novo, volta ao automático (o curativo vai onde mais precisa). Embaixo, a vida geral, o
    ///   analgésico, a barra do curativo que está sendo feito (com o botão de parar) e o aviso.
    /// - À direita, no lugar dos detalhes, a parte escolhida e a coluna "CURATIVOS NA MOCHILA" (a atadura, as bandagens, o
    ///   spray, o kit médico, a tala, os comprimidos, o xarope e o antibiótico; só os que estão na mochila, que os 9 não cabem
    ///   a 720p): a quantidade (o spray, os usos) e o que o clique faz, ou, apagado, por que não serve
    ///   (<see cref="VidaJogador.PodeTratar"/>). Clicar começa o <see cref="Tratamento"/> (a barra de tempo; o anfitrião
    ///   confere e gasta no fim).
    /// - O botão direito numa parte lista os curativos que servem nela.
    /// - A etapa 2 (05/11/2026): a ferida protegida, limpa ou suja e o dia dela, a parte infeccionada e a linha da infecção
    ///   do corpo inteiro (a fase, o xarope, a próxima dose do antibiótico).
    /// Na aba MOCHILA, o botão direito num curativo lista as partes que ele trata (<see cref="OpcoesDoTratamento"/>), e os
    /// detalhes dele mostram o que ele faz (<see cref="LinhasDoTratamento"/>).
    /// </summary>
    public partial class TelaDoInventario
    {
        // as medidas, a 720p: a área da grade (a silhueta e as etiquetas) e a coluna dos detalhes (os curativos)
        static readonly Rect AbaCorpo = new Rect(648, 14, 150, 36);
        static readonly Rect AreaDoCorpo = new Rect(GradeX, 58, GradeMaxW, 486);
        static readonly Rect SilhuetaGrande = new Rect(500, 66, 158, 310);
        static readonly Rect BotaoParar = new Rect(GradeX + GradeMaxW - 96, 458, 90, 30);
        const float EtiquetaW = 150f, EtiquetaH = 96f, PassoDaEtiqueta = 106f;
        const float CurativosY = 230f, PassoDoCurativo = 56f;
        // o fim da coluna dos curativos (o fim da área dos detalhes): cabem 6 no passo cheio; com mais, o passo encolhe
        static float FimDosCurativos => AreaDosDetalhes.yMax - 4f;

        /// <summary>
        /// Os curativos da coluna, nesta ordem (a etapa 2: a bandagem antisséptica, o spray e o antibiótico). A coluna mostra
        /// só os que estão na mochila (<see cref="CurativosNaTela"/>): os 9 não cabem a 720p.
        /// </summary>
        static readonly Item[] Curativos =
        {
            Item.Atadura, Item.Bandagem, Item.BandagemAntisseptica, Item.SprayAntisseptico, Item.KitMedico, Item.Tala,
            Item.Comprimidos, Item.Remedio, Item.Antibiotico,
        };

        // os curativos da coluna agora (os que estão na mochila), refeitos a cada desenho e a cada clique
        readonly List<Item> _curativosNaTela = new List<Item>();

        void CurativosNaTela()
        {
            _curativosNaTela.Clear();
            foreach (var item in Curativos)
                if (inventario.Quantos(item) > 0) _curativosNaTela.Add(item);
        }

        // o passo das linhas: o cheio (56) até 6; com mais, o que couber até o fim da área
        float PassoDosCurativos => Mathf.Min(PassoDoCurativo, (FimDosCurativos - CurativosY) / Mathf.Max(1, _curativosNaTela.Count));

        // a parte escolhida (Sortear: nenhuma, o automático)
        ParteDoCorpo _parteEscolhida = PartesDoCorpo.Sortear;
        VidaJogador _vidaDoCorpo;
        readonly List<(string texto, Color cor)> _linhasDaParte = new List<(string, Color)>();

        VidaJogador VidaDoCorpo => _vidaDoCorpo != null ? _vidaDoCorpo : _vidaDoCorpo = GetComponent<VidaJogador>();

        /// <summary>Abre a tela na aba CORPO (a tecla H; o teste).</summary>
        public void AbrirNaAbaCorpo()
        {
            if (rede == null || !rede.IsOwner || MenuOnline.Aberto) return;
            IrParaAbaCorpo();
            Abrir(true);
        }

        // a tecla H: fechada, abre na aba CORPO (só jogando, não digitando); aberta nela, fecha; aberta em outra, troca
        void TeclaDoCorpo()
        {
            if (Aberta)
            {
                if (_aba == Aba.Corpo) Abrir(false);
                else IrParaAbaCorpo();
            }
            else if (MenuOnline.Controla) AbrirNaAbaCorpo();
        }

        void IrParaAbaCorpo()
        {
            _aba = Aba.Corpo;
            // a coluna da estante dá lugar à dos curativos
            if (_apertou == Origem.Estante)
            {
                _apertou = Origem.Nada;
                _arrastando = false;
            }
            _estante = -1;
        }

        // a etiqueta da parte: as da esquerda do jogador à esquerda (a silhueta é vista de costas), de cima para baixo
        static Rect Etiqueta(ParteDoCorpo p)
        {
            bool esquerda = p == ParteDoCorpo.Cabeca || p == ParteDoCorpo.BracoE || p == ParteDoCorpo.PernaE;
            int linha = p <= ParteDoCorpo.Tronco ? 0 : p <= ParteDoCorpo.BracoD ? 1 : 2;
            return new Rect(esquerda ? GradeX + 6f : GradeX + GradeMaxW - 6f - EtiquetaW, 66f + linha * PassoDaEtiqueta, EtiquetaW, EtiquetaH);
        }

        Rect LinhaDoCurativo(int i)
        {
            float passo = PassoDosCurativos;
            return Em(new Rect(AreaDoNome.x + 6f, CurativosY + i * passo, AreaDoNome.width - 12f, passo - 4f));
        }

        // a parte embaixo do mouse: a etiqueta dela ou ela na silhueta
        bool ParteNaAbaCorpo(Vector2 m, out ParteDoCorpo parte)
        {
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
                if (Em(Etiqueta((ParteDoCorpo)p)).Contains(m))
                {
                    parte = (ParteDoCorpo)p;
                    return true;
                }
            return CorpoNoHud.ParteEm(Em(SilhuetaGrande), m, out parte);
        }

        bool CurativoEm(Vector2 m, out int linha)
        {
            CurativosNaTela();
            for (linha = 0; linha < _curativosNaTela.Count; linha++)
                if (LinhaDoCurativo(linha).Contains(m)) return true;
            linha = -1;
            return false;
        }

        static bool Tratando(out Tratamento t)
        {
            t = Tratamento.Local;
            return t != null && t.Ativo;
        }

        // ------------------------------------------------------------------ o mouse

        /// <summary>O clique (o botão esquerdo) na aba CORPO: parar o curativo, escolher a parte ou usar um curativo.</summary>
        bool CliqueNaAbaCorpo(Vector2 m)
        {
            if (Tratando(out var t) && Em(BotaoParar).Contains(m))
            {
                t.Cancelar("Curativo interrompido");
                return true;
            }
            if (ParteNaAbaCorpo(m, out var parte))
            {
                // a mesma de novo: volta ao automático
                _parteEscolhida = parte == _parteEscolhida ? PartesDoCorpo.Sortear : parte;
                return true;
            }
            if (CurativoEm(m, out int linha))
            {
                UsarCurativo(_curativosNaTela[linha]);
                return true;
            }
            return false;
        }

        // o curativo da coluna: começa o tratamento na parte escolhida (o kit e o analgésico não têm parte), ou diz por que não
        void UsarCurativo(Item item)
        {
            if (!ServeAgora(item, out var parte, out var motivo))
            {
                if (!string.IsNullOrEmpty(motivo)) InventarioJogador.AvisoLocal(motivo);
                return;
            }
            if (!PrimeiraPilha(item, out ushort id)) return;
            ComecarTratamento(id, SemParte(item) ? PartesDoCorpo.Sortear : parte);
        }

        // sem parte: o kit (o corpo todo) e o que se toma pela boca (os comprimidos, o xarope, o antibiótico)
        static bool SemParte(Item item) => PartesDoCorpo.CorpoTodo(item) || PartesDoCorpo.PelaBoca(item);

        // começado pela aba MOCHILA, a tela passa para a aba CORPO: é lá que a barra e o botão de parar aparecem
        void ComecarTratamento(ushort id, ParteDoCorpo parte)
        {
            var t = Tratamento.Local;
            if (t != null && t.Comecar(id, parte) && _aba != Aba.Corpo) IrParaAbaCorpo();
        }

        // a primeira pilha do item na mochila
        bool PrimeiraPilha(Item item, out ushort id)
        {
            foreach (var it in inventario.Mochila)
                if (it.item == item)
                {
                    id = it.id;
                    return true;
                }
            id = 0;
            return false;
        }

        // os usos que sobram no primeiro do item na mochila (o spray: o que o clique gasta); -1 se não tem usos
        int UsosDoPrimeiro(Item item)
        {
            if (!Itens.ComUsos(item)) return -1;
            foreach (var it in inventario.Mochila)
                if (it.item == item) return Itens.UsosQueSobram(it.desgaste);
            return -1;
        }

        /// <summary>
        /// O curativo serve agora na parte escolhida (ou no automático)? Escolhe a parte (a que ele vai tratar) e, se não
        /// serve, o motivo: não tem na mochila, já está tratando, ocupado, ou o do <see cref="VidaJogador.PodeTratar"/>.
        /// </summary>
        bool ServeAgora(Item item, out ParteDoCorpo parte, out string motivo)
        {
            return ServeAgora(item, SemParte(item) ? PartesDoCorpo.Sortear : _parteEscolhida, out parte, out motivo);
        }

        bool ServeAgora(Item item, ParteDoCorpo naParte, out ParteDoCorpo parte, out string motivo)
        {
            parte = naParte;
            var vida = VidaDoCorpo;
            if (vida == null || !vida.IsSpawned)
            {
                motivo = null;
                return false;
            }
            if (inventario.Quantos(item) <= 0)
            {
                motivo = "Não tem na mochila";
                return false;
            }
            if (Tratando(out _))
            {
                motivo = "Espere o curativo de agora acabar";
                return false;
            }
            if (arma != null && (arma.Usando || arma.Montando))
            {
                motivo = "Ocupado: espere as mãos ficarem livres";
                return false;
            }
            return vida.PodeTratar(item, ref parte, out motivo);
        }

        // o que o clique faz, já com a parte
        static string CliqueDo(in EstadoDoCorpo e, Item item, ParteDoCorpo parte)
        {
            string tempo = Numero(PartesDoCorpo.TempoDe(item)) + " s";
            return item switch
            {
                Item.Atadura or Item.Bandagem => $"Clique: enfaixar {PartesDoCorpo.NomeComArtigo(parte)} ({tempo})",
                Item.BandagemAntisseptica => $"Clique: enfaixar e proteger {PartesDoCorpo.NomeComArtigo(parte)} ({tempo})",
                Item.SprayAntisseptico => $"Clique: borrifar {PartesDoCorpo.NaParte(parte)} ({tempo})",
                Item.Tala => $"Clique: pôr a tala {PartesDoCorpo.NaParte(parte)} ({tempo})",
                Item.KitMedico => $"Clique: tratar o corpo todo ({tempo})",
                Item.Antibiotico => $"Clique: tomar a dose, {PorCento(VidaJogador.ChanceDoAntibiotico(e.Infeccao))} de cura ({tempo})",
                Item.Remedio => $"Clique: tomar, tira a febre ({tempo})",
                _ => $"Clique: tomar ({tempo})",
            };
        }

        static string PorCento(float f) => Mathf.RoundToInt(f * 100f) + "%";

        // "Cura a infecção: 90% / 70% / 40%" (a ferida infeccionada, a febre, a sepse)
        static string ChancesDoAntibiotico() =>
            $"{PorCento(VidaJogador.ChanceDoAntibiotico(1))} / {PorCento(VidaJogador.ChanceDoAntibiotico(2))} / {PorCento(VidaJogador.ChanceDoAntibiotico(3))}";

        // ------------------------------------------------------------------ o menu do botão direito

        /// <summary>
        /// Na aba MOCHILA, o botão direito num curativo: uma opção por parte que ele trata ("Enfaixar o braço esquerdo
        /// (sangrando)", "Pôr a tala na perna direita (quebrada)"); o kit, "Usar no corpo todo"; o analgésico, "Tomar".
        /// Nada para tratar: a opção apagada, com o motivo.
        /// </summary>
        void OpcoesDoTratamento(Item item, ushort id, bool ocupado)
        {
            var vida = VidaDoCorpo;
            if (vida == null || !vida.IsSpawned) return;
            if (Tratando(out _) || ocupado)
            {
                Opcao((item == Item.KitMedico ? "Usar no corpo todo" : PartesDoCorpo.PelaBoca(item) ? "Tomar" : "Tratar") + " (ocupado)", null);
                return;
            }
            string tempo = $" ({Numero(PartesDoCorpo.TempoDe(item))} s)";
            if (SemParte(item))
            {
                var auto = PartesDoCorpo.Sortear;
                bool pode = vida.PodeTratar(item, ref auto, out var motivo);
                string texto = item == Item.KitMedico ? "Usar no corpo todo" : item == Item.Antibiotico ? "Tomar a dose" : "Tomar";
                Opcao(pode ? texto + tempo : $"{texto} ({motivo})", pode ? () => ComecarTratamento(id, PartesDoCorpo.Sortear) : (System.Action)null);
                return;
            }
            var e = vida.Corpo.Value;
            int n = 0;
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var parte = (ParteDoCorpo)p;
                if (item == Item.Tala && !PartesDoCorpo.EhMembro(parte)) continue;
                var aqui = parte;
                if (!vida.PodeTratar(item, ref aqui, out _)) continue;
                string verbo = item == Item.Tala ? $"Pôr a tala {PartesDoCorpo.NaParte(parte)}"
                    : item == Item.SprayAntisseptico ? $"Borrifar o spray {PartesDoCorpo.NaParte(parte)}"
                    : $"Enfaixar {PartesDoCorpo.NomeComArtigo(parte)}";
                Opcao($"{verbo} ({PorQueTrata(e, parte, item)})", () => ComecarTratamento(id, parte));
                n++;
            }
            if (n == 0)
            {
                var auto = PartesDoCorpo.Sortear;
                vida.PodeTratar(item, ref auto, out var motivo);
                Opcao(string.IsNullOrEmpty(motivo) ? "Nada para tratar" : motivo, null);
            }
        }

        // por que a parte precisa: "sangrando", "60%", "quebrada", "torcido", "fratura exposta"
        static string PorQueTrata(in EstadoDoCorpo e, ParteDoCorpo p, Item item)
        {
            string o = PartesDoCorpo.NomeComArtigo(p)[0] == 'a' ? "a" : "o";
            if (item == Item.Tala)
                return e.LesaoDe(p) switch
                {
                    Lesao.Entorse => "torcid" + o, Lesao.Exposta => "fratura exposta", _ => "quebrad" + o,
                };
            if (e.Sangra(p)) return "sangrando";
            // a etapa 2: a limpeza e a troca do curativo do dia
            if (item == Item.SprayAntisseptico) return "ferida sem antisséptico";
            if (e.Ferida(p) && !e.Protegida(p) && !e.Coberta(p))
            {
                if (e.Limpa(p)) return $"limp{o}: protege até o amanhecer";
                if (item == Item.BandagemAntisseptica) return "protege até o amanhecer";
            }
            return e.Inutil(p) ? "inutilizad" + o : $"{e.Vida(p)}%";
        }

        /// <summary>Na aba CORPO, o botão direito numa parte: os curativos da mochila que servem nela (os outros, apagados).</summary>
        void MenuDaParte(ParteDoCorpo parte)
        {
            _parteEscolhida = parte;
            int n = 0;
            foreach (var item in Curativos)
            {
                if (inventario.Quantos(item) <= 0) continue;
                n++;
                bool pode = ServeAgora(item, SemParte(item) ? PartesDoCorpo.Sortear : parte, out var aqui, out var motivo);
                string tempo = $" ({Numero(PartesDoCorpo.TempoDe(item))} s)";
                string texto = item switch
                {
                    Item.Atadura => "Enfaixar com a atadura",
                    Item.Bandagem => "Enfaixar com a bandagem",
                    Item.BandagemAntisseptica => "Enfaixar com a bandagem antisséptica",
                    Item.SprayAntisseptico => $"Borrifar o spray ({TextoDosUsos(UsosDoPrimeiro(item))})",
                    Item.Tala => "Pôr a tala",
                    Item.KitMedico => "Usar o kit médico (o corpo todo)",
                    Item.Comprimidos => "Tomar um comprimido (a dor)",
                    Item.Antibiotico => "Tomar o antibiótico (a infecção)",
                    _ => "Tomar o xarope (a febre)",
                };
                if (!PrimeiraPilha(item, out ushort id)) continue;
                var alvo = SemParte(item) ? PartesDoCorpo.Sortear : aqui;
                Opcao(pode ? texto + tempo : $"{texto} ({motivo})", pode ? () => ComecarTratamento(id, alvo) : (System.Action)null);
            }
            if (n == 0) Opcao("Nenhum curativo na mochila", null);
        }

        // ------------------------------------------------------------------ os detalhes de um curativo (a aba MOCHILA)

        /// <summary>
        /// As linhas dos detalhes de um curativo, no lugar da "Vida +N" de antes: o que ele trata e quanto leva ("Estanca 1
        /// ferida · +30% no membro · +8 de vida · regenera 5 s · leva 3 s"), e como usar.
        /// </summary>
        void LinhasDoTratamento(Item item, System.Action<string, string> linha)
        {
            string leva = Numero(PartesDoCorpo.TempoDe(item)) + " s";
            string trata = item switch
            {
                Item.Atadura => $"Estanca 1 ferida · +30% no membro · +8 de vida · regenera 5 s · leva {leva}",
                Item.Bandagem => $"Estanca 1 ferida · +20% no membro · +3 de vida · regenera 5 s · leva {leva}",
                Item.BandagemAntisseptica => $"Estanca 1 ferida · +25% no membro · +5 de vida · regenera 5 s · leva {leva}",
                Item.SprayAntisseptico => $"Limpa 1 ferida ({TextoDosUsos(Itens.UsosDoSpray)}) · enfaixe por cima · leva {leva}",
                Item.KitMedico => $"Estanca todas · +50% em cada membro · +40 de vida · regenera 10 s · leva {leva}",
                Item.Tala => $"Imobiliza o membro quebrado ou torcido · leva {leva}",
                Item.Comprimidos => "Tira a dor por 6 h do jogo (a mira treme a metade)",
                Item.Antibiotico => $"Cura a infecção: {ChancesDoAntibiotico()} · 1 dose a cada 6 h",
                _ => "Tira a febre por 4 h do jogo (não cura a infecção)",
            };
            linha("Trata", trata);
            // a etapa 2: o que cada um faz contra a infecção
            string protege = item switch
            {
                Item.BandagemAntisseptica => "Protege da infecção até o amanhecer",
                Item.KitMedico => "Protege toda ferida da infecção até o amanhecer",
                Item.Atadura or Item.Bandagem => "Só com o spray antes (sem ele, pode infeccionar)",
                Item.SprayAntisseptico => "Com uma bandagem por cima: até o amanhecer",
                _ => null,
            };
            if (protege != null) linha("Infecção", protege);
            linha("Usar", PartesDoCorpo.CorpoTodo(item) ? "botão direito: no corpo todo"
                : PartesDoCorpo.PelaBoca(item) ? "botão direito: tomar"
                : "botão direito: escolher a parte (H: o corpo)");
        }

        // ------------------------------------------------------------------ o desenho

        /// <summary>A aba CORPO, no lugar da grade: a silhueta, as etiquetas, a vida geral, o curativo de agora e o aviso.</summary>
        void Corpo(Vector2 mouse)
        {
            float u = _u;
            Ret(Encolher(Em(AreaDoCorpo), -4 * u), Painel);
            var vida = VidaDoCorpo;
            if (vida == null || !vida.IsSpawned) return;
            var e = vida.Corpo.Value;
            bool tratando = Tratando(out var t);
            var sob = PartesDoCorpo.Sortear;
            if (!MenuAberto && !_arrastando) ParteNaAbaCorpo(mouse, out sob);

            // a silhueta: a parte escolhida acesa; com um curativo sendo feito, a parte dele pulsa
            var area = Em(SilhuetaGrande);
            var realce = _parteEscolhida;
            float brilho = 1f;
            if (tratando && PartesDoCorpo.Valida(t.Parte) && !SemParte(t.Item))
            {
                realce = t.Parte;
                brilho = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
            }
            CorpoNoHud.DesenharSilhueta(area, e, realce, brilho);
            if (PartesDoCorpo.Valida(sob))
            {
                var rs = CorpoNoHud.RetanguloDaParte(area, sob);
                if (sob == ParteDoCorpo.Cabeca)
                {
                    var antes = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.15f);
                    GUI.DrawTexture(rs, Circulo());
                    GUI.color = antes;
                }
                else Ret(rs, new Color(1f, 1f, 1f, 0.15f));
                _dicaTexto = "Clique: escolher   ·   botão direito: os curativos";
            }
            for (int p = 0; p < PartesDoCorpo.Quantas; p++) EtiquetaDaParte(e, (ParteDoCorpo)p, (ParteDoCorpo)p == sob);

            // embaixo: a vida geral (um pouco mais alta que antes: a linha da infecção entrou embaixo dela)
            float x = GradeX + 6f, w = GradeMaxW - 12f, y = 384f;
            GUI.Label(Em(new Rect(x, y, 200, 20)), "VIDA GERAL", _titulo);
            float fracao = vida.vidaMaxima > 0f ? Mathf.Clamp01(vida.Vida.Value / vida.vidaMaxima) : 0f;
            var num = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleRight };
            PorCor(num, fracao > 0.35f ? CorDoTexto : Ruim);
            GUI.Label(Em(new Rect(x, y, w, 20)), $"{Mathf.CeilToInt(vida.Vida.Value)} / {Mathf.RoundToInt(vida.vidaMaxima)}", num);
            var barra = Em(new Rect(x, y + 24f, w, 8f));
            Ret(barra, new Color(0f, 0f, 0f, 0.5f));
            Ret(new Rect(barra.x, barra.y, barra.width * fracao, barra.height),
                fracao > 0.35f ? new Color(0.78f, 0.74f, 0.62f, 0.9f) : new Color(0.85f, 0.25f, 0.2f, 0.95f));

            // o analgésico, a dor ou o sangue
            string linha = null;
            var cor = TextoApagado;
            if (e.AlgumSangrando)
            {
                linha = "Sangrando: a vida geral cai até estancar (a atadura, as bandagens ou o kit).";
                cor = Ruim;
            }
            else if (e.ComAnalgesico)
            {
                linha = $"Analgésico agindo: mais {CorpoNoHud.Tempo(e.analgesico)}.";
                cor = Bom;
            }
            else if (e.ComDor)
            {
                linha = "Com dor: o analgésico tira a dor (a mira treme menos e o osso sara mais rápido).";
                cor = Ruim;
            }
            // a etapa 2: a infecção do corpo inteiro, em cima (a mais grave)
            string infeccao = LinhaDaInfeccao(e);
            float yDaLinha = 424f;
            if (infeccao != null || linha != null)
            {
                var est = new GUIStyle(_pequeno) { wordWrap = false };
                if (infeccao != null)
                {
                    PorCor(est, Ruim);
                    GUI.Label(Em(new Rect(x, yDaLinha, w, 18f)), infeccao, est);
                    yDaLinha += 18f;
                }
                if (linha != null)
                {
                    PorCor(est, cor);
                    GUI.Label(Em(new Rect(x, yDaLinha, w, 18f)), linha, est);
                }
            }

            // o curativo de agora (a barra, como a do arrombar) e o botão de parar; sem ele, as dicas
            if (tratando)
            {
                var texto = new GUIStyle(_texto) { fontStyle = FontStyle.Bold, wordWrap = false };
                PorCor(texto, Destaque);
                GUI.Label(Em(new Rect(x, 460f, w - 104f, 20f)), string.IsNullOrEmpty(t.Texto) ? "Tratando…" : t.Texto, texto);
                var b = Em(new Rect(x, 484f, w - 104f, 6f));
                Ret(Encolher(b, -1f), new Color(0f, 0f, 0f, 0.6f));
                Ret(new Rect(b.x, b.y, b.width * Mathf.Clamp01(t.Progresso), b.height), new Color(0.95f, 0.85f, 0.45f, 0.95f));
                var parar = Em(BotaoParar);
                Ret(parar, parar.Contains(mouse) ? new Color(0.62f, 0.16f, 0.12f, 0.95f) : new Color(0f, 0f, 0f, 0.35f));
                Moldura(parar, Mathf.Max(1f, Mathf.Round(u)), BordaClara);
                var est = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(13 * u) };
                GUI.Label(parar, "PARAR", est);
            }
            else
            {
                GUI.Label(Em(new Rect(x, 460f, w, 20f)), "Clique numa parte: escolher (de novo: o automático)", _texto);
                GUI.Label(Em(new Rect(x, 482f, w, 20f)), "Clique num curativo, à direita: usar   ·   botão direito na parte: opções   ·   H: esta aba", _pequeno);
            }
            Aviso(new Rect(x, 512f, w, 22f));
        }

        /// <summary>
        /// A infecção do corpo inteiro, para a linha embaixo da vida geral: "Infecção: febre — tome antibiótico (próxima dose
        /// em 3 h)"; null sem infecção.
        /// </summary>
        static string LinhaDaInfeccao(in EstadoDoCorpo e)
        {
            if (!e.Infectado) return null;
            string dose = e.doseFalta > 0 ? $"próxima dose em {PartesDoCorpo.HorasDoJogo(e.doseFalta)}"
                                          : $"{PorCento(VidaJogador.ChanceDoAntibiotico(e.Infeccao))} de cura";
            return e.Infeccao switch
            {
                1 => $"Infecção: a ferida {PartesDoCorpo.NaParte(e.OrigemDaInfeccao)} — tome antibiótico ({dose})",
                2 => e.xarope > 0 ? $"Infecção: febre (xarope: mais {CorpoNoHud.Tempo(e.xarope)}) — tome antibiótico ({dose})"
                                  : $"Infecção: febre — tome antibiótico ({dose}); o xarope tira a febre",
                _ => $"Infecção: sepse, a vida está caindo — tome antibiótico já ({dose})",
            };
        }

        // a etiqueta da parte (como as do capô): o nome e a vida, a barra e o estado em palavras
        GUIStyle _estiloDoNome, _estiloDoNumero, _estiloDoEstado, _estiloDoEstadoMenor;
        readonly GUIContent _conteudoDoEstado = new GUIContent();

        void EtiquetaDaParte(in EstadoDoCorpo e, ParteDoCorpo p, bool sob)
        {
            float u = _u;
            var r = Em(Etiqueta(p));
            bool escolhida = p == _parteEscolhida;
            Ret(r, escolhida ? new Color(0f, 0f, 0f, 0.38f) : sob ? new Color(0f, 0f, 0f, 0.3f) : new Color(0f, 0f, 0f, 0.22f));
            Moldura(r, Mathf.Max(1f, Mathf.Round((escolhida ? 2 : 1) * u)), escolhida ? Destaque : new Color(1f, 1f, 1f, sob ? 0.25f : 0.08f));
            bool inutil = e.Inutil(p);
            int vida = e.Vida(p);
            var cor = inutil ? new Color(0.62f, 0.6f, 0.56f) : Pecas.CorDoEstado(vida);
            var nome = new Rect(r.x + 8 * u, r.y + 5 * u, r.width - 16 * u, 20 * u);
            // o nome um pouco menor que o título, para o "100%" não encostar no "BRAÇO ESQ." (os estilos guardados: nada
            // de GUIStyle novo por quadro)
            if (_estiloDoNome == null || _estiloDoNome.fontSize != Mathf.RoundToInt(_titulo.fontSize * 0.86f))
            {
                _estiloDoNome = new GUIStyle(_titulo) { fontSize = Mathf.RoundToInt(_titulo.fontSize * 0.86f), alignment = TextAnchor.MiddleLeft };
                _estiloDoNumero = new GUIStyle(_estiloDoNome) { alignment = TextAnchor.MiddleRight };
            }
            GUI.Label(nome, PartesDoCorpo.NomeCurto(p), _estiloDoNome);
            PorCor(_estiloDoNumero, cor);
            GUI.Label(nome, $"{vida}%", _estiloDoNumero);
            var barra = new Rect(r.x + 8 * u, r.y + 28 * u, r.width - 16 * u, 8 * u);
            Ret(barra, new Color(0f, 0f, 0f, 0.55f));
            if (inutil)
            {
                Ret(barra, new Color(0.3f, 0.3f, 0.29f, 0.95f));
                float passo = 10f * u;
                GUI.DrawTextureWithTexCoords(barra, IconesDoCorpo.Riscas, new Rect(0f, 0f, barra.width / passo, barra.height / passo));
            }
            else Ret(new Rect(barra.x, barra.y, barra.width * vida / 100f, barra.height), cor);
            var palavras = CorpoNoHud.EstadoDaParte(e, p, out bool grave);
            if (_estiloDoEstado == null || _estiloDoEstado.fontSize != _pequeno.fontSize)
            {
                _estiloDoEstado = new GUIStyle(_pequeno) { wordWrap = true };
                _estiloDoEstadoMenor = new GUIStyle(_pequeno) { wordWrap = true, fontSize = Mathf.Max(8, Mathf.RoundToInt(_pequeno.fontSize * 0.85f)) };
            }
            // a etapa 2 pôs mais coisas na ferida (protegida, o dia dela): o que não couber vai na letra menor
            var areaDoEstado = new Rect(r.x + 8 * u, r.y + 42 * u, r.width - 16 * u, r.height - 44 * u);
            _conteudoDoEstado.text = palavras;
            var est = _estiloDoEstado.CalcHeight(_conteudoDoEstado, areaDoEstado.width) > areaDoEstado.height ? _estiloDoEstadoMenor : _estiloDoEstado;
            PorCor(est, grave ? Ruim : vida < 100 || e.Faixa(p) || e.Tala(p) || e.Ferida(p) ? CorDoTexto : TextoApagado);
            GUI.Label(areaDoEstado, _conteudoDoEstado, est);
        }

        /// <summary>
        /// À direita, no lugar dos detalhes (a aba CORPO): a parte escolhida (o nome, a vida e o que ela tem, com o que
        /// fazer) e a coluna "CURATIVOS NA MOCHILA".
        /// </summary>
        void ColunaDoCorpo(Vector2 mouse)
        {
            float u = _u;
            var nome = Em(AreaDoNome);
            var resto = Em(new Rect(AreaDaFoto.x, AreaDaFoto.y, AreaDaFoto.width, AreaDosDetalhes.yMax - AreaDaFoto.y));
            Ret(nome, Painel);
            Ret(resto, Painel);
            var vida = VidaDoCorpo;
            if (vida == null || !vida.IsSpawned) return;
            var e = vida.Corpo.Value;
            var p = _parteEscolhida;
            var faixaDoNome = new Rect(nome.x + 10 * u, nome.y, nome.width - 20 * u, nome.height);
            if (PartesDoCorpo.Valida(p))
            {
                GUI.Label(faixaDoNome, PartesDoCorpo.Nome(p).ToUpperInvariant(), _nome);
                var num = new GUIStyle(_nome) { alignment = TextAnchor.MiddleRight };
                PorCor(num, e.Inutil(p) ? Ruim : Pecas.CorDoEstado(e.Vida(p)));
                GUI.Label(faixaDoNome, $"{e.Vida(p)}%", num);
            }
            else
            {
                PorCor(_nome, TextoApagado);
                GUI.Label(faixaDoNome, "AUTOMÁTICO", _nome);
                PorCor(_nome, CorDoTexto);
            }
            // o que a parte tem e o que fazer (até onde couber, antes do título da coluna)
            LinhasDaParte(e, p);
            float x = resto.x + 12 * u, w = resto.width - 24 * u, y = resto.y + 8 * u, limite = Em(new Rect(0, CurativosY - 30f, 0, 0)).y;
            var est = new GUIStyle(_texto) { wordWrap = true };
            foreach (var (texto, cor) in _linhasDaParte)
            {
                var conteudo = new GUIContent(texto);
                float h = est.CalcHeight(conteudo, w);
                if (y + h > limite) break;
                PorCor(est, cor);
                GUI.Label(new Rect(x, y, w, h), conteudo, est);
                y += h + 3 * u;
            }
            // a coluna dos curativos
            GUI.Label(Em(new Rect(AreaDoNome.x + 10f, CurativosY - 26f, AreaDoNome.width - 20f, 22f)), "CURATIVOS NA MOCHILA", _titulo);
            var nomeDoCurativo = new GUIStyle(_texto) { fontStyle = FontStyle.Bold, wordWrap = false };
            var qtd = new GUIStyle(nomeDoCurativo) { alignment = TextAnchor.UpperRight };
            // (só os que estão na mochila; com mais de 6, as linhas apertam e o texto de baixo fica numa linha só)
            CurativosNaTela();
            bool apertado = PassoDosCurativos < PassoDoCurativo - 0.5f;
            var baixo = new GUIStyle(_pequeno) { wordWrap = !apertado };
            if (_curativosNaTela.Count == 0)
            {
                var vazio = new GUIStyle(_pequeno) { wordWrap = true };
                PorCor(vazio, TextoApagado);
                GUI.Label(Em(new Rect(AreaDoNome.x + 10f, CurativosY, AreaDoNome.width - 20f, 60f)),
                          "Nenhum curativo na mochila. Procure na farmácia e no hospital, ou compre de um comerciante.", vazio);
            }
            for (int i = 0; i < _curativosNaTela.Count; i++)
            {
                var item = _curativosNaTela[i];
                var r = LinhaDoCurativo(i);
                int n = inventario.Quantos(item);
                bool serve = ServeAgora(item, out var parte, out var motivo);
                bool sobre = r.Contains(mouse) && !MenuAberto && !_arrastando;
                Ret(r, serve ? (sobre ? new Color(0.62f, 0.6f, 0.56f, 0.95f) : new Color(0.5f, 0.48f, 0.45f, 0.9f)) : new Color(0f, 0f, 0f, 0.22f));
                Moldura(r, Mathf.Max(1f, Mathf.Round((sobre && serve ? 2 : 1) * u)), sobre && serve ? Destaque : new Color(1f, 1f, 1f, serve ? 0.12f : 0.05f));
                float alfa = serve ? 1f : 0.45f;
                var caixa = new Rect(r.x + 4 * u, r.y + 4 * u, r.height - 8 * u, r.height - 8 * u);
                var fundo = FundoDeRemedio;
                fundo.a *= alfa;
                Ret(caixa, fundo);
                var antes = GUI.color;
                var icone = inventario.IconeDe(item);
                if (icone != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, alfa);
                    GUI.DrawTexture(Encolher(caixa, 2 * u), icone, ScaleMode.ScaleToFit);
                }
                else
                {
                    // sem o ícone (a tala e a bandagem antisséptica, até o loot.py gerar os delas): o desenho do corpo
                    GUI.color = new Color(0.97f, 0.95f, 0.9f, alfa);
                    GUI.DrawTexture(Encolher(caixa, 6 * u), item == Item.Tala ? IconesDoCorpo.Tala : PartesDoCorpo.Enfaixa(item) ? IconesDoCorpo.Faixa : IconesDoCorpo.Cruz, ScaleMode.ScaleToFit);
                }
                GUI.color = antes;
                float tx = caixa.xMax + 8 * u, tw = r.xMax - 8 * u - tx;
                // o spray: os usos que sobram no primeiro (é ele que o clique gasta)
                int usos = UsosDoPrimeiro(item);
                string quantos = n <= 0 ? "—" : usos >= 0 ? (n > 1 ? $"×{n} · {TextoDosUsos(usos)}" : TextoDosUsos(usos)) : $"×{n}";
                float larguraDoQuantos = qtd.CalcSize(new GUIContent(quantos)).x + 6 * u;
                float topo = apertado ? 2 * u : 4 * u;
                PorCor(nomeDoCurativo, serve ? CorDoTexto : TextoApagado);
                GUI.Label(new Rect(tx, r.y + topo, Mathf.Max(20 * u, tw - larguraDoQuantos), 20 * u), Maiuscula(Itens.Nome(item)), nomeDoCurativo);
                PorCor(qtd, n > 0 ? CorDoTexto : TextoApagado);
                GUI.Label(new Rect(tx, r.y + topo, tw, 20 * u), quantos, qtd);
                PorCor(baixo, serve ? (sobre ? Destaque : TextoApagado) : new Color(0.9f, 0.6f, 0.52f, 0.85f));
                float yDeBaixo = apertado ? 19 * u : 24 * u;
                GUI.Label(new Rect(tx, r.y + yDeBaixo, tw, Mathf.Max(12 * u, r.height - yDeBaixo - 1 * u)), serve ? CliqueDo(e, item, parte) : motivo ?? "", baixo);
            }
        }

        // as linhas do que a parte tem e do que fazer (a parte escolhida; nenhuma: como o automático escolhe)
        void LinhasDaParte(in EstadoDoCorpo e, ParteDoCorpo p)
        {
            _linhasDaParte.Clear();
            if (!PartesDoCorpo.Valida(p))
            {
                _linhasDaParte.Add(("Sem parte escolhida, o curativo vai onde mais precisa: o que sangra primeiro.", TextoApagado));
                _linhasDaParte.Add(("Clique numa parte do corpo para escolher.", TextoApagado));
                return;
            }
            string o = PartesDoCorpo.NomeComArtigo(p)[0] == 'a' ? "a" : "o";
            if (e.Inutil(p))
            {
                // (o mesmo que o jogo faz: com a tala, o braço e a perna funcionam com dificuldade)
                string efeito = PartesDoCorpo.EhBraco(p)
                    ? (e.BracoImpede(p) ? "não segura arma de duas mãos" : "segura e atira com dificuldade (a tala)")
                    : PartesDoCorpo.EhPerna(p)
                        ? (PartesDoCorpo.MancaForte(e, p) ? "manca forte" : "manca, mas corre e pula com a tala")
                        : p == ParteDoCorpo.Cabeca ? "a vista escurece" : "anda mais devagar";
                _linhasDaParte.Add(($"Inutilizad{o}: {efeito}. O kit médico devolve 50%.", Ruim));
            }
            // a etapa 2: a infecção que veio daqui (a primeira coisa: é a que mata)
            if (e.Infectado && e.OrigemDaInfeccao == p)
                _linhasDaParte.Add(($"Infeccionad{o}: tome antibiótico ({PorCento(VidaJogador.ChanceDoAntibiotico(e.Infeccao))} de cura agora).", Ruim));
            if (e.Sangra(p)) _linhasDaParte.Add(("Sangrando: a atadura, as bandagens ou o kit estancam.", Ruim));
            else if (e.Faixa(p) && e.FeridaFalta(p) > 0) _linhasDaParte.Add(($"Enfaixad{o}: a ferida fecha em {CorpoNoHud.Tempo(e.FeridaFalta(p))}.", CorDoTexto));
            // a limpeza da ferida, nos 3 dias em que ela pode infeccionar
            if (e.Ferida(p) && !(e.Infectado && e.OrigemDaInfeccao == p))
            {
                string dia = CorpoNoHud.DiaDaFerida(e, p);
                if (e.Coberta(p)) _linhasDaParte.Add(("O antibiótico cobriu a ferida: ela não infecciona mais.", Bom));
                else if (e.Dias(p) >= 3) _linhasDaParte.Add(("Passou dos 3 dias: a ferida não infecciona mais.", TextoApagado));
                else if (e.Protegida(p)) _linhasDaParte.Add(($"Protegid{o} até o amanhecer ({dia}). Depois, troque o curativo antisséptico.", Bom));
                else if (e.Limpa(p)) _linhasDaParte.Add(($"Limp{o} ({dia}): enfaixe agora e fica protegid{o} até o amanhecer.", CorDoTexto));
                else _linhasDaParte.Add(($"Sem antisséptico ({dia}): pode infeccionar no amanhecer. Use a bandagem antisséptica, ou o spray e uma bandagem.", CorpoNoHud.LinhaMedia));
            }
            var l = e.LesaoDe(p);
            if (l != Lesao.Nenhuma)
            {
                if (e.Tala(p))
                {
                    int falta = e.SararFalta(p) * (e.ComAnalgesico ? 1 : 2);
                    _linhasDaParte.Add(($"{Maiuscula(PartesDoCorpo.NomeDaLesao(l))} com tala: sara em {CorpoNoHud.Tempo(falta)}"
                                        + (e.ComAnalgesico ? "." : " (com o analgésico, na metade do tempo)."), Bom));
                }
                else if (l == Lesao.Entorse) _linhasDaParte.Add(("Entorse: ponha uma tala (e um analgésico tira a dor).", Ruim));
                else if (l == Lesao.Fratura) _linhasDaParte.Add(("Fratura: ponha uma tala. Sem ela, o osso não cola.", Ruim));
                else _linhasDaParte.Add(("Fratura exposta: estanque o sangue e ponha a tala.", Ruim));
            }
            if (_linhasDaParte.Count == 0)
                _linhasDaParte.Add(e.Dano(p) > 0 ? ($"Machucad{o} ({e.Vida(p)}%): a atadura devolve 30%.", CorDoTexto) : ($"Inteir{o}.", TextoApagado));
        }
    }
}
