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
        float _uDasRegras;

        Rect PainelDaTrilha(float u, Rect emCima)
        {
            var area = new Rect(emCima.x, emCima.yMax + 8 * u, emCima.width, 240 * u);
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
            var m = MaestroDaTrilha.Instancia;
            string ambiente = m == null ? "" : $"\nambiente: {(m.UltimoAmbiente.Length > 0 ? m.UltimoAmbiente : "nada ainda")}{(m.Dentro > 0.5f ? " · dentro" : "")}";
            GUI.Label(new Rect(x, y, w, 56 * u), m == null ? "Sem a trilha (falta o TrilhaDoJogo.asset)." : m.Situacao + ambiente, _texto);
            y += 56 * u;
            if (m == null) return area;
            var regras = QuadroDasRegras(u, area, m);

            bool ligada = MaestroDaTrilha.Ligada;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), ligada ? "Trilha: LIGADA" : "Trilha: DESLIGADA", _botao))
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
            if (GUI.Button(new Rect(x, y, meio, 26 * u), nomeDoNivel, _botao))
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
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Amanhecer agora", _botao))
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
            // o ambiente (a pedido em 29/10/2026): os grilos, os cachorros, a janela e a placa com o vento, os corvos
            bool ambienteLigado = MaestroDaTrilha.AmbienteLigado;
            if (GUI.Button(new Rect(x, y, meio, 26 * u), ambienteLigado ? $"Ambiente: {Mathf.RoundToInt(MaestroDaTrilha.VolumeDoAmbiente * 100f)}%" : "Ambiente: DESLIGADO", _botao))
            {
                MaestroDaTrilha.AmbienteLigado = !ambienteLigado;
                Avisar(!ambienteLigado ? "Os sons de ambiente voltam." : "Sem os sons de ambiente (a música e o vento continuam).");
            }
            if (GUI.Button(new Rect(x2, y, meio, 26 * u), "Ambiente agora", _botao))
            {
                m.AmbienteAgora();
                Avisar("Um som de ambiente agora (o que caberia aqui: de dia ou de noite, dentro ou fora).");
            }
            return regras.width > 0f ? Rect.MinMaxRect(area.xMin, area.yMin, regras.xMax, Mathf.Max(area.yMax, regras.yMax)) : area;
        }

        // o quadro "REGRAS" ao lado do painel da trilha (só texto): embaixo dos zumbis não cabe mais nada na tela
        Rect QuadroDasRegras(float u, Rect painel, MaestroDaTrilha m)
        {
            float largura = Mathf.Min(170 * u, Screen.width - painel.xMax - 12 * u - _barraDaColuna);   // (e a barra da coluna, 13/11/2026)
            if (largura < 110 * u) return new Rect(painel.xMax, painel.y, 0f, 0f);
            var area = new Rect(painel.xMax + 8 * u, painel.y, largura, painel.height);
            if (_textoDasRegras == null || !Mathf.Approximately(_uDasRegras, u))
            {
                _uDasRegras = u;
                _textoDasRegras = new GUIStyle(_texto) { fontSize = Mathf.RoundToInt(11 * u) };
            }
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;
            float x = area.x + 10 * u, w = area.width - 20 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "REGRAS", _titulo);
            y += 24 * u;
            GUI.Label(new Rect(x, y, w, area.yMax - y - 6 * u), m.Regras, _textoDasRegras);
            return area;
        }
    }
}
