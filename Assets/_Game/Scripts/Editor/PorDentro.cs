using System.Reflection;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Para os testes pelo MCP (27/11/2026, a pesca nova): o RunCommand da Unity não deixa o roteiro usar o System.Reflection, e
    /// o teste precisa ler e mudar o que fica por dentro de um componente (a fase, o tempo, o peixe sorteado). Só no Editor:
    /// não vai para o jogo.
    /// </summary>
    public static class PorDentro
    {
        const BindingFlags Tudo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        /// <summary>O valor de um campo (privado ou não).</summary>
        public static T Ler<T>(object dono, string campo) => (T)dono.GetType().GetField(campo, Tudo).GetValue(dono);

        /// <summary>Muda um campo (privado ou não).</summary>
        public static void Por(object dono, string campo, object valor) => dono.GetType().GetField(campo, Tudo).SetValue(dono, valor);

        /// <summary>Chama um método (privado ou não).</summary>
        public static object Chamar(object dono, string metodo, params object[] argumentos) =>
            dono.GetType().GetMethod(metodo, Tudo).Invoke(dono, argumentos);
    }
}
