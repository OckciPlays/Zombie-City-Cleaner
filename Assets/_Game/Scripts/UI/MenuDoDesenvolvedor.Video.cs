using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel VÍDEO do F1 (03/11/2026, a pedido: gravar jogando, na partida de sempre), embaixo do painel principal: o F8
    /// grava e para (<see cref="GravacaoNoMundo"/>; só no Editor, onde está o Unity Recorder), em pé ou deitado, com nada,
    /// a mira ou tudo na tela, a 60 ou a 30 quadros. Gravando em pé, a câmera dos braços se ajusta para a arma caber.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        static readonly string[] Formatos = { "Em pé (Reels)", "Deitado" };
        static readonly string[] NasTelas = { "Nada", "A mira", "Tudo" };
        bool _bracosAjustados;

        void AtalhoDoVideo(Keyboard teclado)
        {
            if (!Application.isEditor || teclado == null || MenuOnline.Aberto) return;
            if (teclado.f8Key.wasPressedThisFrame) GravacaoNoMundo.Pedido = !GravacaoNoMundo.Pedido;
        }

        void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || (!GravacaoNoMundo.Gravando && !_bracosAjustados)) return;
            // gravando, a câmera dos braços na tela em pé; depois, de volta à do jogo (a terceira pessoa e o carro só ligam
            // e desligam a câmera, sem mexer no campo nem no lugar dela)
            var bracos = GetComponent<JogadorRede>().cameraBracos;
            _bracosAjustados = GravacaoNoMundo.Gravando;
            if (_bracosAjustados) GravacaoNoMundo.BracosEmPe(bracos);
            else GravacaoNoMundo.BracosEmPe(bracos, false);
        }

        Rect PainelDoVideo(float u, Rect emCima)
        {
            var area = new Rect(emCima.x, emCima.yMax + 8 * u, emCima.width, 214 * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "VÍDEO  (F8 grava e para)", _titulo);
            y += 26 * u;
            if (!Application.isEditor)
            {
                GUI.Label(new Rect(x, y, w, 40 * u), "A gravação é do Unity Recorder: só no Editor.", _texto);
                return area;
            }
            bool gravando = GravacaoNoMundo.Gravando;
            string botao = gravando
                ? $"■  Parar  ({Mathf.FloorToInt((Time.unscaledTime - GravacaoNoMundo.Desde) / 60f)}:{Mathf.FloorToInt((Time.unscaledTime - GravacaoNoMundo.Desde) % 60f):00})"
                : GravacaoNoMundo.Pedido ? "Começando…" : "●  Gravar";
            if (GUI.Button(new Rect(x, y, w, 28 * u), botao, _botao)) GravacaoNoMundo.Pedido = !GravacaoNoMundo.Pedido;
            y += 34 * u;

            // as escolhas não mudam no meio de uma gravação
            GUI.enabled = !GravacaoNoMundo.Pedido && !gravando;
            float meio = (w - 6 * u) / 2f, terco = (w - 12 * u) / 3f;
            int formato = GravacaoNoMundo.EmPe ? 0 : 1;
            for (int i = 0; i < 2; i++)
                if (GUI.Toggle(new Rect(x + i * (meio + 6 * u), y, meio, 26 * u), formato == i, Formatos[i], _botao) && formato != i)
                    GravacaoNoMundo.EmPe = i == 0;
            y += 30 * u;
            GUI.Label(new Rect(x, y + 3 * u, w, 20 * u), "Na tela, gravando:", _texto);
            y += 22 * u;
            int naTela = (int)GravacaoNoMundo.NaTela;
            for (int i = 0; i < 3; i++)
                if (GUI.Toggle(new Rect(x + i * (terco + 6 * u), y, terco, 26 * u), naTela == i, NasTelas[i], _botao) && naTela != i)
                    GravacaoNoMundo.NaTela = (GravacaoNoMundo.Tela)i;
            y += 30 * u;
            int quadros = GravacaoNoMundo.Quadros;
            for (int i = 0; i < 2; i++)
            {
                int q = i == 0 ? 60 : 30;
                if (GUI.Toggle(new Rect(x + i * (meio + 6 * u), y, meio, 26 * u), quadros == q, $"{q} quadros/s", _botao) && quadros != q)
                    GravacaoNoMundo.Quadros = q;
            }
            GUI.enabled = true;
            y += 32 * u;
            string onde = GravacaoNoMundo.Arquivo != null ? $"Último: {Path.GetFileName(GravacaoNoMundo.Arquivo)}" : "Os vídeos vão para Recordings/Mundo/.";
            GUI.Label(new Rect(x, y, w, 40 * u), onde, _texto);
            return area;
        }
    }
}
