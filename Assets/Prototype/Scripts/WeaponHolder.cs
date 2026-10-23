using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>Armas do jogador: 1/2 (ou a rodinha do mouse) trocam a arma na mão. Só a arma atual fica ligada.</summary>
    public class WeaponHolder : MonoBehaviour
    {
        public Gun[] guns;
        public int startIndex;
        public FirstPersonController controller;

        int _index;

        public Gun Current => guns != null && guns.Length > 0 ? guns[_index] : null;

        void Start()
        {
            _index = Mathf.Clamp(startIndex, 0, guns.Length - 1);
            for (int i = 0; i < guns.Length; i++)
            {
                guns[i].enabled = i == _index;
                guns[i].viewModel.gameObject.SetActive(i == _index);   // OnDisable não roda se já começou desligada
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || (controller != null && (controller.IsPaused || !controller.InputEnabled))) return;

            for (int i = 0; i < guns.Length && i < 9; i++)
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame) Select(i);
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.1f) Select((_index + guns.Length - 1) % guns.Length);
            else if (scroll < -0.1f) Select((_index + 1) % guns.Length);
        }

        public void Select(int index)
        {
            if (index == _index || index < 0 || index >= guns.Length) return;
            guns[_index].enabled = false;
            _index = index;
            guns[_index].enabled = true;
            Effects.PlayAt(Effects.Click, transform.position + Vector3.up * 1.4f, 0.6f, 0.8f);
        }
    }
}
