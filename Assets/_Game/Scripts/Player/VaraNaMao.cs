using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A vara de pesca na mão (06/11/2026): o lugar da lata segura a lata em pé; com a vara, ele gira para a vara apontar
    /// para onde o jogador olha, uns 35° acima (no dono, pelo olho; nos outros, pelo corpo e pelo Pitch da rede). A origem
    /// da vara é a pegada (o pé do molinete), então ela fica na mão; a ponta, para +Z.
    /// </summary>
    public class VaraNaMao : MonoBehaviour
    {
        JogadorRede _rede;
        Quaternion _antes;
        bool _ligada;

        public void Ligar(bool ligar)
        {
            if (ligar == _ligada) return;
            if (ligar) _antes = transform.localRotation;
            else transform.localRotation = _antes;
            _ligada = ligar;
        }

        void Awake() => _rede = GetComponentInParent<JogadorRede>();

        void LateUpdate()
        {
            if (!_ligada || _rede == null) return;
            if (_rede.IsOwner && _rede.Olho != null)
            {
                var o = _rede.Olho;
                transform.rotation = Quaternion.AngleAxis(-35f, o.right) * o.rotation;
            }
            else transform.rotation = Quaternion.Euler(Mathf.Clamp(_rede.Pitch.Value, -60f, 60f) - 35f, _rede.transform.eulerAngles.y, 0f);
        }
    }
}
