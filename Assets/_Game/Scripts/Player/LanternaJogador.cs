using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Lanterna presa no peito, no estilo Silent Hill: segue o olhar com um leve atraso e liga/desliga com F.
    /// Fica abaixo e à frente dos olhos, longe da pistola (colada na câmera, ela estourava a arma de luz).
    /// Os outros jogadores veem a luz de cada um, ligada ou não.
    /// </summary>
    public class LanternaJogador : NetworkBehaviour
    {
        public JogadorRede rede;
        public Light luz;
        [Tooltip("Posição em relação aos pés do jogador (x direita, y altura, z frente).")]
        public Vector3 posicaoNoPeito = new Vector3(0.08f, 1.28f, 0.22f);
        [Tooltip("Graus para baixo em relação ao olhar: a luz pega o chão à frente, não só o horizonte.")]
        public float inclinacao = 9f;
        public float atraso = 9f;

        public readonly NetworkVariable<bool> Ligada = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        InputAction _alternar;
        Quaternion _rotacao;
        float _proximaTrocaBot;

        void Awake()
        {
            _alternar = InputSystem.actions.FindAction("Player/Flashlight", true);
            _rotacao = transform.rotation;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                bool controla = !MenuOnline.Aberto && Cursor.lockState == CursorLockMode.Locked;
                bool trocar = MenuOnline.Bot ? Time.time >= _proximaTrocaBot : controla && _alternar.WasPressedThisFrame();
                if (MenuOnline.Bot && trocar) _proximaTrocaBot = Time.time + 5f;
                if (trocar)
                {
                    Ligada.Value = !Ligada.Value;
                    Efeitos.Som(Efeitos.Lanterna, luz.transform.position, 0.6f, false);
                }
            }
            luz.enabled = Ligada.Value;
            var alvo = transform.rotation * Quaternion.Euler(rede.Pitch.Value + inclinacao, 0f, 0f);
            _rotacao = Quaternion.Slerp(_rotacao, alvo, 1f - Mathf.Exp(-atraso * Time.deltaTime));
            luz.transform.SetPositionAndRotation(transform.TransformPoint(posicaoNoPeito), _rotacao);
        }
    }
}
