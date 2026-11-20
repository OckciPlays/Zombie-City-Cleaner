using UnityEngine;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os sons novos do urso (19/11/2026, com as gravações CC0 que o Pedro achou): o filhote chorando quando fica longe da
    /// mãe (o choro também entra no lugar do bufo e do rugido do filhote, em FaunaDoMundo.TocarSom) e o urso mastigando o
    /// salmão, deitado.
    /// </summary>
    public sealed partial class Bicho
    {
        float _choroLongeDaMae, _proximaMastigada, _proximoRosnadoDeLonge, _uivoAte;

        /// <summary>
        /// (20/11/2026, a pedido: "deveria ter uma animação dele uivando") o uivo com a pose: parado, a cabeça para o céu
        /// (AnimacaoDoBicho.Pose.Uivando, que vai às cópias), pelo tempo do uivo. O som passa na frente de outro que o bicho
        /// acabou de fazer (o rosnado), para não ficar mudo.
        /// </summary>
        internal void ComecarUivo(FaunaDoMundo f, float agora, float segundos = 4.5f)
        {
            if (Morto || Copia) return;
            Uivando = true;
            _uivoAte = agora + segundos;
            Estado = EstadoDoBicho.Alerta;
            _temDestino = false;
            _velAlvo = 0f;
            _olhar = 0f;
            _proximoSom = 0f;
            f.Uivar(this);
        }

        // (20/11/2026) o líder da matilha rosna para quem ele está olhando (de 5 a 10 s): o lobo avisa antes de cercar
        void RosnarDeLonge(float agora)
        {
            if (Grupo == null || Grupo.lider != this || agora < _proximoRosnadoDeLonge) return;
            _proximoRosnadoDeLonge = agora + Random.Range(5f, 10f);
            Tocar(SomDoBicho.Rosnado);
        }

        // o filhote longe da mãe chora de vez em quando (de 6 a 14 s)
        void ChorarLongeDaMae(float agora)
        {
            if (agora < _choroLongeDaMae) return;
            _choroLongeDaMae = agora + Random.Range(6f, 14f);
            Tocar(SomDoBicho.Filhote);
        }

        // o urso deitado comendo mastiga de vez em quando (de 2,5 a 5 s)
        void Mastigar(float agora)
        {
            if (agora < _proximaMastigada) return;
            _proximaMastigada = agora + Random.Range(2.5f, 5f);
            Tocar(SomDoBicho.Comendo);
        }
    }
}
