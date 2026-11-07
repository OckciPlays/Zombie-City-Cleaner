using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// No dono (06/11/2026, a árvore de habilidades): o XP ganho aparece embaixo da mira, pequeno e sumindo ("+3 XP ·
    /// curativo"; o mesmo motivo seguido soma na mesma linha), e o nível novo aparece maior ("NÍVEL 5 · +1 ponto · K:
    /// habilidades"). Sem caixa atrás do texto: só a sombra.
    /// </summary>
    public class XpNaTela : MonoBehaviour
    {
        const float Dura = 2.6f, Some = 0.8f, NivelDura = 4.5f;

        class Linha
        {
            public string motivo;
            public int xp;
            public float ate;
        }

        readonly List<Linha> _linhas = new List<Linha>();
        int _nivel;
        float _nivelAte;
        string _aprendeu;
        float _aprendeuAte;
        GUIStyle _estilo, _grande;
        float _u;

        void OnEnable()
        {
            InventarioJogador.GanhouXp += AoGanhar;
            InventarioJogador.Aprendeu += AoAprender;
        }

        void OnDisable()
        {
            InventarioJogador.GanhouXp -= AoGanhar;
            InventarioJogador.Aprendeu -= AoAprender;
        }

        void AoGanhar(int xp, string motivo, int nivel)
        {
            // o mesmo motivo, ainda na tela: soma (matando zumbis seguidos, uma linha só)
            foreach (var l in _linhas)
                if (l.motivo == motivo && l.ate - Time.unscaledTime > Some)
                {
                    l.xp += xp;
                    l.ate = Time.unscaledTime + Dura;
                    goto nivel;
                }
            _linhas.Add(new Linha { motivo = motivo, xp = xp, ate = Time.unscaledTime + Dura });
            if (_linhas.Count > 4) _linhas.RemoveAt(0);
        nivel:
            if (nivel > 0)
            {
                _nivel = nivel;
                _nivelAte = Time.unscaledTime + NivelDura;
            }
        }

        void AoAprender(Habilidade h)
        {
            _aprendeu = $"Aprendeu: {Habilidades.Nome(h)}";
            _aprendeuAte = Time.unscaledTime + 2.5f;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (MenuOnline.Aberto || MenuOnline.Cinema || ModoVideo.Ligado || GravacaoNoMundo.SemHud) return;
            float agora = Time.unscaledTime;
            _linhas.RemoveAll(l => l.ate <= agora);
            bool algo = _linhas.Count > 0 || _nivelAte > agora || _aprendeuAte > agora;
            if (!algo) return;
            float u = Screen.height / 720f;
            if (_estilo == null || !Mathf.Approximately(u, _u))
            {
                _u = u;
                // (cópias: o EstilosDoHud devolve o mesmo objeto a todos, e a cor muda aqui)
                _estilo = new GUIStyle(EstilosDoHud.Rotulo(Mathf.RoundToInt(15 * u), FontStyle.Bold, TextAnchor.MiddleCenter));
                _grande = new GUIStyle(EstilosDoHud.Rotulo(Mathf.RoundToInt(24 * u), FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            float cx = Screen.width * 0.5f, y = Screen.height * 0.5f + 70 * u;
            if (_nivelAte > agora)
            {
                float a = Mathf.Clamp01((_nivelAte - agora) / Some);
                Texto(new Rect(cx - 300 * u, y, 600 * u, 34 * u), $"NÍVEL {_nivel}  ·  +1 ponto  ·  K: habilidades", _grande,
                      new Color(0.98f, 0.82f, 0.36f, a));
                y += 36 * u;
            }
            if (_aprendeuAte > agora)
            {
                float a = Mathf.Clamp01((_aprendeuAte - agora) / Some);
                Texto(new Rect(cx - 300 * u, y, 600 * u, 24 * u), _aprendeu, _estilo, new Color(0.55f, 0.9f, 0.55f, a));
                y += 24 * u;
            }
            foreach (var l in _linhas)
            {
                float a = Mathf.Clamp01((l.ate - agora) / Some);
                Texto(new Rect(cx - 300 * u, y, 600 * u, 22 * u), string.IsNullOrEmpty(l.motivo) ? $"+{l.xp} XP" : $"+{l.xp} XP  ·  {l.motivo}",
                      _estilo, new Color(0.95f, 0.93f, 0.85f, 0.9f * a));
                y += 22 * u;
            }
        }

        static void Texto(Rect r, string texto, GUIStyle estilo, Color cor)
        {
            var antes = estilo.normal.textColor;
            estilo.normal.textColor = new Color(0f, 0f, 0f, cor.a * 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), texto, estilo);
            estilo.normal.textColor = cor;
            GUI.Label(r, texto, estilo);
            estilo.normal.textColor = antes;
        }
    }
}
