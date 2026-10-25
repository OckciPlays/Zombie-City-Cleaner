using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// O clipe de um som gerado por código. Uma amostra inválida (NaN ou infinita) num clipe contamina a mixagem e cala
    /// todo o áudio do jogo, que fica só com um ruído: foi o que o som de abrir a lata fazia (um filtro instável, veja
    /// <see cref="SomDePasso.Faixa"/>). Aqui elas viram silêncio, e o erro aparece no console.
    /// </summary>
    public static class SomGerado
    {
        public static AudioClip Clip(string nome, float[] dados, int taxa = 44100)
        {
            int ruins = 0;
            for (int i = 0; i < dados.Length; i++)
            {
                if (float.IsNaN(dados[i]) || float.IsInfinity(dados[i]))
                {
                    dados[i] = 0f;
                    ruins++;
                }
            }
            if (ruins > 0) Debug.LogError($"O som \"{nome}\" saiu com {ruins} amostras inválidas (viraram silêncio).");
            var clip = AudioClip.Create(nome, dados.Length, 1, taxa, false);
            clip.SetData(dados, 0);
            return clip;
        }
    }
}
