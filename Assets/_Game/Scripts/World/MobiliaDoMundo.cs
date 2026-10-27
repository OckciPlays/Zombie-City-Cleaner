using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A mobília na cena (Plano_Mapa, "A mobília", primeira fatia): guarda os modelos do Cidade_Mobilia.fbx e os registra
    /// na <see cref="Mobilia"/> ao nascer, antes de qualquer obra ser montada (as casas, as lojas e os andares dos prédios
    /// ganham os móveis em outra thread; o <see cref="TerrenoDoMundo"/> mostra os das casas e das lojas só perto). Fica no
    /// objeto "Mundo" da cena Mundo; as malhas vêm da montagem da cena (legíveis: a malha de cada modelo é copiada).
    /// </summary>
    public class MobiliaDoMundo : MonoBehaviour
    {
        [Tooltip("Os modelos do Cidade_Mobilia.fbx (os nomes são os da Mobilia.Feitos).")]
        public Mesh[] malhas;

        void Awake() => Mobilia.Registrar(malhas);

        void OnDestroy() => Mobilia.Limpar();
    }
}
