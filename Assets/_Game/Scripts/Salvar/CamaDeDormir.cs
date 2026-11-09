using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Salvar
{
    /// <summary>
    /// Uma cama da mobília (a de casal, a de solteiro, a de hospital e o beliche; o gatilho "Dormir" que o
    /// <see cref="World.MobiliaNovaDoMundo"/> põe em cada uma): E dorme e salva (02/11/2026, o save: no modo "só dormindo", é o
    /// jeito de salvar).
    /// <para>
    /// 08/11/2026, a pedido ("dormir nas camas dos prédios/bunker"): antes, só na casa da família, e nem lá funcionava (o
    /// molde da cama perde o colisor de malha, e a cama ficava sem o E). Agora vale em qualquer cama (das casas, dos prédios e
    /// do bunker), e o corpo deita no colchão: os pés no pé da cama e a cabeça na cabeceira
    /// (<see cref="VidaJogador.DeitarNaCama"/>); acordando, ele volta para onde estava, do lado dela. O lugar onde se volta
    /// quando cai continua sendo a cama improvisada.
    /// </para>
    /// </summary>
    public class CamaDeDormir : Interagivel
    {
        [Tooltip("O modelo da cama (a chave das medidas da mobília).")]
        public string modelo;

        /// <summary>
        /// As camas: a altura do colchão com a coberta (m, do chão, no tamanho do modelo: ×1,30 no mundo) e o lado da
        /// cabeceira no espaço do modelo (−1: no −Z, o lado da parede; o pé fica no +Z, onde vai o baú). Medidas nas malhas
        /// (08/11/2026; as mesmas do colchão da colisão, <see cref="MobiliaNova"/>.Cama).
        /// </summary>
        static (float colchao, float cabeceira) Medidas(string modelo) => modelo switch
        {
            "CamaCasal" => (0.585f, -1f),
            "CamaSolteiro" => (0.585f, -1f),
            "CamaHospital" => (0.7f, -1f),
            "Beliche" => (0.4f, -1f),
            _ => (0.55f, -1f),
        };

        /// <summary>O modelo é uma cama de dormir.</summary>
        public static bool EhCama(string modelo) => modelo == "CamaCasal" || modelo == "CamaSolteiro" || modelo == "Beliche" || modelo == "CamaHospital";

        public override string Dica(ArmaJogador quem) => "[E] Dormir e salvar";

        public override void Usar(ArmaJogador quem)
        {
            Salvamento.PedirDormir();
            var vida = JogadorRede.Local != null ? JogadorRede.Local.Vida : null;
            if (vida == null) return;
            if (Deitado(out var pos, out float yaw)) vida.DeitarNaCama(pos, yaw);
            vida.PedirDormir();   // a mente (06/11/2026): dormir de verdade
        }

        // onde fica a raiz de quem deita: no colchão, perto do pé da cama, olhando para o pé (o corpo deitado vai de costas
        // para trás da raiz: a cabeça na cabeceira)
        bool Deitado(out Vector3 pos, out float yaw)
        {
            pos = default;
            yaw = 0f;
            var cama = transform.parent;
            if (cama == null || string.IsNullOrEmpty(modelo) || !MedidasDaMobilia.Tem(modelo)) return false;
            var (colchao, cabeceira) = Medidas(modelo);
            Vector3 t = MedidasDaMobilia.Real(modelo);
            Vector2 meio = MedidasDaMobilia.Meio(modelo);
            float pe = -cabeceira;
            pos = cama.TransformPoint(new Vector3(meio.x, colchao + 0.02f, meio.y + pe * (t.z * 0.5f - 0.25f)));
            Vector3 frente = cama.TransformDirection(new Vector3(0f, 0f, pe));
            frente.y = 0f;
            if (frente.sqrMagnitude < 1e-4f) return false;
            yaw = Quaternion.LookRotation(frente).eulerAngles.y;
            return true;
        }
    }
}
