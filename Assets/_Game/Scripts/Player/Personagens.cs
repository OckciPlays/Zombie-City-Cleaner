using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os 4 personagens do jogador (27/10/2026, a pedido: cada jogador escolhe o seu antes de hospedar ou entrar): os
    /// modelos do Tripo do usuário, com o esqueleto e as animações do jogador de blocos (Modelo 3D/Personagens na pasta de
    /// arte, o personagens.py). A ordem é o número que vai pela rede (<see cref="JogadorRede.Personagem"/>), a linha da
    /// paleta dos braços de primeira pessoa (a pele de cada um, Jogador_Variacao0..3) e a do PERSONAGENS do script.
    /// </summary>
    public static class Personagens
    {
        public static readonly string[] Modelos = { "HomemBranco", "HomemGordo", "Mulher", "HomemNegro" };
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
