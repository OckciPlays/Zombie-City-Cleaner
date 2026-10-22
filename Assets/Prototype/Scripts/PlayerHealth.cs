using UnityEngine;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>Vida do jogador, com regeneração lenta depois de um tempo sem levar dano.</summary>
    public class PlayerHealth : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float regenDelay = 5f;
        public float regenPerSecond = 6f;

        public float Health { get; private set; }
        public bool IsDead { get; private set; }
        /// <summary>Intensidade do flash vermelho na tela (1 ao levar dano, cai até 0).</summary>
        public float DamageFlash { get; private set; }

        FirstPersonController _controller;
        float _lastDamageTime = -999f;

        void Awake()
        {
            Health = maxHealth;
            _controller = GetComponent<FirstPersonController>();
        }

        void Update()
        {
            DamageFlash = Mathf.MoveTowards(DamageFlash, 0f, Time.deltaTime * 2f);
            if (!IsDead && Time.time - _lastDamageTime > regenDelay)
                Health = Mathf.Min(maxHealth, Health + regenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            Health = Mathf.Max(0f, Health - amount);
            _lastDamageTime = Time.time;
            DamageFlash = 1f;
            if (_controller != null) _controller.AddShake(1f);
            if (Health <= 0f)
            {
                IsDead = true;
                if (_controller != null) _controller.Die();
            }
        }
    }
}
