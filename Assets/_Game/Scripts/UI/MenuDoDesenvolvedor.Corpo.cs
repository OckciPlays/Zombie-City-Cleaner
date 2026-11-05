using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel de teste do corpo (05/11/2026, a etapa 1 do sistema de vida: "a vida não se regenera mais sozinha ... cada
    /// membro do jogador terá uma vida"), na quarta aba do canto ("Corpo", ao lado de "Zumbis", "Clima" e "Gente"). Em
    /// cima, o corpo de quem joga (o <see cref="VidaJogador.ResumoDoCorpo"/>: a vida geral e as 6 partes, o que sangra, a
    /// faixa, a lesão, a tala e o analgésico); embaixo, a parte escolhida (Cab, Tro, BE, BD, PE, PD) e os testes nela: abrir
    /// uma ferida, a entorse, a fratura, a fratura exposta, inutilizar, a mordida que sangra, o tiro no braço, o golpe no chão
    /// do Brutamonte (a fratura na perna, sem sangrar), as quedas só pela conta do anfitrião (4, 6, 9 e 13 m) e as de verdade
    /// (6 e 10 m: sobe e cai, para o detector medir), sarar tudo, passar 6 h no corpo, e os curativos na mochila. A etapa 2
    /// (a limpeza e a infecção): amanhecer agora (o sorteio de toda ferida aberta), a infecção na fase (a ferida infeccionada
    /// da parte escolhida, a febre, a sepse, curar), o spray com 1 uso e, nos curativos, a bandagem antisséptica, o spray, o
    /// antibiótico, o xarope, o álcool e mais 3 bandagens (a receita da antisséptica). Os pedidos vão para o anfitrião (quem
    /// manda no corpo); a queda de verdade é aqui (o movimento é do dono).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        bool _abaDoCorpo;
        ParteDoCorpo _parteDoTeste = ParteDoCorpo.PernaE;
        static readonly string[] PartesDoTeste = { "Cab", "Tro", "BE", "BD", "PE", "PD" };
        static readonly GUIContent _conteudoDoCorpo = new GUIContent();

        // os testes (o número vai no TesteDoCorpoRpc)
        const byte CorpoFerida = 0, CorpoLesao = 1, CorpoInutilizar = 2, CorpoSarar = 3, CorpoHoras = 4, CorpoCurativos = 5,
                   CorpoQueda = 6, CorpoBrutamonte = 7, CorpoMordida = 8, CorpoTiro = 9,
                   // a etapa 2 (a limpeza e a infecção)
                   CorpoAmanhecer = 10, CorpoInfeccao = 11, CorpoSpray = 12;

        static readonly string[] FasesDaInfeccao = { "ferida", "febre", "sepse", "curar" };

        static readonly int[] QuedasDeConta = { 4, 6, 9, 13 };
        static readonly int[] QuedasDeVerdade = { 6, 10 };

        Rect PainelDoCorpo(float u, Rect aoLado)
        {
            float x0 = aoLado.xMax + 8 * u, largura = 250 * u;
            float x = x0 + 12 * u, w = largura - 24 * u;
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            float terco = (w - 12 * u) / 3f;
            const float passo = 28f, alto = 24f;
            // o texto primeiro: a altura do painel acompanha (as linhas crescem com a ferida, a lesão e o analgésico)
            var vida = GetComponent<VidaJogador>();
            string texto = vida != null && vida.IsSpawned ? vida.ResumoDoCorpo().Replace(" | ", "\n") : "sem o corpo do jogador";
            if (vida != null && vida.IsSpawned && vida.Caido.Value) texto += "\nCAÍDO: \"Sarar tudo\" renasce aqui";
            if (!VidaJogador.CorpoAnda) texto += "\nO corpo não anda agora (o Dia 0, ou o mundo carregando): os golpes só tiram da vida geral, e a queda não machuca";
            _conteudoDoCorpo.text = texto;
            float alturaDoTexto = _texto.CalcHeight(_conteudoDoCorpo, w);
            const int linhas = 11;
            const float rotuloDaInfeccao = 20f;   // a linha do rótulo "Infecção" (menor que um botão)
            float altura = 8 * u + 28 * u + alturaDoTexto + 8 * u + linhas * passo * u + rotuloDaInfeccao * u + 6 * u;
            var area = new Rect(x0, aoLado.y, largura, altura);

            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.75f, 0.32f, 0.28f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "CORPO (TESTE)", _titulo);
            y += 28 * u;
            GUI.Label(new Rect(x, y, w, alturaDoTexto), _conteudoDoCorpo, _texto);
            y += alturaDoTexto + 8 * u;

            // a parte escolhida (ligada, o botão fica apertado e com o texto amarelo: o estilo "on" do _botao)
            float sexto = (w - 5 * 4 * u) / 6f;
            for (int p = 0; p < PartesDoTeste.Length; p++)
            {
                bool escolhida = (int)_parteDoTeste == p;
                if (GUI.Toggle(new Rect(x + p * (sexto + 4 * u), y, sexto, alto * u), escolhida, PartesDoTeste[p], _botao) && !escolhida)
                    _parteDoTeste = (ParteDoCorpo)p;
            }
            y += passo * u;
            byte parte = (byte)_parteDoTeste;

            if (GUI.Button(new Rect(x, y, meio, alto * u), "Abrir ferida", _botao)) TesteDoCorpoRpc(CorpoFerida, parte, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Inutilizar", _botao)) TesteDoCorpoRpc(CorpoInutilizar, parte, 0);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, terco, alto * u), "Entorse", _botao)) TesteDoCorpoRpc(CorpoLesao, parte, (byte)Lesao.Entorse);
            if (GUI.Button(new Rect(x + terco + 6 * u, y, terco, alto * u), "Fratura", _botao)) TesteDoCorpoRpc(CorpoLesao, parte, (byte)Lesao.Fratura);
            if (GUI.Button(new Rect(x + 2f * (terco + 6 * u), y, terco, alto * u), "Exposta", _botao)) TesteDoCorpoRpc(CorpoLesao, parte, (byte)Lesao.Exposta);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Mordida (sangra)", _botao)) TesteDoCorpoRpc(CorpoMordida, parte, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Tiro no braço", _botao)) TesteDoCorpoRpc(CorpoTiro, parte, 0);
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), "Golpe no chão do Brutamonte", _botao)) TesteDoCorpoRpc(CorpoBrutamonte, parte, 0);
            y += passo * u;

            // as quedas só pela conta do anfitrião (o VidaJogador.Cair), sem subir
            float rotulo = 52 * u, quarto = (w - rotulo - 3 * 4 * u) / 4f;
            GUI.Label(new Rect(x, y + 3 * u, rotulo, 20 * u), "Queda", _texto);
            for (int k = 0; k < QuedasDeConta.Length; k++)
                if (GUI.Button(new Rect(x + rotulo + k * (quarto + 4 * u), y, quarto, alto * u), $"{QuedasDeConta[k]} m", _botao))
                    TesteDoCorpoRpc(CorpoQueda, parte, (byte)QuedasDeConta[k]);
            y += passo * u;
            // as quedas de verdade: sobe e cai (o QuedaDoJogador mede)
            float rotuloDeVerdade = 112 * u, metade = (w - rotuloDeVerdade - 4 * u) / 2f;
            GUI.Label(new Rect(x, y + 3 * u, rotuloDeVerdade, 20 * u), "Cair de verdade", _texto);
            for (int k = 0; k < QuedasDeVerdade.Length; k++)
                if (GUI.Button(new Rect(x + rotuloDeVerdade + k * (metade + 4 * u), y, metade, alto * u), $"{QuedasDeVerdade[k]} m", _botao))
                    CairDeVerdade(QuedasDeVerdade[k]);
            y += passo * u;

            if (GUI.Button(new Rect(x, y, meio, alto * u), "Sarar tudo", _botao)) TesteDoCorpoRpc(CorpoSarar, parte, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "+6 h no corpo", _botao)) TesteDoCorpoRpc(CorpoHoras, parte, 6);
            y += passo * u;

            // a etapa 2: o amanhecer (o sorteio de cada ferida aberta, mesmo nova), o spray quase no fim e a infecção na fase
            if (GUI.Button(new Rect(x, y, meio, alto * u), "Amanhecer agora", _botao)) TesteDoCorpoRpc(CorpoAmanhecer, parte, 0);
            if (GUI.Button(new Rect(x2, y, meio, alto * u), "Spray com 1 uso", _botao)) TesteDoCorpoRpc(CorpoSpray, parte, 0);
            y += passo * u;
            GUI.Label(new Rect(x, y, w, rotuloDaInfeccao * u), "Infecção (vinda da parte escolhida)", _texto);
            y += rotuloDaInfeccao * u;
            float quartoDaInfeccao = (w - 3 * 4 * u) / 4f;
            for (int k = 0; k < FasesDaInfeccao.Length; k++)
                if (GUI.Button(new Rect(x + k * (quartoDaInfeccao + 4 * u), y, quartoDaInfeccao, alto * u), FasesDaInfeccao[k], _botao))
                    TesteDoCorpoRpc(CorpoInfeccao, parte, (byte)(k < 3 ? k + 1 : 0));
            y += passo * u;
            if (GUI.Button(new Rect(x, y, w, alto * u), "Os curativos (na mochila)", _botao)) TesteDoCorpoRpc(CorpoCurativos, parte, 0);
            return area;
        }

        // no dono: sobe <altura> metros e cai (o detector de queda de verdade mede a altura, como num telhado)
        void CairDeVerdade(float altura)
        {
            var passageiro = GetComponent<PassageiroDoCarro>();
            if (passageiro != null && passageiro.NoCarro)
            {
                Avisar("Saia do carro primeiro.");
                return;
            }
            var movimento = GetComponent<MovimentoPrimeiraPessoa>();
            if (movimento == null) return;
            if (movimento.Voando) movimento.Voando = false;   // (voando, o detector desarma: a queda não contaria)
            // com um teto em cima (dentro de casa, debaixo da ponte), ele subiria dentro dele
            var cabeca = transform.position + Vector3.up * 1.8f;
            if (Physics.Raycast(cabeca, Vector3.up, altura + 0.5f, ~((1 << 2) | (1 << JogadorRede.CamadaJogador)), QueryTriggerInteraction.Ignore))
            {
                Avisar("Tem teto em cima: vá para o aberto.");
                return;
            }
            movimento.Teleportar(transform.position + Vector3.up * altura, transform.eulerAngles.y);
            QuedaDoJogador.Local?.ArmarJa();
            Avisar(VidaJogador.CorpoAnda ? $"Caindo de {altura:0} m." : $"Caindo de {altura:0} m (no Dia 0 a queda não machuca).");
        }

        // os testes do corpo, no anfitrião: a parte escolhida (byte da ParteDoCorpo) e um número (a lesão, as horas, os metros)
        [Rpc(SendTo.Server)]
        void TesteDoCorpoRpc(byte acao, byte parteDoTeste, byte valor, RpcParams parametros = default)
        {
            if (!Aceita(parametros)) return;
            var vida = GetComponent<VidaJogador>();
            if (vida == null || !vida.IsSpawned) return;
            var parte = (ParteDoCorpo)parteDoTeste;
            if (!PartesDoCorpo.Valida(parte)) return;
            string aviso = null;
            if (vida.Caido.Value && acao != CorpoSarar && acao != CorpoCurativos)
            {
                AvisoDoTesteRpc("Caído: \"Sarar tudo\" renasce aqui.", RpcTarget.Single(parametros.Receive.SenderClientId, RpcTargetUse.Temp));
                return;
            }
            bool corpoAnda = VidaJogador.CorpoAnda;
            string semCorpo = "O corpo não anda agora (o Dia 0, ou o mundo carregando).";
            switch (acao)
            {
                case CorpoFerida:
                    vida.AbrirFeridaDeTeste(parte, TipoDeGolpe.Corte);
                    break;
                case CorpoLesao:
                    if (!PartesDoCorpo.EhMembro(parte)) aviso = "A lesão é só nos braços e nas pernas.";
                    else vida.LesaoDeTeste(parte, (Lesao)Mathf.Clamp(valor, (int)Lesao.Entorse, (int)Lesao.Exposta));
                    break;
                case CorpoInutilizar:
                    vida.InutilizarDeTeste(parte);
                    aviso = $"{TelaDoInventario.Maiuscula(PartesDoCorpo.NomeComArtigo(parte))} a 0: inutilizad{(PartesDoCorpo.NomeComArtigo(parte)[0] == 'a' ? "a" : "o")}.";
                    break;
                case CorpoSarar:
                    if (vida.Caido.Value)
                    {
                        vida.RenascerAgora();
                        aviso = "Renasceu (na cama, ou no ponto de nascimento) com o corpo inteiro.";
                    }
                    else
                    {
                        vida.SararTudo();
                        vida.Curar(vida.vidaMaxima);
                        aviso = "O corpo e a vida inteiros de novo.";
                    }
                    break;
                case CorpoHoras:
                    vida.PassarHorasNoCorpo(Mathf.Max(1, (int)valor));
                    aviso = $"Passaram {Mathf.Max(1, (int)valor)} h no corpo (as feridas enfaixadas fecham, as lesões com a tala saram).";
                    break;
                case CorpoCurativos:
                    aviso = DarOsCurativos();
                    break;
                case CorpoAmanhecer:
                    vida.AmanhecerAgora();
                    aviso = "Amanheceu no corpo: cada ferida aberta foi sorteada, e a proteção do antisséptico acabou.";
                    break;
                case CorpoInfeccao:
                {
                    int fase = Mathf.Clamp(valor, 0, 3);
                    vida.InfeccaoDeTeste(fase, parte);
                    aviso = fase == 0 ? "A infecção curada."
                          : fase == 1 ? $"A ferida {PartesDoCorpo.NaParte(parte)} infeccionada (a 1ª fase)."
                          : fase == 2 ? "Com febre (a 2ª fase): o xarope alivia, o antibiótico cura."
                          : "Sepse (a 3ª fase): a vida cai até a morte. Antibiótico, agora.";
                    break;
                }
                case CorpoSpray:
                    aviso = DarOSprayQuaseNoFim();
                    break;
                case CorpoQueda:
                    if (!corpoAnda) aviso = semCorpo;
                    else
                    {
                        vida.Cair(valor);
                        aviso = $"Caiu de {valor} m (só a conta do anfitrião).";
                    }
                    break;
                case CorpoBrutamonte:
                {
                    // o golpe no chão pega nos pés: a perna escolhida, ou sorteada (quase sempre uma perna)
                    var perna = PartesDoCorpo.EhPerna(parte) ? parte : PartesDoCorpo.Sortear;
                    vida.Dano(35f, transform.position + transform.forward * 1.5f, "Esmagado pelo Brutamonte.", TipoDeGolpe.Esmagamento, perna, Lesao.Fratura);
                    if (!corpoAnda) aviso = semCorpo;
                    break;
                }
                case CorpoMordida:
                    // o Dano com a mordida só abre a ferida em 20% das vezes: aqui ela sempre abre
                    vida.AbrirFeridaDeTeste(parte, TipoDeGolpe.Mordida);
                    vida.Dano(12f, transform.position + transform.forward, "Mordido pelos zumbis.", TipoDeGolpe.Nenhum);
                    break;
                case CorpoTiro:
                {
                    var braco = PartesDoCorpo.EhBraco(parte) ? parte : ParteDoCorpo.BracoE;
                    vida.Dano(20f, transform.position + transform.forward * 8f + Vector3.up * 1.4f, "Baleado (o menu do desenvolvedor).", TipoDeGolpe.Tiro, braco);
                    if (!corpoAnda) aviso = semCorpo;
                    break;
                }
            }
            if (aviso != null) AvisoDoTesteRpc(aviso, RpcTarget.Single(parametros.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        // no anfitrião: os curativos na mochila (o que não couber vai para o chão à frente)
        string DarOsCurativos()
        {
            var inv = GetComponent<InventarioJogador>();
            if (inv == null) return null;
            bool noChao = false;
            foreach (var c in CurativosDeTeste) noChao |= DarCurativo(inv, c.item, c.qtd);
            return noChao
                ? "Os curativos: o que não coube na mochila foi para o chão."
                : $"Na mochila: {Receitas.Lista(CurativosDeTeste)} (o álcool e 3 bandagens fazem a antisséptica).";
        }

        // os curativos de teste: os da etapa 1 e os da etapa 2 (a bandagem antisséptica, o spray, os antibióticos, o xarope e,
        // para testar a receita, o álcool e mais 3 bandagens)
        static readonly Parte[] CurativosDeTeste =
        {
            new Parte(Item.Atadura, 2), new Parte(Item.Bandagem, 5), new Parte(Item.BandagemAntisseptica, 3), new Parte(Item.SprayAntisseptico, 1),
            new Parte(Item.KitMedico, 1), new Parte(Item.Tala, 2), new Parte(Item.Comprimidos, 1), new Parte(Item.Antibiotico, 2),
            new Parte(Item.Remedio, 1), new Parte(Item.Desinfetante, 1),
        };

        // no anfitrião: um spray com 1 uso só (o desgaste 80), para ver ele acabar
        string DarOSprayQuaseNoFim()
        {
            var inv = GetComponent<InventarioJogador>();
            if (inv == null) return null;
            int desgaste = 100 - Itens.DesgastePorUsoDoSpray;
            if (inv.Receber(Item.SprayAntisseptico, -1, desgaste)) return "Na mochila: um spray antisséptico com 1 uso.";
            if (ItensNoMundo.Instancia == null) return "Sem lugar na mochila.";
            Vector3 frente = transform.position + transform.forward * 1.2f;
            var ponto = Physics.Raycast(frente + Vector3.up * 1.5f, Vector3.down, out var chao, 4f, ~((1 << 2) | (1 << JogadorRede.CamadaJogador)), QueryTriggerInteraction.Ignore)
                ? chao.point : transform.position;
            ItensNoMundo.Instancia.Criar(Item.SprayAntisseptico, ponto, transform.eulerAngles.y + 90f, -1, desgaste);
            return "Sem lugar na mochila: o spray com 1 uso foi para o chão.";
        }

        // devolve se algum foi para o chão
        bool DarCurativo(InventarioJogador inv, Item item, int quantos)
        {
            int sobra;
            if (Itens.EmPilha(item)) sobra = inv.ReceberEmPilha(item, quantos);
            else
            {
                sobra = 0;
                for (int i = 0; i < quantos; i++) if (!inv.Receber(item)) sobra++;
            }
            if (sobra <= 0 || ItensNoMundo.Instancia == null || !Itens.NoJogo(item)) return false;
            // no chão, 1,2 m à frente (onde um raio de cima bater), como o F3
            Vector3 frente = transform.position + transform.forward * 1.2f;
            var ponto = Physics.Raycast(frente + Vector3.up * 1.5f, Vector3.down, out var chao, 4f, ~((1 << 2) | (1 << JogadorRede.CamadaJogador)), QueryTriggerInteraction.Ignore)
                ? chao.point : transform.position;
            if (Itens.EmPilha(item)) ItensNoMundo.Instancia.Criar(item, ponto, transform.eulerAngles.y + 90f, sobra);
            else for (int i = 0; i < sobra; i++) ItensNoMundo.Instancia.Criar(item, ponto, transform.eulerAngles.y + 90f);
            return true;
        }
    }
}
