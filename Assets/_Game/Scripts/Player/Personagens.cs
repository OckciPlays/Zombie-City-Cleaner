using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os personagens do jogador (cada jogador escolhe o seu antes de hospedar ou entrar). Desde 28/10/2026, a pedido,
    /// só o personagem principal em blocos (Modelo 3D/Principal na pasta de arte: o PrincipalBloco.blend e o
    /// bloco_exportar.py), no lugar dos 4 do Tripo. A ordem é o número que vai pela rede
    /// (<see cref="JogadorRede.Personagem"/>).
    /// </summary>
    public static class Personagens
    {
        public static readonly string[] Modelos = { "PrincipalBloco" };
        public static int Quantos => Modelos.Length;

        const string Chave = "personagem";
        static int _escolhido = -1;

        /// <summary>O escolhido nesta máquina (no menu, antes de hospedar ou entrar), guardado no PlayerPrefs.</summary>
        public static int Escolhido
        {
            get
            {
                if (_escolhido < 0) _escolhido = Mathf.Clamp(PlayerPrefs.GetInt(Chave, 0), 0, Quantos - 1);
                return _escolhido;
            }
            set
            {
                _escolhido = Mathf.Clamp(value, 0, Quantos - 1);
                PlayerPrefs.SetInt(Chave, _escolhido);
                PlayerPrefs.Save();
            }
        }

        static Texture2D[] _retratos;

        /// <summary>O retrato da tela de escolha (Art/UI/Personagens/Resources/Retrato_&lt;modelo&gt;.png, feito no Blender).</summary>
        public static Texture2D Retrato(int i)
        {
            if (_retratos == null) _retratos = new Texture2D[Quantos];
            if (_retratos[i] == null) _retratos[i] = Resources.Load<Texture2D>("Retrato_" + Modelos[i]);
            return _retratos[i];
        }
    }
}
