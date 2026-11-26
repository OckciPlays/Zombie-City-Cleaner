using UnityEngine;
using ZombieCityCleaner.Music;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel da trilha sonora (a pedido em 29/10/2026: "precisamos ver na prática"), embaixo do painel dos zumbis: o
    /// que está tocando e o nível do perigo, ligar e desligar a trilha, o volume dela, forçar um nível de perigo (sem
    /// precisar de zumbis), o silêncio curto (15 a 30 s, para não esperar), começar um trecho agora, e o amanhecer e a
    /// morte para ouvir. Só nesta máquina: cada jogador tem a sua música (Music/MaestroDaTrilha).
    /// Ao lado (04/11/2026, com as regras novas do perigo: "toda hora a música entra"), o quadro "REGRAS": a recarga, as
    /// entradas nos últimos 5 min (n/3), quando o "te viram" fica livre, a confirmação em andamento (x/2 s), os zumbis que
    /// te caçam e os que brigam com outro, e o que já aconteceu na partida (para contar as entradas em 10 min). A SOMBRA
    /// aparece na primeira linha.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        static readonly float[] VolumesDaTrilha = { 0.25f, 0.5f, 0.7f, 0.85f, 1f };

        GUIStyle _textoDasRegras;
        int _tipoDoAmbiente;
        float _uDasRegras;
        static readonly GUIContent _conteudoDaTrilha = new GUIContent();
        static readonly GUIContent _conteudoDasRegras = new GUIContent();

        Rect PainelDaTrilha(float u, Rect emCima)
        {
            // o texto primeiro (26/11/2026, a pedido do Pedro: os textos cortados pela metade): a caixa tinha 3 linhas, mas a
            // do perigo e a do ambiente quebram (em 1920x1080, sempre), e a última saía cortada ou sumia; agora o painel cresce
            // com o texto (e o quadro REGRAS ao lado, junto)
            var m = MaestroDaTrilha.Instancia;
            string ambiente = m == null ? "" : $"\nambiente: {(m.UltimoAmbiente.Length > 0 ? m.UltimoAmbiente : "nada ainda")}{(m.Dentro > 0.5f ? " · dentro" : "")}";
            _conteudoDaTrilha.text = m == null ? "Sem a trilha (falta o TrilhaDoJogo.asset)." : m.Situacao + ambiente;
            float alturaDoTexto = Mathf.Max(56 * u, _texto.CalcHeight(_conteudoDaTrilha, emCima.width - 24 * u));
            var area = new Rect(emCima.x, emCima.yMax + 8 * u, emCima.width, 270 * u + alturaDoTexto - 56 * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "TRILHA (TESTE)", _titulo);
            y += 24 * u;
            GUI.Label(new Rect(x, y, w, alturaDoTexto), _conteudoDaTrilha, _texto);
            y += alturaDoTexto;
            if (m == null) return area;
            var regras = QuadroDasRegras(u, area, m);

            // (26/11/2026, a pedido do Pedro: os textos cortados ...) nos botões em par, a letra menor no que passa do botão
            // ("Ambiente: DESLIGADO" em duas linhas de 10, que cabem nos 26)
            bool ligada = MaestroDaTrilha.Ligada;
            string trilha = ligada ? "Trilha: LIGADA" : "Trilha: DESLIGADA";
            if (GUI.Button(new Rect(x, y, meio, 26 * u), trilha, BotaoQueCabe(trilha, meio)))
            {
                MaestroDaTrilha.Ligada = !ligada;
                Avisar(!ligada ? "A trilha volta (o vento, e a música daqui a pouco)." : "A trilha some devagar.");
            }
            float volume = MaestroDaTrilha.Volume;
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), $"Volume: {Mathf.RoundToInt(volume * 100f)}%", _botao))
            {
                int i = 0;
                while (i < VolumesDaTrilha.Length && VolumesDaTrilha[i] <= volume + 0.01f) i++;
                MaestroDaTrilha.Volume = VolumesDaTrilha[i % VolumesDaTrilha.Length];
                Avisar($"A trilha a {Mathf.RoundToInt(MaestroDaTrilha.Volume * 100f)}% (só a música; o resto do jogo não muda).");
            }
            y += 30 * u;
            int forcado = MaestroDaTrilha.NivelForcado;
            string nomeDoNivel = forcado < 0 ? "Perigo: dos zumbis" : $"Perigo: sempre {forcado}";
            if (GUI.Button(new Rect(x, y, meio, 26 * u), nomeDoNivel, BotaoQueCabe(nomeDoNivel, meio)))
            {
                MaestroDaTrilha.NivelForcado = forcado >= 3 ? -1 : forcado + 1;
                int n = MaestroDaTrilha.NivelForcado;
                // forçado, as confirmações são puladas (04/11/2026); o orçamento cheio também não segura
                Avisar(n < 0 ? "O nível volta a ser o dos zumbis em volta." :
                       n == 0 ? "Nível 0: sem perigo (o perigo acaba depois da graça de 6 s, no compasso)." :
                       n == 1 ? "Nível 1: a tensão (no silêncio, a sombra, se ela estiver livre; na exploração, nada)." :
                       n == 2 ? "Nível 2: a perseguição, na hora (o \"te viram\" só se ele estiver livre)." : "Nível 3: a horda, na hora.");
            }
            bool curto = MaestroDaTrilha.SilencioCurto;
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), curto ? "Silêncio: curto" : "Silêncio: normal", _botao))
            {
                MaestroDaTrilha.SilencioCurto = !curto;
                Avisar(!curto ? "Entre um trecho e outro, de 15 a 30 s de vento (para testar)." : "Entre um trecho e outro, de 1 a 3 min de vento.");
            }
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), "Música agora", _botao))
                Avisar(m.ComecarAgora() ? "Começa um trecho agora." : "Já tem música tocando (ou o perigo).");
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Amanhecer agora", BotaoQueCabe("Amanhecer agora", meio)))
            {
                m.TestarAmanhecer();
                Avisar("O amanhecer (no meio de uma perseguição, ele espera o perigo acabar).");
            }
            y += 30 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Morte agora (só a música)", _botao))
            {
                m.TestarMorte();
                Avisar("A morte: tudo some e toca a morte; depois o vento.");
            }
            y += 30 * u;
            // o ambiente (a pedido em 29/10/2026; desde 20/11/2026 só os gravados): os pássaros, a coruja, os lobos, o galho e
            // os grilos
            bool ambienteLigado = MaestroDaTrilha.AmbienteLigado;
            string nomeDoAmbiente = ambienteLigado ? $"Ambiente: {Mathf.RoundToInt(MaestroDaTrilha.VolumeDoAmbiente * 100f)}%" : "Ambiente: DESLIGADO";
            if (GUI.Button(new Rect(x, y, meio, 26 * u), nomeDoAmbiente, BotaoQueCabe(nomeDoAmbiente, meio)))
            {
                MaestroDaTrilha.AmbienteLigado = !ambienteLigado;
                Avisar(!ambienteLigado ? "Os sons de ambiente voltam." : "Sem os sons de ambiente (a música e o vento continuam).");
            }
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Ambiente agora", _botao))
            {
                m.AmbienteAgora();
                Avisar("Um som de ambiente agora (o que caberia aqui: de dia ou de noite, dentro ou fora).");
            }
            // (19/11/2026) cada tipo de som do ambiente, no lugar onde ele tocaria: os pássaros, a coruja, os lobos (o uivo) e o
            // galho (20/11/2026: os feitos em código, a janela, a porta, a placa, a lata, a madeira e os corvos, saíram)
            y += 30 * u;
            string tipoDoAmbiente = MaestroDaTrilha.TiposDoAmbiente[_tipoDoAmbiente % MaestroDaTrilha.TiposDoAmbiente.Length];
            string trocarOTipo = $"Tipo: {tipoDoAmbiente} (trocar)";
            if (GUI.Button(new Rect(x, y, meio, 26 * u), trocarOTipo, BotaoQueCabe(trocarOTipo, meio))) _tipoDoAmbiente++;
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Tocar esse tipo", _botao))
            {
                m.AmbienteDoTipo(tipoDoAmbiente);
                Avisar($"O ambiente: {tipoDoAmbiente}, no lugar onde ele tocaria.");
            }
            return regras.width > 0f ? Rect.MinMaxRect(area.xMin, area.yMin, regras.xMax, Mathf.Max(area.yMax, regras.yMax)) : area;
        }

        // o quadro "REGRAS" ao lado do painel da trilha (só texto): embaixo dos zumbis não cabe mais nada na tela
        Rect QuadroDasRegras(float u, Rect painel, MaestroDaTrilha m)
        {
            float largura = Mathf.Min(170 * u, Screen.width - painel.xMax - 12 * u - _barraDaColuna);   // (e a barra da coluna, 13/11/2026)
            if (largura < 110 * u) return new Rect(painel.xMax, painel.y, 0f, 0f);
            if (_textoDasRegras == null || !Mathf.Approximately(_uDasRegras, u))
            {
                _uDasRegras = u;
                _textoDasRegras = new GUIStyle(_texto) { fontSize = Mathf.RoundToInt(11 * u) };
            }
            // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) o quadro vai até o fim das regras: estreito (com
            // as colunas do Lakewood, uns 118 de largura), o texto quebra em quase 20 linhas, e o fim saía cortado no pé do
            // quadro, da altura do painel da trilha
            _conteudoDasRegras.text = m.Regras;
            float alturaDasRegras = _textoDasRegras.CalcHeight(_conteudoDasRegras, largura - 20 * u);
            var area = new Rect(painel.xMax + 8 * u, painel.y, largura, Mathf.Max(painel.height, 8 * u + 24 * u + alturaDasRegras + 6 * u));
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;
            float x = area.x + 10 * u, w = area.width - 20 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "REGRAS", _titulo);
            y += 24 * u;
            GUI.Label(new Rect(x, y, w, area.yMax - y - 6 * u), _conteudoDasRegras, _textoDasRegras);
            return area;
        }
    }
}
