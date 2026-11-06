using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Video
{
    // Tomada 5: o vídeo das armas (o segundo vídeo), em primeira pessoa, com quem grava jogando. O jogador nasce no salão
    // do bunker com a pistola, a mochila grande e o coldre, e o painel vem cheio: E pega cada arma (sem o limite de armas
    // no corpo, só o espaço da mochila), 1, 2, 3 e a roda trocam, e a munição não acaba. A tela mostra só a mira, a
    // munição, a dica do E e a luneta (sem as barras e os atalhos). Enter termina a tomada; parar o Play também.
    public partial class DiretorDoVideo
    {
        IEnumerator PrepararArmas()
        {
            Clima(horaDasArmas, 0.7f, 0.5f);
            ModoVideo.MostrarMira = mostrarMira;
            ModoVideo.MunicaoInfinita = true;
            ModoVideo.ArmasSemLimite = true;
            // quem filma é a câmera do jogador (com a dos braços por cima), e o ouvido é o dele
            _comCamera = false;
            var jogador = JogadorRede.Local;
            jogador.cameraMundo.enabled = true;
            jogador.cameraBracos.enabled = true;
            jogador.ouvido.enabled = true;
            // a mochila grande: as armas cabem
            jogador.Inventario.Receber(Item.MochilaGrande);
            return AnimarArmas(jogador);
        }

        /// <summary>A tela em pé corta a arma: o mesmo ajuste da gravação no mundo (<see cref="GravacaoNoMundo.BracosEmPe"/>).</summary>
        void BracosEmPe()
        {
            var jogador = JogadorRede.Local;
            if (jogador != null) GravacaoNoMundo.BracosEmPe(jogador.cameraBracos);
        }

        IEnumerator AnimarArmas(JogadorRede jogador)
        {
            while (true)
            {
                // parar o Play no meio: o jogador sai da rede antes do diretor
                if (jogador == null) yield break;
                var teclado = Keyboard.current;
                if (teclado != null && teclado.enterKey.wasPressedThisFrame) yield break;
                yield return null;
            }
        }
    }
}
