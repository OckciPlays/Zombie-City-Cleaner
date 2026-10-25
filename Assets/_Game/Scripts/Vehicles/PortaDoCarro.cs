using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// A porta de um lugar do carro. O colisor (um gatilho do lado de fora da porta fechada) é o que a mira procura:
    /// "[E] Dirigir" na do motorista, "[E] Entrar no carro" nas outras. A folha (a malha da porta, com a origem na
    /// dobradiça) abre, espera a pessoa entrar ou sair e bate (<see cref="Ciclo"/>, tocado pelo carro em cada máquina
    /// quando o lugar muda).
    /// </summary>
    public class PortaDoCarro : Interagivel
    {
        public Carro carro;
        public int lugar;
        [Tooltip("A malha da porta (a origem na dobradiça).")]
        public Transform folha;
        [Tooltip("-1 na esquerda, +1 na direita: para que lado ela abre.")]
        public int lado = -1;
        [Tooltip("Quanto ela abre (graus).")]
        public float abertura = 62f;

        Quaternion _repouso;
        float _tempo = -1f;
        bool _bateu = true;

        void Awake()
        {
            if (folha != null) _repouso = folha.localRotation;
        }

        /// <summary>Abre, fica aberta enquanto a pessoa entra ou sai e bate.</summary>
        public void Ciclo()
        {
            // se ela já estava aberta (alguém entrou logo depois de outro sair), não estala de novo
            if (_tempo < 0f || _tempo > Carro.FecharEm) Efeitos.Som(SomDoCarro.PortaAbrir, folha.position, 0.7f, true, 20f);
            _tempo = _tempo >= 0f && _tempo <= Carro.FecharEm ? Mathf.Min(_tempo, Carro.AbrirPorta) : 0f;
            _bateu = false;
        }

        void Update()
        {
            if (_tempo < 0f || folha == null) return;
            _tempo += Time.deltaTime;
            float aberta;
            if (_tempo < Carro.AbrirPorta)
            {
                float t = _tempo / Carro.AbrirPorta;
                aberta = 1f - (1f - t) * (1f - t);   // começa rápido e chega devagar
            }
            else if (_tempo < Carro.FecharEm) aberta = 1f;
            else if (_tempo < Carro.FecharEm + Carro.FecharPorta)
            {
                float t = (_tempo - Carro.FecharEm) / Carro.FecharPorta;
                aberta = 1f - t * t;   // ganha velocidade até bater
            }
            else
            {
                aberta = 0f;
                _tempo = -1f;
                if (!_bateu)
                {
                    _bateu = true;
                    Efeitos.Som(SomDoCarro.PortaFechar, folha.position, 0.9f, true, 30f);
                }
            }
            folha.localRotation = _repouso * Quaternion.Euler(0f, -lado * abertura * aberta, 0f);
        }

        public override string Dica(ArmaJogador quem)
        {
            if (carro == null || !carro.IsSpawned || carro.Ocupado(lugar) || carro.Velocidade > Carro.VelocidadeParaEntrar) return null;
            var p = quem.GetComponent<PassageiroDoCarro>();
            if (p != null && p.Sentado) return null;
            return lugar == 0 ? "[E] Dirigir" : "[E] Entrar no carro";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (carro != null) carro.PedirEntrar(lugar);
        }
    }
}
